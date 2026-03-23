using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// FlowBatchCommand 일괄 적용기.
    ///
    /// [개선 2] MaxMass 제한 없이 질량 수용 — 계획 단계(Processor)에서
    /// deficit 기반으로 이미 올바른 분배를 보장하므로, 적용 단계에서는
    /// 계획된 질량을 그대로 수용한다.
    /// </summary>
    public sealed class FlowBatchApplier
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        private readonly int[] _flowOutgoingMass;
        private readonly int[] _flowIncomingMass;
        private readonly float[] _flowIncomingThermal;
        private readonly byte[] _flowIncomingElementId;
        private readonly bool[] _flowTouched;
        private readonly List<int> _flowTouchedIndices = new(256);

        public FlowBatchApplier(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            _flowOutgoingMass = new int[_grid.Length];
            _flowIncomingMass = new int[_grid.Length];
            _flowIncomingThermal = new float[_grid.Length];
            _flowIncomingElementId = new byte[_grid.Length];
            _flowTouched = new bool[_grid.Length];
        }

        public void Apply(IReadOnlyList<FlowBatchCommand> flowCommands, FlowBatchApplyReport report = null)
        {
            if (flowCommands == null)
                throw new ArgumentNullException(nameof(flowCommands));

            report?.Clear();

            if (flowCommands.Count == 0)
                return;

            for (int i = 0; i < flowCommands.Count; i++)
            {
                FlowBatchCommand batch = flowCommands[i];
                ApplyBatch(batch, report);
            }

            ApplyTouchedFlowCells();
            ClearFlowAccumulators();
        }

        private void ApplyBatch(in FlowBatchCommand batch, FlowBatchApplyReport report)
        {
            for (int t = 0; t < batch.TransferCount; t++)
            {
                FlowTransferPlan transfer = batch.GetTransfer(t);
                if (!transfer.IsValid)
                    continue;

                int targetIndex = transfer.TargetIndex;
                SimCell targetSnapshot = _grid.GetCellRef(targetIndex);

                // 원소 타입 체크: 타겟이 진공이거나 같은 원소여야 수용
                if (targetSnapshot.ElementId != BuiltInElementIds.Vacuum &&
                    targetSnapshot.ElementId != batch.ElementId)
                    continue;

                // [개선 2] MaxMass 캡 제거 — 계획된 질량을 그대로 수용
                // 계획 단계(Processor)에서 deficit 기반으로 이미 올바른 분배를 보장.
                int acceptedMass = transfer.PlannedMass;

                if (acceptedMass <= 0)
                    continue;

                _flowOutgoingMass[batch.SourceIndex] += acceptedMass;
                _flowIncomingMass[targetIndex] += acceptedMass;
                _flowIncomingThermal[targetIndex] += batch.SourceTemperature * acceptedMass;

                if (_flowIncomingElementId[targetIndex] == 0)
                    _flowIncomingElementId[targetIndex] = batch.ElementId;

                report?.Add(batch.SourceIndex, targetIndex, batch.ElementId, acceptedMass);

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
                temperature: 0f,
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
                    float retainedThermal = retainedMass > 0
                        ? snapshot.Temperature * retainedMass
                        : 0f;
                    float totalThermal = retainedThermal + _flowIncomingThermal[index];
                    updated.Temperature = totalThermal / finalMass;
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
                _flowIncomingThermal[index] = 0f;
                _flowIncomingElementId[index] = 0;
                _flowTouched[index] = false;
            }
            _flowTouchedIndices.Clear();
        }
    }
}