namespace Core.Simulation.Commands
{
    public readonly struct FlowTransferPlan
    {
        public readonly int TargetIndex;
        public readonly int PlannedMass;

        public FlowTransferPlan(int targetIndex, int plannedMass)
        {
            TargetIndex = targetIndex;
            PlannedMass = plannedMass;
        }

        public bool IsValid => TargetIndex >= 0 && PlannedMass > 0;
    }
}