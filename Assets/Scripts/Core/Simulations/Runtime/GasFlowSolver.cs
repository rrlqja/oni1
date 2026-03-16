using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// Gas is not treated as "source-centered spreading".
    /// It is solved as repeated local mass exchange across 4-neighbor edges.
    /// </summary>
    public sealed class GasFlowSolver
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        // Start conservative. Tune later.
        private const int MAX_TRANSFER_PER_EDGE_PER_SUBSTEP = 100;

        public GasFlowSolver(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void BuildExchangeBatches(bool leftToRight, List<FlowBatchCommand> output)
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
                    SimCell sourceCell = _grid.GetCellByIndex(sourceIndex);

                    ref readonly ElementRuntimeDefinition sourceElement = ref _registry.Get(sourceCell.ElementId);
                    if (sourceElement.BehaviorType != ElementBehaviorType.Gas)
                        continue;

                    if (sourceCell.Mass <= 0)
                        continue;

                    if (TryBuildExchangeBatch(
                            x,
                            y,
                            sourceIndex,
                            sourceCell,
                            sourceElement,
                            leftToRight,
                            out FlowBatchCommand batch))
                    {
                        output.Add(batch);
                    }
                }
            }
        }

        private bool TryBuildExchangeBatch(
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
            if (sourceMass < flowUnit)
                return false;

            int[] candidateIndices = new int[4] { -1, -1, -1, -1 };
            int[] candidateMasses = new int[4];
            int[] proposalMasses = new int[4];
            long[] fractionalScaled = new long[4];
            int candidateCount = 0;

            // No directional preference.
            // This only acts as a tie-break order to reduce fixed bias.
            int firstHorizontalX = leftToRight ? x - 1 : x + 1;
            int secondHorizontalX = leftToRight ? x + 1 : x - 1;

            TryAddExchangeCandidate(firstHorizontalX, y, sourceCell.ElementId, sourceMass, sourceElement.MaxMass,
                candidateIndices, candidateMasses, proposalMasses, ref candidateCount, flowUnit);

            TryAddExchangeCandidate(secondHorizontalX, y, sourceCell.ElementId, sourceMass, sourceElement.MaxMass,
                candidateIndices, candidateMasses, proposalMasses, ref candidateCount, flowUnit);

            TryAddExchangeCandidate(x, y + 1, sourceCell.ElementId, sourceMass, sourceElement.MaxMass,
                candidateIndices, candidateMasses, proposalMasses, ref candidateCount, flowUnit);

            TryAddExchangeCandidate(x, y - 1, sourceCell.ElementId, sourceMass, sourceElement.MaxMass,
                candidateIndices, candidateMasses, proposalMasses, ref candidateCount, flowUnit);

            if (candidateCount == 0)
                return false;

            int totalProposal = 0;
            for (int i = 0; i < candidateCount; i++)
            {
                totalProposal += proposalMasses[i];
            }

            if (totalProposal <= 0)
                return false;

            int available = QuantizeDown(sourceMass, flowUnit);
            if (available <= 0)
                return false;

            int[] plannedMasses = new int[4];
            int plannedTotal = 0;

            if (totalProposal <= available)
            {
                for (int i = 0; i < candidateCount; i++)
                {
                    plannedMasses[i] = proposalMasses[i];
                    plannedTotal += plannedMasses[i];
                }
            }
            else
            {
                for (int i = 0; i < candidateCount; i++)
                {
                    long scaled = (long)proposalMasses[i] * available;
                    int scaledFloor = (int)(scaled / totalProposal);
                    scaledFloor = QuantizeDown(scaledFloor, flowUnit);

                    plannedMasses[i] = scaledFloor;
                    fractionalScaled[i] = scaled % totalProposal;
                    plannedTotal += scaledFloor;
                }

                int remainder = available - plannedTotal;

                while (remainder >= flowUnit)
                {
                    int bestIndex = -1;
                    long bestFraction = long.MinValue;

                    for (int i = 0; i < candidateCount; i++)
                    {
                        if (plannedMasses[i] + flowUnit > proposalMasses[i])
                            continue;

                        if (fractionalScaled[i] > bestFraction)
                        {
                            bestFraction = fractionalScaled[i];
                            bestIndex = i;
                        }
                    }

                    if (bestIndex < 0)
                        break;

                    plannedMasses[bestIndex] += flowUnit;
                    fractionalScaled[bestIndex] = -1;
                    remainder -= flowUnit;
                }
            }

            FlowTransferPlan transfer0 = default;
            FlowTransferPlan transfer1 = default;
            FlowTransferPlan transfer2 = default;
            FlowTransferPlan transfer3 = default;
            byte transferCount = 0;

            for (int i = 0; i < candidateCount; i++)
            {
                if (plannedMasses[i] <= 0)
                    continue;

                FlowTransferPlan plan = new FlowTransferPlan(candidateIndices[i], plannedMasses[i]);

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

        private void TryAddExchangeCandidate(
            int targetX,
            int targetY,
            byte sourceElementId,
            int sourceMass,
            int sourceMaxMass,
            int[] candidateIndices,
            int[] candidateMasses,
            int[] proposalMasses,
            ref int candidateCount,
            int flowUnit)
        {
            if (targetX < 0 || targetX >= _grid.Width)
                return;

            if (targetY < 0 || targetY >= _grid.Height)
                return;

            if (candidateCount >= 4)
                return;

            int targetIndex = _grid.ToIndex(targetX, targetY);
            SimCell targetCell = _grid.GetCellByIndex(targetIndex);

            if (!CanExchangeWith(sourceElementId, targetCell))
                return;

            int targetMass = targetCell.ElementId == BuiltInElementIds.Vacuum
                ? 0
                : targetCell.Mass;

            if (sourceMass <= targetMass)
                return;

            int targetCapacity = GetTargetCapacity(sourceElementId, sourceMaxMass, targetCell);
            if (targetCapacity <= 0)
                return;

            int diff = sourceMass - targetMass;
            if (diff < flowUnit * 2)
                return;

            int proposal = diff / 2;
            proposal = Math.Min(proposal, MAX_TRANSFER_PER_EDGE_PER_SUBSTEP);
            proposal = Math.Min(proposal, targetCapacity);
            proposal = QuantizeDown(proposal, flowUnit);

            if (proposal <= 0)
                return;

            candidateIndices[candidateCount] = targetIndex;
            candidateMasses[candidateCount] = targetMass;
            proposalMasses[candidateCount] = proposal;
            candidateCount++;
        }

        private static bool CanExchangeWith(byte sourceElementId, in SimCell targetCell)
        {
            if (targetCell.ElementId == BuiltInElementIds.Vacuum)
                return true;

            return targetCell.ElementId == sourceElementId;
        }

        private static int GetTargetCapacity(byte sourceElementId, int sourceMaxMass, in SimCell targetCell)
        {
            if (targetCell.ElementId == BuiltInElementIds.Vacuum)
                return sourceMaxMass;

            if (targetCell.ElementId == sourceElementId)
                return Math.Max(0, sourceMaxMass - targetCell.Mass);

            return 0;
        }

        private static int QuantizeDown(int value, int unit)
        {
            if (unit <= 1)
                return value;

            return value / unit * unit;
        }
    }
}