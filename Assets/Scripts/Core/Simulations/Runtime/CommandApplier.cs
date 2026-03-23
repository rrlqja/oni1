using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// SimulationCommand 일괄 적용기.
    /// Move, Swap, MergeMass, ForceMergeMass 커맨드를 실행한다.
    /// </summary>
    public sealed class CommandApplier
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        public CommandApplier(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Apply(IReadOnlyList<SimulationCommand> commands)
        {
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));

            if (commands.Count == 0)
                return;

            ref readonly ElementRuntimeDefinition vacuum = ref _registry.Get(BuiltInElementIds.Vacuum);
            SimCell vacuumCell = new SimCell(
                elementId: vacuum.Id,
                mass: vacuum.DefaultMass,
                temperature: 0f,
                flags: SimCellFlags.None);

            for (int i = 0; i < commands.Count; i++)
            {
                SimulationCommand command = commands[i];

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

                    case SimulationCommandType.ForceMergeMass:
                        ApplyForceMergeMass(command, vacuumCell);
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

            float mergedTemperature = ComputeMassWeightedTemperature(
                sourceCell.Temperature, sourceMassBefore,
                targetCell.Temperature, targetMassBefore,
                transferMass);

            targetCell.Mass += transferMass;
            targetCell.Temperature = mergedTemperature;

            sourceCell.Mass -= transferMass;

            if (sourceCell.Mass <= 0)
            {
                sourceCell = vacuumCell;
            }
        }

        /// <summary>
        /// MaxMass를 무시하고 전량 강제 합산한다.
        /// 밀어내기(Displacement)에서 사용: 기체를 동종에 압축 합류.
        /// source는 항상 진공이 된다.
        /// </summary>
        private void ApplyForceMergeMass(SimulationCommand command, SimCell vacuumCell)
        {
            ref SimCell sourceCell = ref _grid.GetCellRef(command.FromIndex);
            ref SimCell targetCell = ref _grid.GetCellRef(command.ToIndex);

            // 원소 불일치 → 무시 (안전 장치)
            if (sourceCell.ElementId != targetCell.ElementId)
                return;

            int transferMass = sourceCell.Mass;
            if (transferMass <= 0)
            {
                sourceCell = vacuumCell;
                return;
            }

            int targetMassBefore = targetCell.Mass;

            float mergedTemperature = ComputeMassWeightedTemperature(
                sourceCell.Temperature, transferMass,
                targetCell.Temperature, targetMassBefore,
                transferMass);

            // MaxMass 무시 — 전량 합산
            targetCell.Mass += transferMass;
            targetCell.Temperature = mergedTemperature;

            sourceCell = vacuumCell;
        }

        private static float ComputeMassWeightedTemperature(
            float sourceTemperature, int sourceMassBefore,
            float targetTemperature, int targetMassBefore,
            int transferredMass)
        {
            if (transferredMass <= 0)
                return targetTemperature;

            float weightedSource = sourceTemperature * transferredMass;
            float weightedTarget = targetTemperature * targetMassBefore;

            long totalMass = (long)targetMassBefore + transferredMass;
            if (totalMass <= 0)
                return targetTemperature;

            return (weightedSource + weightedTarget) / totalMass;
        }
    }
}