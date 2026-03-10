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

        public SimulationRunner(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Step(int currentTick)
        {
            _commands.Clear();
            _grid.ClearAllTickReservations();

            bool leftToRight = (currentTick & 1) == 0;

            ScanAndCreateCommands(currentTick, leftToRight);
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

                    // 맨 아래는 더 내려갈 수 없음
                    int belowY = y - 1;
                    if (belowY < 0)
                        continue;

                    int toIndex = _grid.ToIndex(x, belowY);
                    SimCell targetCell = _grid.GetCell(x, belowY);
                    ref readonly ElementRuntimeDefinition targetElement = ref _registry.Get(targetCell.ElementId);

                    DownInteractionResult interaction =
                        ElementInteractionRules.EvaluateDownInteraction(actorElement, targetElement);

                    switch (interaction)
                    {
                        case DownInteractionResult.Move:
                            TryReserveMove(currentTick, fromIndex, toIndex);
                            break;

                        case DownInteractionResult.Replace:
                            // 다음 단계에서 Replace 구현 예정
                            // 지금은 구조만 열어두고 실제로는 아무것도 하지 않음
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
            fromMeta.AddReservation(TickReservationMask.SourceReserved);
            toMeta.AddReservation(TickReservationMask.TargetReserved);

            _commands.Add(SimulationCommand.CreateMove(fromIndex, toIndex));
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

                    case SimulationCommandType.Replace:
                        // 다음 단계
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
    }
}