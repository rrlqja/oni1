using Core.Simulation.Data;

namespace Core.Simulation.Commands
{
    public enum SimulationCommandType : byte
    {
        None = 0,
        Move = 1,
        Swap = 2,
        MergeMass = 3,
        FlowBatch = 4,
        Transform = 5
    }

    public readonly struct SimulationCommand
    {
        public readonly SimulationCommandType Type;
        public readonly int FromIndex;
        public readonly int ToIndex;

        private SimulationCommand(
            SimulationCommandType type,
            int fromIndex,
            int toIndex)
        {
            Type = type;
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }

        public static SimulationCommand CreateMove(int fromIndex, int toIndex)
        {
            return new SimulationCommand(
                SimulationCommandType.Move,
                fromIndex,
                toIndex);
        }

        public static SimulationCommand CreateSwap(int fromIndex, int toIndex)
        {
            return new SimulationCommand(
                SimulationCommandType.Swap,
                fromIndex,
                toIndex);
        }

        public static SimulationCommand CreateMergeMass(int fromIndex, int toIndex)
        {
            return new SimulationCommand(
                SimulationCommandType.MergeMass,
                fromIndex,
                toIndex);
        }
    }
}