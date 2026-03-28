namespace Core.Simulation.Definitions
{
    public static class BuiltInElementIds
    {
        // ── 시스템 ──
        public const byte Vacuum = 0;
        public const byte Bedrock = 1;

        // ── 고체 (StaticSolid) ──
        public const byte Granite = 2;
        public const byte Dirt = 3;
        public const byte Ice = 5;

        // ── 고체 (FallingSolid) ──
        public const byte Sand = 4;

        // ── 액체 ──
        public const byte Water = 6;
        public const byte Oil = 7;
        public const byte Brine = 8;
        public const byte Magma = 9;

        // ── 기체 ──
        public const byte Oxygen = 10;
        public const byte Hydrogen = 11;
        public const byte CarbonDioxide = 12;
        public const byte Steam = 13;
    }
}