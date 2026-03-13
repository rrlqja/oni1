using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    public sealed class FlowBatchApplier
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        private readonly int[] _flowOutgoingMass;
        private readonly int[] _flowIncomingMass;
        private readonly long[] _flowIncomingThermal;
        private readonly byte[] _flowIncomingElementId;
        private readonly bool[] _flowTouched;
        private readonly List<int> _flowTouchedIndices = new(256);

        public FlowBatchApplier(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            _flowOutgoingMass = new int[_grid.Length];
            _flowIncomingMass = new int[_grid.Length];
            _flowIncomingThermal = new long[_grid.Length];
            _flowIncomingElementId = new byte[_grid.Length];
            _flowTouched = new bool[_grid.Length];
        }

        public void Apply(IReadOnlyList<FlowBatchCommand> flowCommands)
        {
            if (flowCommands == null)
                throw new ArgumentNullException(nameof(flowCommands));

            if (flowCommands.Count == 0)
                return;

            for (int i = 0; i < flowCommands.Count; i++)
            {
                FlowBatchCommand batch = flowCommands[i];
                ApplyBatch(batch);
            }

            ApplyTouchedFlowCells();
            ClearFlowAccumulators();
        }

        private void ApplyBatch(in FlowBatchCommand batch)
        {
            SimCell sourceSnapshot = _grid.GetCellByIndex(batch.SourceIndex);

            for (int t = 0; t < batch.TransferCount; t++)
            {
                FlowTransferPlan transfer = batch.GetTransfer(t);
                if (!transfer.IsValid)
                    continue;

                int targetIndex = transfer.TargetIndex;
                SimCell targetSnapshot = _grid.GetCellRef(targetIndex);

                int targetCurrentMass = targetSnapshot.Mass + _flowIncomingMass[targetIndex];
                int targetCapacity = Math.Max(0, _registry.Get(batch.ElementId).MaxMass - targetCurrentMass);

                int acceptedMass = Math.Min(transfer.PlannedMass, targetCapacity);
                if (acceptedMass <= 0)
                    continue;

                _flowOutgoingMass[batch.SourceIndex] += acceptedMass;
                _flowIncomingMass[targetIndex] += acceptedMass;
                _flowIncomingThermal[targetIndex] += (long)batch.SourceTemperature * acceptedMass;

                if (_flowIncomingElementId[targetIndex] == 0)
                    _flowIncomingElementId[targetIndex] = batch.ElementId;

                MarkFlowTouched(batch.SourceIndex);
                MarkFlowTouched(targetIndex);
            }
        }

        private void MarkFlowTouched(int index)
        {
            if (_flowTouched[index])
                return;

            _flowTouched[index] = true;
            _flowTouchedIndices.Add(index);
        }

        private void ApplyTouchedFlowCells()
        {
            ref readonly ElementRuntimeDefinition vacuum = ref _registry.Get(BuiltInElementIds.Vacuum);
            SimCell vacuumCell = new SimCell(
                elementId: vacuum.Id,
                mass: vacuum.DefaultMass,
                temperature: 0,
                flags: SimCellFlags.None);

            for (int i = 0; i < _flowTouchedIndices.Count; i++)
            {
                int index = _flowTouchedIndices[i];

                SimCell snapshot = _grid.GetCellByIndex(index);

                int outgoing = _flowOutgoingMass[index];
                int incoming = _flowIncomingMass[index];

                int retainedMass = snapshot.Mass - outgoing;
                int finalMass = retainedMass + incoming;

                if (finalMass <= 0)
                {
                    _grid.GetCellRef(index) = vacuumCell;
                    continue;
                }

                SimCell updated = snapshot;

                if (snapshot.ElementId == BuiltInElementIds.Vacuum && incoming > 0)
                {
                    updated.ElementId = _flowIncomingElementId[index];
                    updated.Flags = (byte)SimCellFlags.None;
                }

                updated.Mass = finalMass;

                if (incoming > 0)
                {
                    long retainedThermal = retainedMass > 0
                        ? (long)snapshot.Temperature * retainedMass
                        : 0L;

                    long totalThermal = retainedThermal + _flowIncomingThermal[index];
                    updated.Temperature = (short)(totalThermal / finalMass);
                }

                _grid.GetCellRef(index) = updated;
            }
        }

        private void ClearFlowAccumulators()
        {
            for (int i = 0; i < _flowTouchedIndices.Count; i++)
            {
                int index = _flowTouchedIndices[i];
                _flowOutgoingMass[index] = 0;
                _flowIncomingMass[index] = 0;
                _flowIncomingThermal[index] = 0;
                _flowIncomingElementId[index] = 0;
                _flowTouched[index] = false;
            }

            _flowTouchedIndices.Clear();
        }
    }
}