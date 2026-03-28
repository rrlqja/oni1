using UnityEngine;

namespace Core.Simulation.Runtime.WorldGeneration
{
    /// <summary>
    /// 맵 생성 프로파일.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldGenProfile",
        menuName = "Simulation/World Gen/World Gen Profile")]
    public sealed class WorldGenProfileSO : ScriptableObject
    {
        [Header("Seed")]
        [Tooltip("맵 생성 시드. 동일 시드 → 동일 맵.")]
        [SerializeField] private int seed = 42;

        [Tooltip("true이면 매번 랜덤 시드 사용.")]
        [SerializeField] private bool randomizeSeed = false;

        [Header("Biome Configuration")]
        [Tooltip("스폰 바이옴 (맵 중앙에 고정 배치).")]
        [SerializeField] private BiomeSO spawnBiome;

        [Tooltip("스폰 외 배치 가능한 바이옴 후보 목록.")]
        [SerializeField] private BiomeSO[] availableBiomes = new BiomeSO[0];

        [Header("Voronoi Layout")]
        [Tooltip("바이옴 시드 포인트 수 (스폰 포함).")]
        [Range(3, 200)]
        [SerializeField] private int biomePointCount = 80;

        [Tooltip("Lloyd's Relaxation 반복 횟수. 높을수록 바이옴 크기가 균일. 3~5 권장.")]
        [Range(0, 10)]
        [SerializeField] private int relaxationIterations = 4;

        [Tooltip("Voronoi 경계 노이즈 강도. 0=직선 경계, 높을수록 유기적.")]
        [Range(0f, 30f)]
        [SerializeField] private float boundaryNoise = 8f;

        [Tooltip("경계 노이즈 스케일. 작을수록 큰 곡선, 클수록 잔 흔들림.")]
        [Range(0.01f, 0.2f)]
        [SerializeField] private float boundaryNoiseScale = 0.06f;

        [Header("Terrain")]
        [Tooltip("Bedrock 테두리 두께 (셀).")]
        [Range(1, 5)]
        [SerializeField] private int borderThickness = 1;

        // ── Properties ──
        public int Seed => randomizeSeed ? Random.Range(int.MinValue, int.MaxValue) : seed;
        public BiomeSO SpawnBiome => spawnBiome;
        public BiomeSO[] AvailableBiomes => availableBiomes;
        public int BiomePointCount => biomePointCount;
        public int RelaxationIterations => relaxationIterations;
        public float BoundaryNoise => boundaryNoise;
        public float BoundaryNoiseScale => boundaryNoiseScale;
        public int BorderThickness => borderThickness;
    }
}