using UnityEngine;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime.WorldGeneration
{
    /// <summary>
    /// 바이옴 정의.
    ///
    /// 각 바이옴의 원소 분포, 동굴 밀도, 온도 등을 정의한다.
    /// WorldGenProfileSO에서 참조하여 맵 생성에 사용.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Biome",
        menuName = "Simulation/World Gen/Biome")]
    public sealed class BiomeSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string biomeName = "New Biome";
        [SerializeField] private Color backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Element Distribution")]
        [Tooltip("이 바이옴을 구성하는 원소 레이어들. 위에서부터 순서대로 적용.")]
        [SerializeField] private BiomeElementLayer[] elementLayers = new BiomeElementLayer[0];

        [Header("Cave Generation (Step 3)")]
        [Tooltip("동굴 밀도. 0=동굴 없음, 1=거의 전부 동굴. 0.15~0.3 권장.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float caveDensity = 0.2f;

        [Tooltip("동굴 노이즈 스케일. 작을수록 큰 동굴, 클수록 잔 동굴.")]
        [Range(0.02f, 0.2f)]
        [SerializeField] private float caveScale = 0.08f;

        [Tooltip("동굴 빈 공간을 채울 기체 원소. null이면 Vacuum.")]
        [SerializeField] private ElementDefinitionSO caveGasElement;

        [Header("Temperature (Step 4)")]
        [Tooltip("바이옴 기본 온도 (K).")]
        [SerializeField] private float baseTemperature = 293.15f;

        [Tooltip("온도 변동 범위 (K). 셀마다 ±이 값만큼 랜덤 변동.")]
        [Range(0f, 50f)]
        [SerializeField] private float temperatureVariation = 5f;

        // ── Properties ──
        public string BiomeName => biomeName;
        public Color BackgroundColor => backgroundColor;
        public BiomeElementLayer[] ElementLayers => elementLayers;
        public float CaveDensity => caveDensity;
        public float CaveScale => caveScale;
        public ElementDefinitionSO CaveGasElement => caveGasElement;
        public float BaseTemperature => baseTemperature;
        public float TemperatureVariation => temperatureVariation;
    }

    /// <summary>
    /// 바이옴 내 원소 레이어 정의.
    ///
    /// 예: 온대 바이옴 = [Dirt 30%, Granite 50%, Sand 20%]
    /// weight는 상대 비율 — 합이 100일 필요 없음.
    /// </summary>
    [System.Serializable]
    public struct BiomeElementLayer
    {
        [Tooltip("이 레이어의 원소.")]
        public ElementDefinitionSO element;

        [Tooltip("상대적 비율 가중치. 다른 레이어와의 상대 비율로 적용.")]
        [Range(0f, 100f)]
        public float weight;

        [Tooltip("최소 높이 비율 (0=바닥, 1=꼭대기). 이 높이 이상에서만 배치.")]
        [Range(0f, 1f)]
        public float minHeightRatio;

        [Tooltip("최대 높이 비율. 이 높이 이하에서만 배치.")]
        [Range(0f, 1f)]
        public float maxHeightRatio;

        public BiomeElementLayer(ElementDefinitionSO element, float weight,
            float minHeight = 0f, float maxHeight = 1f)
        {
            this.element = element;
            this.weight = weight;
            this.minHeightRatio = minHeight;
            this.maxHeightRatio = maxHeight;
        }
    }
}