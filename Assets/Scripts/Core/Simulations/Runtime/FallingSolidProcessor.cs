using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// FallingSolid 행동 처리기.
    /// SimulationRunner에서 분리된 로직: 아래 이동, Swap, Merge.
    /// </summary>
    public sealed class FallingSolidProcessor
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        public FallingSolidProcessor(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 전체 셀을 순회하며 FallingSolid의 아래 이동 커맨드를 생성한다.
        /// </summary>
        public void BuildCommands(
            int currentTick,
            bool leftToRight,
            List<SimulationCommand> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            // 아래 → 위 순서로 순회
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int fromIndex = _grid.ToIndex(x, y);

                    ref TickMeta fromMeta = ref _grid.GetTickMetaRef(fromIndex);
                    if (fromMeta.HasActedThisTick(currentTick))
                        continue;

                    SimCell actorCell = _grid.GetCell(x, y);
                    ref readonly ElementRuntimeDefinition actorElement =
                        ref _registry.Get(actorCell.ElementId);

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
                    ref readonly ElementRuntimeDefinition targetElement =
                        ref _registry.Get(targetCell.ElementId);

                    DownInteractionResult interaction =
                        ElementInteractionRules.EvaluateDownInteraction(
                            actorCell, actorElement,
                            targetCell, targetElement);

                    switch (interaction)
                    {
                        case DownInteractionResult.Move:
                            TryReserveMove(currentTick, fromIndex, toIndex, output);
                            break;

                        case DownInteractionResult.Swap:
                            TryReserveSwap(currentTick, fromIndex, toIndex, output);
                            break;

                        case DownInteractionResult.Merge:
                            TryReserveMergeMass(currentTick, fromIndex, toIndex, output);
                            break;

                        case DownInteractionResult.Blocked:
                        default:
                            break;
                    }
                }
            }
        }

        private void TryReserveMove(
            int currentTick, int fromIndex, int toIndex,
            List<SimulationCommand> output)
        {
            ref TickMeta fromMeta = ref _grid.GetTickMetaRef(fromIndex);
            ref TickMeta toMeta = ref _grid.GetTickMetaRef(toIndex);

            if (fromMeta.ReservationMask != 0) return;
            if (toMeta.ReservationMask != 0) return;

            fromMeta.MarkActed(currentTick);
            toMeta.MarkActed(currentTick);

            fromMeta.AddReservation(TickReservationMask.SourceReserved);
            toMeta.AddReservation(TickReservationMask.TargetReserved);

            output.Add(SimulationCommand.CreateMove(fromIndex, toIndex));
        }

        private void TryReserveSwap(
            int currentTick, int aIndex, int bIndex,
            List<SimulationCommand> output)
        {
            ref TickMeta aMeta = ref _grid.GetTickMetaRef(aIndex);
            ref TickMeta bMeta = ref _grid.GetTickMetaRef(bIndex);

            if (aMeta.ReservationMask != 0) return;
            if (bMeta.ReservationMask != 0) return;

            aMeta.MarkActed(currentTick);
            bMeta.MarkActed(currentTick);

            aMeta.AddReservation(TickReservationMask.SourceReserved);
            bMeta.AddReservation(TickReservationMask.TargetReserved);

            output.Add(SimulationCommand.CreateSwap(aIndex, bIndex));
        }

        private void TryReserveMergeMass(
            int currentTick, int sourceIndex, int targetIndex,
            List<SimulationCommand> output)
        {
            ref TickMeta sourceMeta = ref _grid.GetTickMetaRef(sourceIndex);
            ref TickMeta targetMeta = ref _grid.GetTickMetaRef(targetIndex);

            if (sourceMeta.ReservationMask != 0) return;
            if (targetMeta.ReservationMask != 0) return;

            sourceMeta.MarkActed(currentTick);
            targetMeta.MarkActed(currentTick);

            sourceMeta.AddReservation(TickReservationMask.SourceReserved);
            targetMeta.AddReservation(TickReservationMask.TargetReserved);

            output.Add(SimulationCommand.CreateMergeMass(sourceIndex, targetIndex));
        }
    }
}