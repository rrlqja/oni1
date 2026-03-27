using UnityEngine;
using Core.Simulation.Data;

namespace Core.Simulation.Definitions
{
    [CreateAssetMenu(
        fileName = "ElementDefinition",
        menuName = "Simulation/Element Definition")]
    public sealed class ElementDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private byte id;
        [SerializeField] private string elementName = "New Element";

        [Header("Behavior")]
        [SerializeField] private ElementBehaviorType behaviorType = ElementBehaviorType.Vacuum;
        [SerializeField] private DisplacementPriority displacementPriority = DisplacementPriority.Vacuum;
        [SerializeField] private bool isSolid = false;

        [Header("Physical")]
        [Min(0f)]
        [SerializeField] private float density = 0f;

        [Header("Mass")]
        [Min(0)]
        [SerializeField] private int defaultMass = 0;

        [Min(0)]
        [SerializeField] private int maxMass = 0;

        [Header("Flow")]
        [Min(1)]
        [SerializeField] private int viscosity = 1;

        [Min(0)]
        [SerializeField] private int minSpreadMass = 0;

        [Header("Rendering")]
        [SerializeField] private Color32 baseColor = new Color32(255, 255, 255, 255);

        [Header("Liquid Flow")]
        [Min(0)]
        [SerializeField] private int lateralRetainMass = 0;

        [Header("Thermal Properties")]
        [Tooltip("열 전도율 (W/m·K). 높을수록 열이 빨리 전달됨.")]
        [Min(0f)]
        [SerializeField] private float thermalConductivity = 1f;

        [Tooltip("비열 용량 (DTU/g·K). 높을수록 온도가 잘 안 변함.")]
        [Min(0.01f)]
        [SerializeField] private float specificHeatCapacity = 1f;

        [Tooltip("생성 시 기본 온도 (K).")]
        [SerializeField] private float defaultTemperature = 293.15f;

        [Header("Phase Transition — Heating")]
        [Tooltip("이 온도(K) 이상 + 오버슈트에서 변환. 0이면 없음.")]
        [SerializeField] private float highTransitionTemp = 0f;

        [Tooltip("가열 시 변환 대상 원소.")]
        [SerializeField] private ElementDefinitionSO highTransitionTarget;

        [Tooltip("가열 변환 시 부산물 원소 (선택).")]
        [SerializeField] private ElementDefinitionSO highTransitionOre;

        [Range(0f, 1f)]
        [SerializeField] private float highTransitionOreMassRatio = 0f;

        [Header("Phase Transition — Cooling")]
        [Tooltip("이 온도(K) 이하 - 오버슈트에서 변환. 0이면 없음.")]
        [SerializeField] private float lowTransitionTemp = 0f;

        [Tooltip("냉각 시 변환 대상 원소.")]
        [SerializeField] private ElementDefinitionSO lowTransitionTarget;

        [Tooltip("냉각 변환 시 부산물 원소 (선택).")]
        [SerializeField] private ElementDefinitionSO lowTransitionOre;

        [Range(0f, 1f)]
        [SerializeField] private float lowTransitionOreMassRatio = 0f;

        // ── 원소별 렌더링 파라미터 (신규) ──

        [Header("Liquid Rendering")]
        [Tooltip("패턴 텍스처 강도. 0=밋밋, 0.5+=질감 뚜렷. Water=0.12, Oil=0.4, Magma=0.7")]
        [Range(0f, 1f)]
        [SerializeField] private float liquidPatternStrength = 0.15f;

        [Tooltip("흐름 속도. Water=0.15, Oil=0.04, Magma=0.1")]
        [Range(0f, 1f)]
        [SerializeField] private float liquidFlowSpeed = 0.15f;

        [Tooltip("패턴 스케일. 작을수록 굵직, 클수록 세밀. Water=1.2, Oil=0.8")]
        [Range(0.3f, 4f)]
        [SerializeField] private float liquidPatternScale = 1.0f;

        [Tooltip("표면 하이라이트 강도. Water=0.25, Oil=0.05")]
        [Range(0f, 0.5f)]
        [SerializeField] private float liquidSurfaceHighlight = 0.15f;

        [Tooltip("점성 블러 느낌. Water=0, Oil=0.6, Magma=1.0")]
        [Range(0f, 1f)]
        [SerializeField] private float liquidViscosity = 0f;

        [Header("Gas Rendering")]
        [Tooltip("구름 스케일. 클수록 큰 구름. Oxygen=2, Hydrogen=3, CO2=1")]
        [Range(0.5f, 5f)]
        [SerializeField] private float gasCloudScale = 2.0f;

        [Tooltip("구름 대비. 높으면 덩어리 뚜렷. Hydrogen=0.8, CO2=2.0")]
        [Range(0.5f, 3f)]
        [SerializeField] private float gasCloudContrast = 1.5f;

        [Tooltip("구름 드리프트 속도. Hydrogen=0.15(빠름), CO2=0.03(느림)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float gasDriftSpeed = 0.05f;

        [Tooltip("경계 부드러움. Hydrogen=2.5(흐릿), CO2=0.3(선명)")]
        [Range(0f, 3f)]
        [SerializeField] private float gasEdgeSoftness = 0.8f;

        [Header("Solid Rendering")]
        [Tooltip("고체 전용 텍스처. 월드UV로 연속 타일링됨. null이면 BaseColor × 공유 마스크.")]
        [SerializeField] private Texture2D solidTexture;

        [Tooltip("텍스처 스케일. 1.0=셀당 텍스처 1회, 0.25=4셀에 1회 (큰 패턴)")]
        [Range(0.1f, 2f)]
        [SerializeField] private float solidTextureScale = 0.25f;

        [Tooltip("공유 균열 마스크 강도. 0=균열 없음, 1=최대")]
        [Range(0f, 1f)]
        [SerializeField] private float solidCrackStrength = 0.3f;

        // ── 프로퍼티 ──

        public int LateralRetainMass => lateralRetainMass;

        public byte Id => id;
        public string ElementName => elementName;
        public ElementBehaviorType BehaviorType => behaviorType;
        public DisplacementPriority DisplacementPriority => displacementPriority;
        public bool IsSolid => isSolid;
        public float Density => density;
        public int DefaultMass => defaultMass;
        public int MaxMass => maxMass;
        public int Viscosity => viscosity;
        public int MinSpreadMass => minSpreadMass;
        public Color32 BaseColor => baseColor;

        public float LiquidPatternStrength => liquidPatternStrength;
        public float LiquidFlowSpeed => liquidFlowSpeed;
        public float LiquidPatternScale => liquidPatternScale;
        public float LiquidSurfaceHighlight => liquidSurfaceHighlight;
        public float LiquidViscosity => liquidViscosity;
        public float GasCloudScale => gasCloudScale;
        public float GasCloudContrast => gasCloudContrast;
        public float GasDriftSpeed => gasDriftSpeed;
        public float GasEdgeSoftness => gasEdgeSoftness;
        public Texture2D SolidTexture => solidTexture;
        public float SolidTextureScale => solidTextureScale;
        public float SolidCrackStrength => solidCrackStrength;

        public ElementRuntimeDefinition ToRuntimeDefinition()
        {
            return new ElementRuntimeDefinition(
                id: id,
                name: elementName,
                behaviorType: behaviorType,
                displacementPriority: displacementPriority,
                density: density,
                defaultMass: defaultMass,
                maxMass: maxMass,
                viscosity: viscosity,
                minSpreadMass: minSpreadMass,
                isSolid: isSolid,
                baseColor: baseColor,
                lateralRetainMass: lateralRetainMass,
                thermalConductivity: thermalConductivity,
                specificHeatCapacity: specificHeatCapacity,
                defaultTemperature: defaultTemperature,
                highTransitionTemp: highTransitionTemp,
                highTransitionTargetId: highTransitionTarget != null ? highTransitionTarget.Id : (byte)0,
                highTransitionOreId: highTransitionOre != null ? highTransitionOre.Id : (byte)0,
                highTransitionOreMassRatio: highTransitionOreMassRatio,
                lowTransitionTemp: lowTransitionTemp,
                lowTransitionTargetId: lowTransitionTarget != null ? lowTransitionTarget.Id : (byte)0,
                lowTransitionOreId: lowTransitionOre != null ? lowTransitionOre.Id : (byte)0,
                lowTransitionOreMassRatio: lowTransitionOreMassRatio,
                liquidPatternStrength: liquidPatternStrength,
                liquidFlowSpeed: liquidFlowSpeed,
                liquidPatternScale: liquidPatternScale,
                liquidSurfaceHighlight: liquidSurfaceHighlight,
                liquidViscosity: liquidViscosity,
                gasCloudScale: gasCloudScale,
                gasCloudContrast: gasCloudContrast,
                gasDriftSpeed: gasDriftSpeed,
                gasEdgeSoftness: gasEdgeSoftness);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        public void SetValuesForTests(
            byte id,
            string elementName,
            ElementBehaviorType behaviorType,
            DisplacementPriority displacementPriority,
            float density,
            int defaultMass,
            int maxMass,
            int viscosity,
            int minSpreadMass,
            bool isSolid,
            Color32 baseColor)
        {
            this.id = id;
            this.elementName = elementName;
            this.behaviorType = behaviorType;
            this.displacementPriority = displacementPriority;
            this.density = density;
            this.defaultMass = defaultMass;
            this.maxMass = maxMass;
            this.viscosity = viscosity;
            this.minSpreadMass = minSpreadMass;
            this.isSolid = isSolid;
            this.baseColor = baseColor;
            this.thermalConductivity = 1f;
            this.specificHeatCapacity = 1f;
            this.defaultTemperature = 293.15f;
            this.highTransitionTemp = 0f;
            this.highTransitionTarget = null;
            this.highTransitionOre = null;
            this.highTransitionOreMassRatio = 0f;
            this.lowTransitionTemp = 0f;
            this.lowTransitionTarget = null;
            this.lowTransitionOre = null;
            this.lowTransitionOreMassRatio = 0f;
            // 렌더링 파라미터 기본값
            this.liquidPatternStrength = 0.15f;
            this.liquidFlowSpeed = 0.15f;
            this.liquidPatternScale = 1.0f;
            this.liquidSurfaceHighlight = 0.15f;
            this.liquidViscosity = 0f;
            this.gasCloudScale = 2.0f;
            this.gasCloudContrast = 1.5f;
            this.gasDriftSpeed = 0.05f;
            this.gasEdgeSoftness = 0.8f;

            this.solidTexture = null;
            this.solidTextureScale = 0.25f;
            this.solidCrackStrength = 0.3f;
        }

        public void SetThermalValuesForTests(
            float thermalConductivity,
            float specificHeatCapacity,
            float defaultTemperature = 293.15f)
        {
            this.thermalConductivity = thermalConductivity;
            this.specificHeatCapacity = specificHeatCapacity;
            this.defaultTemperature = defaultTemperature;
        }

        public void SetTransitionValuesForTests(
            float highTransitionTemp = 0f,
            ElementDefinitionSO highTransitionTarget = null,
            ElementDefinitionSO highTransitionOre = null,
            float highTransitionOreMassRatio = 0f,
            float lowTransitionTemp = 0f,
            ElementDefinitionSO lowTransitionTarget = null,
            ElementDefinitionSO lowTransitionOre = null,
            float lowTransitionOreMassRatio = 0f)
        {
            this.highTransitionTemp = highTransitionTemp;
            this.highTransitionTarget = highTransitionTarget;
            this.highTransitionOre = highTransitionOre;
            this.highTransitionOreMassRatio = highTransitionOreMassRatio;
            this.lowTransitionTemp = lowTransitionTemp;
            this.lowTransitionTarget = lowTransitionTarget;
            this.lowTransitionOre = lowTransitionOre;
            this.lowTransitionOreMassRatio = lowTransitionOreMassRatio;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(elementName))
                elementName = name;

            if (density < 0f)
                density = 0f;

            if (viscosity < 1)
                viscosity = 1;

            if (minSpreadMass < 0)
                minSpreadMass = 0;

            if (maxMass < defaultMass)
                maxMass = defaultMass;
        }
#endif
    }
}
