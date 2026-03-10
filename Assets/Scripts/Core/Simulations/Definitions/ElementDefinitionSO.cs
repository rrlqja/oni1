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

        [Header("Mass")]
        [Min(0)]
        [SerializeField] private int defaultMass = 0;

        [Min(0)]
        [SerializeField] private int maxMass = 0;

        [Header("Rendering")]
        [SerializeField] private Color32 baseColor = new Color32(255, 255, 255, 255);

        public byte Id => id;
        public string ElementName => elementName;
        public ElementBehaviorType BehaviorType => behaviorType;
        public DisplacementPriority DisplacementPriority => displacementPriority;
        public bool IsSolid => isSolid;
        public int DefaultMass => defaultMass;
        public int MaxMass => maxMass;
        public Color32 BaseColor => baseColor;

        public ElementRuntimeDefinition ToRuntimeDefinition()
        {
            return new ElementRuntimeDefinition(
                id: id,
                name: elementName,
                behaviorType: behaviorType,
                displacementPriority: displacementPriority,
                defaultMass: defaultMass,
                maxMass: maxMass,
                isSolid: isSolid,
                baseColor: baseColor);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(elementName))
                elementName = name;

            if (maxMass < defaultMass)
                maxMass = defaultMass;
        }
#endif
    }
}