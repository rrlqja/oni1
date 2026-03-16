using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// Gas is solved as local equalization.
    /// A gas cell looks at itself + reachable 4-neighbor vacuum/same-gas cells,
    /// then sends mass to reduce local differences.
    /// No center-retain rule, no relay rule, no directional preference.
    /// </summary>
    public sealed class GasFlowPlanner
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        public GasFlowPlanner(WorldGrid grid, ElementRegistry registry)
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

                    SimCell sourceCell = _grid.GetCellByIndex(sourceIndex);
                    ref readonly ElementRuntimeDefinition sourceElement = ref _registry.Get(sourceCell.ElementId);

                    if (sourceElement.BehaviorType != ElementBehaviorType.Gas)
                        continue;

                    if (sourceCell.Mass <= 0)
                        continue;

                    if (TryBuildGasNormalBatch(
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

        private bool TryBuildGasNormalBatch(
            int x,
            int y,
            int sourceIndex,
            in SimCell sourceCell,
            in ElementRuntimeDefinition sourceElement,
            bool leftToRight,
            out FlowBatchCommand batch)
        {
            batch = default;

            int flowUnit = Math.Max(1, sourceElement.MinSpreadMass);
            int sourceMass = sourceCell.Mass;
            int maxMass = sourceElement.MaxMass;

            if (sourceMass < flowUnit)
                return false;

            int[] candidateIndices = new int[4] { -1, -1, -1, -1 };
            int[] candidateMasses = new int[4];
            int candidateCount = 0;

            // 방향 우선 규칙은 없고, 동률일 때만 좌/우 순서가 번갈아 영향을 줄 수 있게 둔다.
            int firstHorizontalX = leftToRight ? x - 1 : x + 1;
            int secondHorizontalX = leftToRight ? x + 1 : x - 1;

            TryAddCandidate(firstHorizontalX, y, sourceCell.ElementId, candidateIndices, candidateMasses, ref candidateCount);
            TryAddCandidate(secondHorizontalX, y, sourceCell.ElementId, candidateIndices, candidateMasses, ref candidateCount);
            TryAddCandidate(x, y + 1, sourceCell.ElementId, candidateIndices, candidateMasses, ref candidateCount);
            TryAddCandidate(x, y - 1, sourceCell.ElementId, candidateIndices, candidateMasses, ref candidateCount);

            if (candidateCount == 0)
                return false;

            int totalMass = sourceMass;
            for (int i = 0; i < candidateCount; i++)
            {
                totalMass += candidateMasses[i];
            }

            // self + neighbors local equalization
            int targetMass = totalMass / (candidateCount + 1);
            targetMass = Math.Min(targetMass, maxMass);

            int available = sourceMass - targetMass;
            available = QuantizeDown(available, flowUnit);

            if (available <= 0)
                return false;

            int[] needs = new int[4];
            int totalNeed = 0;

            for (int i = 0; i < candidateCount; i++)
            {
                int need = Math.Max(0, targetMass - candidateMasses[i]);
                need = QuantizeDown(need, flowUnit);

                needs[i] = need;
                totalNeed += need;
            }

            if (totalNeed <= 0)
                return false;

            int distributable = Math.Min(available, totalNeed);
            distributable = QuantizeDown(distributable, flowUnit);

            if (distributable <= 0)
                return false;

            int[] planned = new int[4];
            long[] fractional = new long[4];
            int plannedTotal = 0;

            // 1차: 비례 배분
            for (int i = 0; i < candidateCount; i++)
            {
                if (needs[i] <= 0)
                    continue;

                long scaled = (long)distributable * needs[i];
                int baseTransfer = (int)(scaled / totalNeed);
                baseTransfer = QuantizeDown(baseTransfer, flowUnit);

                planned[i] = Math.Min(baseTransfer, needs[i]);
                fractional[i] = scaled % totalNeed;
                plannedTotal += planned[i];
            }

            // 2차: 남은 양을 fractional 큰 순서대로 flowUnit씩 배분
            int remainder = distributable - plannedTotal;
            while (remainder >= flowUnit)
            {
                int bestIndex = -1;
                long bestFraction = long.MinValue;

                for (int i = 0; i < candidateCount; i++)
                {
                    if (needs[i] - planned[i] < flowUnit)
                        continue;

                    if (fractional[i] > bestFraction)
                    {
                        bestFraction = fractional[i];
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                    break;

                planned[bestIndex] += flowUnit;
                fractional[bestIndex] = -1;
                remainder -= flowUnit;
            }

            FlowTransferPlan transfer0 = default;
            FlowTransferPlan transfer1 = default;
            FlowTransferPlan transfer2 = default;
            FlowTransferPlan transfer3 = default;
            byte transferCount = 0;

            for (int i = 0; i < candidateCount; i++)
            {
                if (planned[i] <= 0)
                    continue;

                FlowTransferPlan plan = new FlowTransferPlan(candidateIndices[i], planned[i]);

                if (transferCount == 0) transfer0 = plan;
                else if (transferCount == 1) transfer1 = plan;
                else if (transferCount == 2) transfer2 = plan;
                else transfer3 = plan;

                transferCount++;
            }

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

        private void TryAddCandidate(
            int targetX,
            int targetY,
            byte sourceElementId,
            int[] candidateIndices,
            int[] candidateMasses,
            ref int candidateCount)
        {
            if (targetX < 0 || targetX >= _grid.Width)
                return;

            if (targetY < 0 || targetY >= _grid.Height)
                return;

            if (candidateCount >= 4)
                return;

            int targetIndex = _grid.ToIndex(targetX, targetY);
            SimCell targetCell = _grid.GetCellByIndex(targetIndex);

            if (!CanBeGasNormalTarget(sourceElementId, targetCell))
                return;

            candidateIndices[candidateCount] = targetIndex;
            candidateMasses[candidateCount] = targetCell.ElementId == BuiltInElementIds.Vacuum
                ? 0
                : targetCell.Mass;

            candidateCount++;
        }

        private static bool CanBeGasNormalTarget(byte sourceElementId, in SimCell targetCell)
        {
            if (targetCell.ElementId == BuiltInElementIds.Vacuum)
                return true;

            return targetCell.ElementId == sourceElementId;
        }

        private static int QuantizeDown(int value, int unit)
        {
            if (unit <= 1)
                return value;

            return value / unit * unit;
        }
    }
}