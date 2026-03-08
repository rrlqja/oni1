using System.Runtime.InteropServices;

namespace Core.Simulation.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TickMeta
    {
        public int LastActedTick;
        public int ReservedByCommandId;
        public byte ReservationMask;
        public byte Padding0;
        public short Padding1;

        public bool HasReservation(TickReservationMask mask)
        {
            return (((TickReservationMask)ReservationMask) & mask) != 0;
        }

        public void AddReservation(TickReservationMask mask)
        {
            ReservationMask = (byte)(((TickReservationMask)ReservationMask) | mask);
        }

        public void ClearReservations()
        {
            ReservationMask = 0;
            ReservedByCommandId = -1;
        }

        public bool HasActedThisTick(int tick)
        {
            return LastActedTick == tick;
        }

        public void MarkActed(int tick)
        {
            LastActedTick = tick;
        }

        public static TickMeta CreateDefault()
        {
            return new TickMeta
            {
                LastActedTick = -1,
                ReservedByCommandId = -1,
                ReservationMask = 0
            };
        }
    }
}