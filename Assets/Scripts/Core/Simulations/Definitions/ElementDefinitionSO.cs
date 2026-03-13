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
                lateralRetainMass: lateralRetainMass);
        }

#if UNITY_EDITOR
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