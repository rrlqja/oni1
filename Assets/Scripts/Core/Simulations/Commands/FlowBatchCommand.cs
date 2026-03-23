using System;

namespace Core.Simulation.Commands
{
    public readonly struct FlowBatchCommand
    {
        public readonly int SourceIndex;
        public readonly byte ElementId;
        public readonly float SourceTemperature;
        public readonly FlowBatchMode Mode;
        public readonly byte TransferCount;

        public readonly FlowTransferPlan Transfer0;
        public readonly FlowTransferPlan Transfer1;
        public readonly FlowTransferPlan Transfer2;
        public readonly FlowTransferPlan Transfer3;

        public FlowBatchCommand(
            int sourceIndex,
            byte elementId,
            float sourceTemperature,
            FlowBatchMode mode,
            byte transferCount,
            FlowTransferPlan transfer0,
            FlowTransferPlan transfer1,
            FlowTransferPlan transfer2,
            FlowTransferPlan transfer3)
        {
            SourceIndex = sourceIndex;
            ElementId = elementId;
            SourceTemperature = sourceTemperature;
            Mode = mode;
            TransferCount = transferCount;
            Transfer0 = transfer0;
            Transfer1 = transfer1;
            Transfer2 = transfer2;
            Transfer3 = transfer3;
        }

        public FlowTransferPlan GetTransfer(int index)
        {
            return index switch
            {
                0 => Transfer0,
                1 => Transfer1,
                2 => Transfer2,
                3 => Transfer3,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }
    }
}