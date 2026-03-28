using UnityEngine;

namespace Core.Simulation.Definitions
{
    /// <summary>
    /// 시뮬레이션 전역 튜닝 파라미터.
    ///
    /// Inspector에서 실시간 조절 가능. 재컴파일 불필요.
    /// SimulationWorld → SimulationRunner → 각 프로세서로 전달된다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SimulationSettings",
        menuName = "Simulation/Simulation Settings")]
    public sealed class SimulationSettingsSO : ScriptableObject
    {
        // ================================================================
        //  투사체 (FallingEntityManager)
        // ================================================================

        [Header("Projectile")]

        [Tooltip("투사체 중력 가속도 (셀/틱²). 매 틱 velocity에 더해진다.")]
        [Range(0.1f, 2f)]
        public float gravity = 0.5f;

        [Tooltip("투사체 최대 낙하 속도 (셀/틱). 터미널 벨로시티.")]
        [Range(1f, 20f)]
        public float maxVelocity = 6f;

        [Tooltip("FallingSolid 투사체 초기 낙하 속도 (셀/틱).")]
        [Range(1, 5)]
        public int projectileFallSpeedSolid = 2;

        [Tooltip("Liquid 투사체 초기 낙하 속도 (셀/틱).")]
        [Range(1, 5)]
        public int projectileFallSpeedLiquid = 2;

        // ================================================================
        //  온도 (TemperatureProcessor, StateTransitionProcessor)
        // ================================================================

        [Header("Temperature")]

        [Tooltip("열전도 스케일. 1.0이면 고전도 물질에서 즉각 평형. 낮을수록 느림.")]
        [Range(0.01f, 1f)]
        public float conductivityScale = 0.1f;

        [Tooltip("상태변환 오버슈트 (K). 전환점 ± 이 값을 넘어야 전환 발생.")]
        [Range(0.5f, 10f)]
        public float transitionOvershoot = 3f;

        [Tooltip("상태변환 리바운드 (K). 전환 후 전환점 방향으로 이만큼 온도 복귀.")]
        [Range(0.5f, 5f)]
        public float transitionRebound = 1.5f;

        [Tooltip("최소 열교환량 (K). 이 이하면 교환 스킵 (성능).")]
        [Range(0.0001f, 0.01f)]
        public float minHeatExchange = 0.001f;

        // ================================================================
        //  액체 (LiquidFlowProcessor, LiquidDensityProcessor)
        // ================================================================

        [Header("Liquid")]

        [Tooltip("한 방향 좌우 확산 시 최대 전달량 (mg/틱).")]
        [Range(10_000, 500_000)]
        public int maxLateralTransfer = 100_000;

        [Tooltip("수평 밀도 교환 주기 (N틱마다 1번). 클수록 수평 혼합 느림.")]
        [Range(1, 10)]
        public int liquidHorizontalInterval = 3;

        // ================================================================
        //  기체 (GasFlowPlanner)
        // ================================================================

        [Header("Gas")]

        [Tooltip("기체 밀도 이동 주기 (N틱마다 1번). 클수록 기체가 느리게 분리.")]
        [Range(1, 10)]
        public int gasMovementInterval = 2;

        // ================================================================
        //  틱 (SimulationWorld)
        // ================================================================

        [Header("Tick")]

        [Tooltip("기본 틱 속도 (TPS). SimulationWorld.ticksPerSecond 초기값.")]
        [Range(1f, 60f)]
        public float defaultTicksPerSecond = 10f;

        [Tooltip("프레임 드롭 시 최대 catch-up 틱 수.")]
        [Range(1, 20)]
        public int maxCatchupTicks = 5;
    }
}