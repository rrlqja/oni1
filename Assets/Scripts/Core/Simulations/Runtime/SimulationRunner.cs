using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    public sealed class SimulationRunner
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly List<SimulationCommand> _commands = new(256);
        private readonly List<FlowBatchCommand> _flowCommands = new(256);

        private readonly LiquidFlowPlanner _liquidFlowPlanner;
        private readonly FlowBatchApplier _flowBatchApplier;

        private readonly GasFlowPlanner _gasFlowPlanner;

        public SimulationRunner(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            _liquidFlowPlanner = new LiquidFlowPlanner(_grid, _registry);
            _flowBatchApplier = new FlowBatchApplier(_grid, _registry);

            _gasFlowPlanner = new GasFlowPlanner(_grid, _registry);
        }

        public void Step(int currentTick)
        {
            _commands.Clear();
            _flowCommands.Clear();
            _grid.ClearAllTickReservations();

            bool leftToRight = (currentTick & 1) == 0;

            // 1) FallingSolid phase
            ScanAndCreateCommands(currentTick, leftToRight);
            ApplyCommands();

            _grid.ClearAllTickReservations();

            // 2) Liquid phase
            _flowCommands.Clear();
            _liquidFlowPlanner.BuildNormalFlowBatches(currentTick, leftToRight, _flowCommands);
            _flowBatchApplier.Apply(_flowCommands);

            _grid.ClearAllTickReservations();

            // 3) Gas phase A — 균등화 (같은 가스끼리 질량 분배)
            _flowCommands.Clear();
            _gasFlowPlanner.BuildNormalFlowBatches(currentTick, leftToRight, _flowCommands);
            _flowBatchApplier.Apply(_flowCommands);

            _grid.ClearAllTickReservations();

            // 4) Gas phase B — 밀도 인지 이동 (진공 drift + 이종 가스 swap)
            _flowCommands.Clear();
            _commands.Clear();
            _gasFlowPlanner.BuildDensityAwareMovement(
                currentTick, leftToRight, _flowCommands, _commands);

            // drift (진공 이동)는 FlowBatchApplier로
            _flowBatchApplier.Apply(_flowCommands);
            // swap (이종 가스 교환)는 ApplyCommands로
            ApplyCommands();

            _grid.ClearAllTickReservations();
        }

        private void ScanAndCreateCommands(int currentTick, bool leftToRight)
        {
            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            // 아래 -> 위
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int fromIndex = _grid.ToIndex(x, y);

                    ref TickMeta fromMeta = ref _grid.GetTickMetaRef(fromIndex);
                    if (fromMeta.HasActedThisTick(currentTick))
                        continue;

                    SimCell actorCell = _grid.GetCell(x, y);
                    ref readonly ElementRuntimeDefinition actorElement = ref _registry.Get(actorCell.ElementId);

                    if (!ElementInteractionRules.IsFallingSolid(actorElement))
                        continue;

                    int belowY = y - 1;
                    if (belowY < 0)
                        continue;

                    int toIndex = _grid.ToIndex(x, belowY);

                    ref TickMeta toMeta = ref _grid.GetTickMetaRef(toIndex);
                    if (toMeta.HasActedThisTick(currentTick))
                        continue;

                    SimCell targetCell = _grid.GetCell(x, belowY);
                    ref readonly ElementRuntimeDefinition targetElement = ref _registry.Get(targetCell.ElementId);

                    DownInteractionResult interaction =
                        ElementInteractionRules.EvaluateDownInteraction(
                            actorCell,
                            actorElement,
                            targetCell,
                            targetElement);

                    switch (interaction)
                    {
                        case DownInteractionResult.Move:
                            TryReserveMove(currentTick, fromIndex, toIndex);
                            break;

                        case DownInteractionResult.Swap:
                            TryReserveSwap(currentTick, fromIndex, toIndex);
                            break;

                        case DownInteractionResult.Merge:
                            TryReserveMergeMass(currentTick, fromIndex, toIndex);
                            break;

                        case DownInteractionResult.Blocked:
                        default:
                            break;
                    }
                }
            }
        }

        private void TryReserveMove(int currentTick, int fromIndex, int toIndex)
        {
            ref TickMeta fromMeta = ref _grid.GetTickMetaRef(fromIndex);
            ref TickMeta toMeta = ref _grid.GetTickMetaRef(toIndex);

            if (fromMeta.ReservationMask != 0)
                return;

            if (toMeta.ReservationMask != 0)
                return;

            fromMeta.MarkActed(currentTick);
            toMeta.MarkActed(currentTick);

            fromMeta.AddReservation(TickReservationMask.SourceReserved);
            toMeta.AddReservation(TickReservationMask.TargetReserved);

            _commands.Add(SimulationCommand.CreateMove(fromIndex, toIndex));
        }

        private void TryReserveSwap(int currentTick, int aIndex, int bIndex)
        {
            ref TickMeta aMeta = ref _grid.GetTickMetaRef(aIndex);
            ref TickMeta bMeta = ref _grid.GetTickMetaRef(bIndex);

            if (aMeta.ReservationMask != 0)
                return;

            if (bMeta.ReservationMask != 0)
                return;

            aMeta.MarkActed(currentTick);
            bMeta.MarkActed(currentTick);

            aMeta.AddReservation(TickReservationMask.SourceReserved);
            bMeta.AddReservation(TickReservationMask.TargetReserved);

            _commands.Add(SimulationCommand.CreateSwap(aIndex, bIndex));
        }

        private void TryReserveMergeMass(int currentTick, int sourceIndex, int targetIndex)
        {
            ref TickMeta sourceMeta = ref _grid.GetTickMetaRef(sourceIndex);
            ref TickMeta targetMeta = ref _grid.GetTickMetaRef(targetIndex);

            if (sourceMeta.ReservationMask != 0)
                return;

            if (targetMeta.ReservationMask != 0)
                return;

            sourceMeta.MarkActed(currentTick);
            targetMeta.MarkActed(currentTick);

            sourceMeta.AddReservation(TickReservationMask.SourceReserved);
            targetMeta.AddReservation(TickReservationMask.TargetReserved);

            _commands.Add(SimulationCommand.CreateMergeMass(sourceIndex, targetIndex));
        }

        private void ApplyCommands()
        {
            ref readonly ElementRuntimeDefinition vacuum = ref _registry.Get(BuiltInElementIds.Vacuum);
            SimCell vacuumCell = new SimCell(
                elementId: vacuum.Id,
                mass: vacuum.DefaultMass,
                temperature: 0,
                flags: SimCellFlags.None);

            for (int i = 0; i < _commands.Count; i++)
            {
                SimulationCommand command = _commands[i];

                switch (command.Type)
                {
                    case SimulationCommandType.Move:
                        ApplyMove(command, vacuumCell);
                        break;

                    case SimulationCommandType.Swap:
                        ApplySwap(command);
                        break;

                    case SimulationCommandType.MergeMass:
                        ApplyMergeMass(command, vacuumCell);
                        break;

                    case SimulationCommandType.FlowBatch:
                    case SimulationCommandType.Transform:
                    case SimulationCommandType.None:
                    default:
                        break;
                }
            }
        }

        private void ApplyMove(SimulationCommand command, SimCell vacuumCell)
        {
            ref SimCell fromCell = ref _grid.GetCellRef(command.FromIndex);
            ref SimCell toCell = ref _grid.GetCellRef(command.ToIndex);

            toCell = fromCell;
            fromCell = vacuumCell;
        }

        private void ApplySwap(SimulationCommand command)
        {
            ref SimCell aCell = ref _grid.GetCellRef(command.FromIndex);
            ref SimCell bCell = ref _grid.GetCellRef(command.ToIndex);

            SimCell temp = aCell;
            aCell = bCell;
            bCell = temp;
        }

        private void ApplyMergeMass(SimulationCommand command, SimCell vacuumCell)
        {
            ref SimCell sourceCell = ref _grid.GetCellRef(command.FromIndex);
            ref SimCell targetCell = ref _grid.GetCellRef(command.ToIndex);

            if (sourceCell.ElementId != targetCell.ElementId)
                return;

            ref readonly ElementRuntimeDefinition element = ref _registry.Get(targetCell.ElementId);

            int targetCapacity = element.MaxMass - targetCell.Mass;
            if (targetCapacity <= 0)
                return;

            int transferMass = Math.Min(sourceCell.Mass, targetCapacity);
            if (transferMass <= 0)
                return;

            int sourceMassBefore = sourceCell.Mass;
            int targetMassBefore = targetCell.Mass;

            short mergedTemperature = ComputeMassWeightedTemperature(
                sourceCell.Temperature,
                sourceMassBefore,
                targetCell.Temperature,
                targetMassBefore,
                transferMass);

            targetCell.Mass += transferMass;
            targetCell.Temperature = mergedTemperature;

            sourceCell.Mass -= transferMass;

            if (sourceCell.Mass <= 0)
            {
                sourceCell = vacuumCell;
            }
        }

        private static short ComputeMassWeightedTemperature(
            short sourceTemperature,
            int sourceMassBefore,
            short targetTemperature,
            int targetMassBefore,
            int transferredMass)
        {
            if (transferredMass <= 0)
                return targetTemperature;

            long weightedSource = (long)sourceTemperature * transferredMass;
            long weightedTarget = (long)targetTemperature * targetMassBefore;

            long totalMass = targetMassBefore + transferredMass;
            if (totalMass <= 0)
                return targetTemperature;

            long result = (weightedSource + weightedTarget) / totalMass;
            return (short)result;
        }
    }
}