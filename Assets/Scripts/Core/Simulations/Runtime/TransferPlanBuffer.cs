using Core.Simulation.Commands;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// FlowBatchCommand 생성을 위한 고정 크기 버퍼.
    /// 최대 4방향 FlowTransferPlan을 힙 할당 없이 수집한 뒤
    /// ToBatch()로 FlowBatchCommand를 생성한다.
    /// </summary>
    public struct TransferPlanBuffer
    {
        public FlowTransferPlan T0;
        public FlowTransferPlan T1;
        public FlowTransferPlan T2;
        public FlowTransferPlan T3;
        public byte Count;

        public void Add(int targetIndex, int plannedMass)
        {
            switch (Count)
            {
                case 0: T0 = new FlowTransferPlan(targetIndex, plannedMass); break;
                case 1: T1 = new FlowTransferPlan(targetIndex, plannedMass); break;
                case 2: T2 = new FlowTransferPlan(targetIndex, plannedMass); break;
                case 3: T3 = new FlowTransferPlan(targetIndex, plannedMass); break;
                default: return; // 4개 초과 무시
            }
            Count++;
        }

        public FlowBatchCommand ToBatch(
            int sourceIndex,
            byte elementId,
            short temperature,
            FlowBatchMode mode)
        {
            return new FlowBatchCommand(
                sourceIndex,
                elementId,
                temperature,
                mode,
                Count,
                T0, T1, T2, T3);
        }
    }
}