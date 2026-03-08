using System.Runtime.InteropServices;

namespace Core.Simulation.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SimCell
    {
        public byte ElementId;
        public byte Flags;
        public short Temperature;
        public int Mass;

        public SimCell(byte elementId, int mass, short temperature = 0, SimCellFlags flags = SimCellFlags.None)
        {
            ElementId = elementId;
            Mass = mass;
            Temperature = temperature;
            Flags = (byte)flags;
        }

        public bool HasFlag(SimCellFlags flag)
        {
            return (((SimCellFlags)Flags) & flag) != 0;
        }

        public void SetFlag(SimCellFlags flag)
        {
            Flags = (byte)(((SimCellFlags)Flags) | flag);
        }

        public void ClearFlag(SimCellFlags flag)
        {
            Flags = (byte)(((SimCellFlags)Flags) & ~flag);
        }

        public static SimCell Vacuum => new SimCell(0, 0);
    }
}