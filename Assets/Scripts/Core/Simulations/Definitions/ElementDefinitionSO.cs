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
                // 새 필드
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
                lowTransitionOreMassRatio: lowTransitionOreMassRatio);
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
            // 기존 시그니처 끝에 추가
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