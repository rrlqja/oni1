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

        // ── 열 속성 ──
        public readonly float ThermalConductivity;
        public readonly float SpecificHeatCapacity;
        public readonly float DefaultTemperature;

        // ── 가열 상태변환 ──
        public readonly float HighTransitionTemp;
        public readonly byte HighTransitionTargetId;
        public readonly byte HighTransitionOreId;
        public readonly float HighTransitionOreMassRatio;

        // ── 냉각 상태변환 ──
        public readonly float LowTransitionTemp;
        public readonly byte LowTransitionTargetId;
        public readonly byte LowTransitionOreId;
        public readonly float LowTransitionOreMassRatio;

        // ── 액체 렌더링 파라미터 (신규) ──
        public readonly float LiquidPatternStrength;
        public readonly float LiquidFlowSpeed;
        public readonly float LiquidPatternScale;
        public readonly float LiquidSurfaceHighlight;
        public readonly float LiquidViscosity;

        // ── 기체 렌더링 파라미터 (신규) ──
        public readonly float GasCloudScale;
        public readonly float GasCloudContrast;
        public readonly float GasDriftSpeed;
        public readonly float GasEdgeSoftness;

        /// <summary>전체 파라미터 생성자 (ElementDefinitionSO.ToRuntimeDefinition 용)</summary>
        public ElementRuntimeDefinition(
            byte id, string name,
            ElementBehaviorType behaviorType,
            DisplacementPriority displacementPriority,
            float density, int defaultMass, int maxMass,
            int viscosity, int minSpreadMass,
            bool isSolid, Color32 baseColor,
            int lateralRetainMass,
            float thermalConductivity,
            float specificHeatCapacity,
            float defaultTemperature,
            float highTransitionTemp,
            byte highTransitionTargetId,
            byte highTransitionOreId,
            float highTransitionOreMassRatio,
            float lowTransitionTemp,
            byte lowTransitionTargetId,
            byte lowTransitionOreId,
            float lowTransitionOreMassRatio,
            float liquidPatternStrength,
            float liquidFlowSpeed,
            float liquidPatternScale,
            float liquidSurfaceHighlight,
            float liquidViscosity,
            float gasCloudScale,
            float gasCloudContrast,
            float gasDriftSpeed,
            float gasEdgeSoftness)
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
            LiquidPatternStrength = liquidPatternStrength;
            LiquidFlowSpeed = liquidFlowSpeed;
            LiquidPatternScale = liquidPatternScale;
            LiquidSurfaceHighlight = liquidSurfaceHighlight;
            LiquidViscosity = liquidViscosity;
            GasCloudScale = gasCloudScale;
            GasCloudContrast = gasCloudContrast;
            GasDriftSpeed = gasDriftSpeed;
            GasEdgeSoftness = gasEdgeSoftness;
        }

        /// <summary>
        /// 기존 호환 생성자 (테스트 + GravityOperatorTests 등).
        /// 열/상태변환/렌더링 파라미터는 기본값 사용.
        /// </summary>
        public ElementRuntimeDefinition(
            byte id, string name,
            ElementBehaviorType behaviorType,
            DisplacementPriority displacementPriority,
            float density, int defaultMass, int maxMass,
            int viscosity, int minSpreadMass,
            bool isSolid, Color32 baseColor,
            int lateralRetainMass,
            // 열 속성 (기존 기본값 파라미터)
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
            // 렌더링 파라미터 기본값
            LiquidPatternStrength = 0.15f;
            LiquidFlowSpeed = 0.15f;
            LiquidPatternScale = 1.0f;
            LiquidSurfaceHighlight = 0.15f;
            LiquidViscosity = 0f;
            GasCloudScale = 2.0f;
            GasCloudContrast = 1.5f;
            GasDriftSpeed = 0.05f;
            GasEdgeSoftness = 0.8f;
        }
    }
}
