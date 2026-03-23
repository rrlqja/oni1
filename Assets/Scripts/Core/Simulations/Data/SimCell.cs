using System.Runtime.InteropServices;

namespace Core.Simulation.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SimCell
    {
        public byte ElementId;
        public byte Flags;
        private short _padding;     // 4바이트 정렬용
        public int Mass;
        public float Temperature;   // 켈빈 (K). 0K=절대영도, 273.15K=0℃

        public SimCell(byte elementId, int mass, float temperature = 0f,
                       SimCellFlags flags = SimCellFlags.None)
        {
            ElementId = elementId;
            Mass = mass;
            Temperature = temperature;
            Flags = (byte)flags;
            _padding = 0;
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

        public static SimCell Vacuum => new SimCell(0, 0, 0f);
    }
}