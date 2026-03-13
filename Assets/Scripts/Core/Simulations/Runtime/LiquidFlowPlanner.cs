using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    public sealed class LiquidFlowPlanner
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        private const int MAX_LATERAL_TRANSFER_PER_SIDE_PER_TICK = 100000; // 100kg

        public LiquidFlowPlanner(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void BuildNormalFlowBatches(
            int currentTick,
            bool leftToRight,
            List<FlowBatchCommand> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();

            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int sourceIndex = _grid.ToIndex(x, y);

                    ref TickMeta sourceMeta = ref _grid.GetTickMetaRef(sourceIndex);
                    if (sourceMeta.HasActedThisTick(currentTick))
                        continue;

                    SimCell sourceCell = _grid.GetCell(x, y);
                    ref readonly ElementRuntimeDefinition sourceElement = ref _registry.Get(sourceCell.ElementId);

                    if (sourceElement.BehaviorType != ElementBehaviorType.Liquid)
                        continue;

                    if (sourceCell.Mass <= 0)
                        continue;

                    if (TryBuildLiquidNormalBatch(
                            x,
                            y,
                            sourceIndex,
                            sourceCell,
                            sourceElement,
                            leftToRight,
                            out FlowBatchCommand batch))
                    {
                        sourceMeta.MarkActed(currentTick);
                        output.Add(batch);
                    }
                }
            }
        }

        private bool TryBuildLiquidNormalBatch(
            int x,
            int y,
            int sourceIndex,
            in SimCell sourceCell,
            in ElementRuntimeDefinition sourceElement,
            bool leftToRight,
            out FlowBatchCommand batch)
        {
            batch = default;

            int sourceMassSnapshot = sourceCell.Mass;
            int currentRemainingMass = sourceMassSnapshot;
            int maxMass = sourceElement.MaxMass;

            FlowTransferPlan transfer0 = default;
            FlowTransferPlan transfer1 = default;
            FlowTransferPlan transfer2 = default;
            FlowTransferPlan transfer3 = default;
            byte transferCount = 0;

            // 1) 아래 우선
            int belowY = y - 1;
            if (belowY >= 0)
            {
                int belowIndex = _grid.ToIndex(x, belowY);
                SimCell belowCell = _grid.GetCell(x, belowY);

                if (CanBeLiquidNormalTarget(sourceCell.ElementId, belowCell))
                {
                    int capacity = GetLiquidNormalTargetCapacity(sourceCell.ElementId, maxMass, belowCell);
                    int planned = Math.Min(currentRemainingMass, capacity);

                    if (planned > 0)
                    {
                        transfer0 = new FlowTransferPlan(belowIndex, planned);
                        transferCount++;
                        currentRemainingMass -= planned;
                    }
                }
            }

            // 아래로 먼저 흘렀더라도, 남은 질량이 충분하면 좌우 확산도 계속 시도한다.
            if (currentRemainingMass <= sourceElement.MinSpreadMass)
            {
                if (transferCount == 0)
                    return false;

                batch = new FlowBatchCommand(
                    sourceIndex,
                    sourceCell.ElementId,
                    sourceCell.Temperature,
                    FlowBatchMode.Normal,
                    transferCount,
                    transfer0,
                    transfer1,
                    transfer2,
                    transfer3);

                return true;
            }

            // 2) 아래가 비거나 같은 원소여도, 남는 질량이 충분하면 좌우 확산 가능
            int desiredPerSide = currentRemainingMass / sourceElement.Viscosity;
            desiredPerSide = Math.Min(desiredPerSide, MAX_LATERAL_TRANSFER_PER_SIDE_PER_TICK);

            if (desiredPerSide <= 0)
            {
                if (transferCount == 0)
                    return false;

                batch = new FlowBatchCommand(
                    sourceIndex,
                    sourceCell.ElementId,
                    sourceCell.Temperature,
                    FlowBatchMode.Normal,
                    transferCount,
                    transfer0,
                    transfer1,
                    transfer2,
                    transfer3);

                return true;
            }

            int firstSideX = leftToRight ? x - 1 : x + 1;
            int secondSideX = leftToRight ? x + 1 : x - 1;

            TryPlanHorizontalTransfer(
                firstSideX,
                y,
                sourceCell,
                sourceElement,
                ref currentRemainingMass,
                desiredPerSide,
                ref transferCount,
                ref transfer0,
                ref transfer1,
                ref transfer2,
                ref transfer3);

            TryPlanHorizontalTransfer(
                secondSideX,
                y,
                sourceCell,
                sourceElement,
                ref currentRemainingMass,
                desiredPerSide,
                ref transferCount,
                ref transfer0,
                ref transfer1,
                ref transfer2,
                ref transfer3);

            if (transferCount == 0)
                return false;

            batch = new FlowBatchCommand(
                sourceIndex,
                sourceCell.ElementId,
                sourceCell.Temperature,
                FlowBatchMode.Normal,
                transferCount,
                transfer0,
                transfer1,
                transfer2,
                transfer3);

            return true;
        }

        private void TryPlanHorizontalTransfer(
            int targetX,
            int targetY,
            in SimCell sourceCell,
            in ElementRuntimeDefinition sourceElement,
            ref int currentRemainingMass,
            int desiredPerSide,
            ref byte transferCount,
            ref FlowTransferPlan transfer0,
            ref FlowTransferPlan transfer1,
            ref FlowTransferPlan transfer2,
            ref FlowTransferPlan transfer3)
        {
            if (targetX < 0 || targetX >= _grid.Width)
                return;

            int targetIndex = _grid.ToIndex(targetX, targetY);
            SimCell targetCell = _grid.GetCell(targetX, targetY);

            if (!CanBeLiquidNormalTarget(sourceCell.ElementId, targetCell))
                return;

            int targetCapacity = GetLiquidNormalTargetCapacity(
                sourceCell.ElementId,
                sourceElement.MaxMass,
                targetCell);

            if (targetCapacity <= 0)
                return;

            int allowedByMinSpreadMass = currentRemainingMass - sourceElement.MinSpreadMass;
            if (allowedByMinSpreadMass <= 0)
                return;

            int planned = Math.Min(desiredPerSide, targetCapacity);
            planned = Math.Min(planned, allowedByMinSpreadMass);

            if (planned <= 0)
                return;

            if (transferCount == 0) transfer0 = new FlowTransferPlan(targetIndex, planned);
            else if (transferCount == 1) transfer1 = new FlowTransferPlan(targetIndex, planned);
            else if (transferCount == 2) transfer2 = new FlowTransferPlan(targetIndex, planned);
            else transfer3 = new FlowTransferPlan(targetIndex, planned);

            transferCount++;
            currentRemainingMass -= planned;
        }

        private static bool CanBeLiquidNormalTarget(byte sourceElementId, in SimCell targetCell)
        {
            if (targetCell.ElementId == BuiltInElementIds.Vacuum)
                return true;

            return targetCell.ElementId == sourceElementId;
        }

        private static int GetLiquidNormalTargetCapacity(byte sourceElementId, int maxMass, in SimCell targetCell)
        {
            if (targetCell.ElementId == BuiltInElementIds.Vacuum)
                return maxMass;

            if (targetCell.ElementId == sourceElementId)
                return Math.Max(0, maxMass - targetCell.Mass);

            return 0;
        }
    }
}