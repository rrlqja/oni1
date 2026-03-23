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

        // ── 열 속성 (추가) ──
        public readonly float ThermalConductivity;
        public readonly float SpecificHeatCapacity;
        public readonly float DefaultTemperature;

        // ── 가열 상태변환 (추가) ──
        public readonly float HighTransitionTemp;
        public readonly byte HighTransitionTargetId;
        public readonly byte HighTransitionOreId;
        public readonly float HighTransitionOreMassRatio;

        // ── 냉각 상태변환 (추가) ──
        public readonly float LowTransitionTemp;
        public readonly byte LowTransitionTargetId;
        public readonly byte LowTransitionOreId;
        public readonly float LowTransitionOreMassRatio;

        public ElementRuntimeDefinition(
            byte id, string name,
            ElementBehaviorType behaviorType,
            DisplacementPriority displacementPriority,
            float density, int defaultMass, int maxMass,
            int viscosity, int minSpreadMass,
            bool isSolid, Color32 baseColor,
            int lateralRetainMass,
            // 새 파라미터 (기본값으로 하위호환)
            float thermalConductivity = 1f,
            float specificHeatCapacity = 1f,
            float defaultTemperature = 293.15f,
            float highTransitionTemp = 0f,
            byte highTransitionTargetId = 0,
            byte highTransitionOreId = 0,
            float highTransitionOreMassRatio = 0f,
            float lowTransitionTemp = 0f,
            byte lowTransitionTargetId = 0,
            byte lowTransitionOreId = 0,
            float lowTransitionOreMassRatio = 0f)
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

            ThermalConductivity = thermalConductivity;
            SpecificHeatCapacity = specificHeatCapacity;
            DefaultTemperature = defaultTemperature;

            HighTransitionTemp = highTransitionTemp;
            HighTransitionTargetId = highTransitionTargetId;
            HighTransitionOreId = highTransitionOreId;
            HighTransitionOreMassRatio = highTransitionOreMassRatio;

            LowTransitionTemp = lowTransitionTemp;
            LowTransitionTargetId = lowTransitionTargetId;
            LowTransitionOreId = lowTransitionOreId;
            LowTransitionOreMassRatio = lowTransitionOreMassRatio;
        }
    }
}