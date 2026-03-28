namespace Core.Simulation.Definitions
{
    /// <summary>
    /// SimulationSettingsSO 래퍼.
    ///
    /// SO가 null이면 기본값(기존 const와 동일)을 반환한다.
    /// 테스트 코드에서 SO 없이 사용 가능.
    ///
    /// 사용법:
    ///   var settings = new SimulationSettings(settingsSO);  // 프로덕션
    ///   var settings = new SimulationSettings(null);        // 테스트 (기본값)
    /// </summary>
    public sealed class SimulationSettings
    {
        private readonly SimulationSettingsSO _so;

        public SimulationSettings(SimulationSettingsSO so)
        {
            _so = so;
        }

        // ── 투사체 ──
        public float Gravity => _so != null ? _so.gravity : 0.5f;
        public float MaxVelocity => _so != null ? _so.maxVelocity : 6f;
        public int ProjectileFallSpeedSolid => _so != null ? _so.projectileFallSpeedSolid : 2;
        public int ProjectileFallSpeedLiquid => _so != null ? _so.projectileFallSpeedLiquid : 2;

        // ── 온도 ──
        public float ConductivityScale => _so != null ? _so.conductivityScale : 0.1f;
        public float TransitionOvershoot => _so != null ? _so.transitionOvershoot : 3f;
        public float TransitionRebound => _so != null ? _so.transitionRebound : 1.5f;
        public float MinHeatExchange => _so != null ? _so.minHeatExchange : 0.001f;

        // ── 액체 ──
        public int MaxLateralTransfer => _so != null ? _so.maxLateralTransfer : 100_000;
        public int LiquidHorizontalInterval => _so != null ? _so.liquidHorizontalInterval : 3;

        // ── 기체 ──
        public int GasMovementInterval => _so != null ? _so.gasMovementInterval : 2;

        // ── 틱 ──
        public float DefaultTicksPerSecond => _so != null ? _so.defaultTicksPerSecond : 10f;
        public int MaxCatchupTicks => _so != null ? _so.maxCatchupTicks : 5;
    }
}