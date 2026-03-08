using UnityEngine;

namespace Core.Simulation.Data
{
    public enum ElementBehaviorType : byte
    {
        Vacuum = 0,
        StaticSolid = 1,
        FallingSolid = 2,
        Liquid = 3,
        Gas = 4
    }

    public enum DisplacementPriority : byte
    {
        Vacuum = 0,
        Gas = 1,
        Liquid = 2,
        FallingSolid = 3,
        StaticSolid = 4
    }

    [System.Flags]
    public enum SimCellFlags : byte
    {
        None = 0,
        Dirty = 1 << 0
    }

    [System.Flags]
    public enum TickReservationMask : byte
    {
        None = 0,
        SourceReserved = 1 << 0,
        TargetReserved = 1 << 1,
        DisplacedReserved = 1 << 2
    }
}