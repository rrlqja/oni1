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
        public readonly float Density;
        public readonly int DefaultMass;
        public readonly int MaxMass;
        public readonly int Viscosity;
        public readonly int MinSpreadMass;
        public readonly bool IsSolid;
        public readonly Color32 BaseColor;
        public readonly int LateralRetainMass;

        public ElementRuntimeDefinition(
            byte id,
            string name,
            ElementBehaviorType behaviorType,
            DisplacementPriority displacementPriority,
            float density,
            int defaultMass,
            int maxMass,
            int viscosity,
            int minSpreadMass,
            bool isSolid,
            Color32 baseColor,
            int lateralRetainMass)
        {
            Id = id;
            Name = name;
            BehaviorType = behaviorType;
            DisplacementPriority = displacementPriority;
            Density = density;
            DefaultMass = defaultMass;
            MaxMass = maxMass;
            Viscosity = viscosity;
            MinSpreadMass = minSpreadMass;
            IsSolid = isSolid;
            BaseColor = baseColor;
            LateralRetainMass = lateralRetainMass;
        }
    }
}