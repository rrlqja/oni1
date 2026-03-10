namespace Core.Simulation.Commands
{
    public enum SimulationCommandType : byte
    {
        None = 0,
        Move = 1,
        Replace = 2
    }

    public readonly struct SimulationCommand
    {
        public readonly SimulationCommandType Type;
        public readonly int FromIndex;
        public readonly int ToIndex;
        public readonly int DisplacedToIndex;

        private SimulationCommand(
            SimulationCommandType type,
            int fromIndex,
            int toIndex,
            int displacedToIndex)
        {
            Type = type;
            FromIndex = fromIndex;
            ToIndex = toIndex;
            DisplacedToIndex = displacedToIndex;
        }

        public static SimulationCommand CreateMove(int fromIndex, int toIndex)
        {
            return new SimulationCommand(
                SimulationCommandType.Move,
                fromIndex,
                toIndex,
                -1);
        }

        public static SimulationCommand CreateReplace(int fromIndex, int toIndex, int displacedToIndex)
        {
            return new SimulationCommand(
                SimulationCommandType.Replace,
                fromIndex,
                toIndex,
                displacedToIndex);
        }
    }
}