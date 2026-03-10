using UnityEngine;
using Core.Simulation.Data;

namespace Core.Simulation.Definitions
{
    public readonly struct ElementRuntimeDefinition
    {
        public readonly byte Id;
        public readonly string Name;
        public readonly ElementBehaviorType BehaviorType;
        public readonly DisplacementPriority DisplacementPriority;
        public readonly int DefaultMass;
        public readonly int MaxMass;
        public readonly bool IsSolid;
        public readonly Color32 BaseColor;

        public ElementRuntimeDefinition(
            byte id,
            string name,
            ElementBehaviorType behaviorType,
            DisplacementPriority displacementPriority,
            int defaultMass,
            int maxMass,
            bool isSolid,
            Color32 baseColor)
        {
            Id = id;
            Name = name;
            BehaviorType = behaviorType;
            DisplacementPriority = displacementPriority;
            DefaultMass = defaultMass;
            MaxMass = maxMass;
            IsSolid = isSolid;
            BaseColor = baseColor;
        }
    }
}