using System.Collections.Generic;

namespace Core.Simulation.Runtime
{
    public readonly struct AppliedFlowTransfer
    {
        public readonly int SourceIndex;
        public readonly int TargetIndex;
        public readonly byte ElementId;
        public readonly int AcceptedMass;

        public AppliedFlowTransfer(int sourceIndex, int targetIndex, byte elementId, int acceptedMass)
        {
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            ElementId = elementId;
            AcceptedMass = acceptedMass;
        }
    }

    public sealed class FlowBatchApplyReport
    {
        private readonly List<AppliedFlowTransfer> _transfers = new(256);

        public IReadOnlyList<AppliedFlowTransfer> Transfers => _transfers;

        public void Clear()
        {
            _transfers.Clear();
        }

        public void Add(int sourceIndex, int targetIndex, byte elementId, int acceptedMass)
        {
            if (acceptedMass <= 0)
                return;

            _transfers.Add(new AppliedFlowTransfer(
                sourceIndex,
                targetIndex,
                elementId,
                acceptedMass));
        }
    }
}