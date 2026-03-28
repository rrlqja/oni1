#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 원소 시스템 셋업 도구.
/// Tools > Simulation > Setup Elements 메뉴로 실행.
///
/// 기존 원소 SO를 모두 삭제하고 14종을 새로 생성한다.
/// ElementDatabaseSO에 자동으로 등록한다.
/// </summary>
public static class ElementSetupTool
{
    private const string ElementFolder = "Assets/Data/Simulation/Elements";
    private const string DatabasePath = "Assets/Data/Simulation/ElementDatabase.asset";

    [MenuItem("Tools/Simulation/Setup Elements (14종 재생성)")]
    public static void SetupElements()
    {
        if (!EditorUtility.DisplayDialog(
                "원소 시스템 셋업",
                $"기존 원소 SO를 모두 삭제하고 14종을 새로 생성합니다.\n\n" +
                $"경로: {ElementFolder}\n\n" +
                "계속하시겠습니까?",
                "실행", "취소"))
        {
            return;
        }

        // 폴더 확인
        if (!Directory.Exists(ElementFolder))
            Directory.CreateDirectory(ElementFolder);

        // 기존 에셋 삭제
        ClearExistingElements();

        // 14종 생성
        var elements = CreateAllElements();

        // 전환 관계 설정 (SO 참조 연결)
        SetupTransitions(elements);

        // 데이터베이스 업데이트
        UpdateDatabase(elements);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ElementSetupTool] {elements.Count}종 원소 생성 완료.");
    }

    // ================================================================
    //  기존 에셋 삭제
    // ================================================================

    private static void ClearExistingElements()
    {
        string[] guids = AssetDatabase.FindAssets("t:ElementDefinitionSO", new[] { ElementFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.DeleteAsset(path);
        }

        Debug.Log($"[ElementSetupTool] 기존 원소 {guids.Length}개 삭제.");
    }

    // ================================================================
    //  14종 생성
    // ================================================================

    private static Dictionary<byte, ElementDefinitionSO> CreateAllElements()
    {
        var elements = new Dictionary<byte, ElementDefinitionSO>();

        // ── 시스템 ──

        elements[0] = CreateElement(new ElementSpec
        {
            Id = 0,
            Name = "Vacuum",
            DisplayName = "진공",
            Behavior = ElementBehaviorType.Vacuum,
            Priority = DisplacementPriority.Vacuum,
            IsSolid = false,
            Density = 0f,
            DefaultMass = 0,
            MaxMass = 0,
            Viscosity = 1,
            MinSpreadMass = 0,
            LateralRetainMass = 0,
            BaseColor = new Color32(0, 0, 0, 255),
            ThermalConductivity = 0f,
            SpecificHeatCapacity = 0f,
            DefaultTemperature = 0f,
        });

        elements[1] = CreateElement(new ElementSpec
        {
            Id = 1,
            Name = "Bedrock",
            DisplayName = "기반암",
            Behavior = ElementBehaviorType.StaticSolid,
            Priority = DisplacementPriority.StaticSolid,
            IsSolid = true,
            Density = 9999f,
            DefaultMass = 0,
            MaxMass = 0,
            Viscosity = 1,
            MinSpreadMass = 0,
            LateralRetainMass = 0,
            BaseColor = new Color32(80, 80, 85, 255),
            ThermalConductivity = 3.0f,
            SpecificHeatCapacity = 0.79f,
            DefaultTemperature = 293.15f,
        });

        // ── 고체 (StaticSolid) ──

        elements[2] = CreateElement(new ElementSpec
        {
            Id = 2,
            Name = "Granite",
            DisplayName = "화강암",
            Behavior = ElementBehaviorType.StaticSolid,
            Priority = DisplacementPriority.StaticSolid,
            IsSolid = true,
            Density = 2700f,
            DefaultMass = 1_000_000,
            MaxMass = 1_000_000,
            Viscosity = 1,
            MinSpreadMass = 0,
            LateralRetainMass = 0,
            BaseColor = new Color32(140, 135, 130, 255),
            ThermalConductivity = 3.0f,
            SpecificHeatCapacity = 0.79f,
            DefaultTemperature = 293.15f,
        });

        elements[3] = CreateElement(new ElementSpec
        {
            Id = 3,
            Name = "Dirt",
            DisplayName = "흙",
            Behavior = ElementBehaviorType.StaticSolid,
            Priority = DisplacementPriority.StaticSolid,
            IsSolid = true,
            Density = 1400f,
            DefaultMass = 1_000_000,
            MaxMass = 1_000_000,
            Viscosity = 1,
            MinSpreadMass = 0,
            LateralRetainMass = 0,
            BaseColor = new Color32(120, 85, 60, 255),
            ThermalConductivity = 0.5f,
            SpecificHeatCapacity = 0.84f,
            DefaultTemperature = 293.15f,
        });

        // ── 고체 (FallingSolid) ──

        elements[4] = CreateElement(new ElementSpec
        {
            Id = 4,
            Name = "Sand",
            DisplayName = "모래",
            Behavior = ElementBehaviorType.FallingSolid,
            Priority = DisplacementPriority.FallingSolid,
            IsSolid = true,
            Density = 1600f,
            DefaultMass = 1_000_000,
            MaxMass = 1_000_000,
            Viscosity = 1,
            MinSpreadMass = 0,
            LateralRetainMass = 0,
            BaseColor = new Color32(194, 178, 128, 255),
            ThermalConductivity = 0.2f,
            SpecificHeatCapacity = 0.83f,
            DefaultTemperature = 293.15f,
        });

        // ── 고체 (StaticSolid, 전환 대상) ──

        elements[5] = CreateElement(new ElementSpec
        {
            Id = 5,
            Name = "Ice",
            DisplayName = "얼음",
            Behavior = ElementBehaviorType.StaticSolid,
            Priority = DisplacementPriority.StaticSolid,
            IsSolid = true,
            Density = 917f,
            DefaultMass = 1_000_000,
            MaxMass = 1_000_000,
            Viscosity = 1,
            MinSpreadMass = 0,
            LateralRetainMass = 0,
            BaseColor = new Color32(200, 230, 255, 255),
            ThermalConductivity = 2.18f,
            SpecificHeatCapacity = 2.05f,
            DefaultTemperature = 263.15f,
        });

        // ── 액체 ──

        elements[6] = CreateElement(new ElementSpec
        {
            Id = 6,
            Name = "Water",
            DisplayName = "물",
            Behavior = ElementBehaviorType.Liquid,
            Priority = DisplacementPriority.Liquid,
            IsSolid = false,
            Density = 1000f,
            DefaultMass = 1_000_000,
            MaxMass = 1_000_000,
            Viscosity = 8,
            MinSpreadMass = 10_000,
            LateralRetainMass = 200_000,
            BaseColor = new Color32(60, 120, 220, 255),
            ThermalConductivity = 0.6f,
            SpecificHeatCapacity = 4.18f,
            DefaultTemperature = 293.15f,
        });

        elements[7] = CreateElement(new ElementSpec
        {
            Id = 7,
            Name = "Oil",
            DisplayName = "기름",
            Behavior = ElementBehaviorType.Liquid,
            Priority = DisplacementPriority.Liquid,
            IsSolid = false,
            Density = 700f,
            DefaultMass = 1_000_000,
            MaxMass = 1_000_000,
            Viscosity = 15,
            MinSpreadMass = 10_000,
            LateralRetainMass = 200_000,
            BaseColor = new Color32(100, 75, 30, 255),
            ThermalConductivity = 0.1f,
            SpecificHeatCapacity = 2.01f,
            DefaultTemperature = 293.15f,
        });

        elements[8] = CreateElement(new ElementSpec
        {
            Id = 8,
            Name = "Brine",
            DisplayName = "소금물",
            Behavior = ElementBehaviorType.Liquid,
            Priority = DisplacementPriority.Liquid,
            IsSolid = false,
            Density = 1200f,
            DefaultMass = 1_000_000,
            MaxMass = 1_000_000,
            Viscosity = 8,
            MinSpreadMass = 10_000,
            LateralRetainMass = 200_000,
            BaseColor = new Color32(100, 160, 140, 255),
            ThermalConductivity = 0.55f,
            SpecificHeatCapacity = 3.5f,
            DefaultTemperature = 293.15f,
        });

        elements[9] = CreateElement(new ElementSpec
        {
            Id = 9,
            Name = "Magma",
            DisplayName = "용암",
            Behavior = ElementBehaviorType.Liquid,
            Priority = DisplacementPriority.Liquid,
            IsSolid = false,
            Density = 2500f,
            DefaultMass = 1_000_000,
            MaxMass = 1_000_000,
            Viscosity = 20,
            MinSpreadMass = 10_000,
            LateralRetainMass = 300_000,
            BaseColor = new Color32(220, 80, 20, 255),
            ThermalConductivity = 4.0f,
            SpecificHeatCapacity = 1.0f,
            DefaultTemperature = 1500f,
        });

        // ── 기체 ──

        elements[10] = CreateElement(new ElementSpec
        {
            Id = 10,
            Name = "Oxygen",
            DisplayName = "산소",
            Behavior = ElementBehaviorType.Gas,
            Priority = DisplacementPriority.Gas,
            IsSolid = false,
            Density = 500f,
            DefaultMass = 1_000,
            MaxMass = 2_000,
            Viscosity = 1,
            MinSpreadMass = 50,
            LateralRetainMass = 0,
            BaseColor = new Color32(180, 210, 240, 255),
            ThermalConductivity = 0.024f,
            SpecificHeatCapacity = 1.01f,
            DefaultTemperature = 293.15f,
        });

        elements[11] = CreateElement(new ElementSpec
        {
            Id = 11,
            Name = "Hydrogen",
            DisplayName = "수소",
            Behavior = ElementBehaviorType.Gas,
            Priority = DisplacementPriority.Gas,
            IsSolid = false,
            Density = 90f,
            DefaultMass = 1_000,
            MaxMass = 2_000,
            Viscosity = 1,
            MinSpreadMass = 50,
            LateralRetainMass = 0,
            BaseColor = new Color32(240, 220, 230, 255),
            ThermalConductivity = 0.18f,
            SpecificHeatCapacity = 14.3f,
            DefaultTemperature = 293.15f,
        });

        elements[12] = CreateElement(new ElementSpec
        {
            Id = 12,
            Name = "CarbonDioxide",
            DisplayName = "이산화탄소",
            Behavior = ElementBehaviorType.Gas,
            Priority = DisplacementPriority.Gas,
            IsSolid = false,
            Density = 1000f,
            DefaultMass = 1_000,
            MaxMass = 2_000,
            Viscosity = 1,
            MinSpreadMass = 50,
            LateralRetainMass = 0,
            BaseColor = new Color32(180, 180, 170, 255),
            ThermalConductivity = 0.015f,
            SpecificHeatCapacity = 0.84f,
            DefaultTemperature = 293.15f,
        });

        elements[13] = CreateElement(new ElementSpec
        {
            Id = 13,
            Name = "Steam",
            DisplayName = "증기",
            Behavior = ElementBehaviorType.Gas,
            Priority = DisplacementPriority.Gas,
            IsSolid = false,
            Density = 200f,
            DefaultMass = 1_000,
            MaxMass = 2_000,
            Viscosity = 1,
            MinSpreadMass = 50,
            LateralRetainMass = 0,
            BaseColor = new Color32(220, 230, 240, 255),
            ThermalConductivity = 0.02f,
            SpecificHeatCapacity = 2.01f,
            DefaultTemperature = 383.15f,
        });

        return elements;
    }

    // ================================================================
    //  전환 관계 설정
    // ================================================================

    private static void SetupTransitions(Dictionary<byte, ElementDefinitionSO> elements)
    {
        var ice = elements[5];
        var water = elements[6];
        var steam = elements[13];

        // Ice → (가열 273K) → Water
        SetTransition(ice,
            highTemp: 273.15f, highTarget: water);

        // Water → (가열 373K) → Steam, (냉각 273K) → Ice
        SetTransition(water,
            highTemp: 373.15f, highTarget: steam,
            lowTemp: 273.15f, lowTarget: ice);

        // Steam → (냉각 373K) → Water
        SetTransition(steam,
            lowTemp: 373.15f, lowTarget: water);

        Debug.Log("[ElementSetupTool] 전환 관계 설정: Ice ↔ Water ↔ Steam");
    }

    // ================================================================
    //  데이터베이스 업데이트
    // ================================================================

    private static void UpdateDatabase(Dictionary<byte, ElementDefinitionSO> elements)
    {
        // 기존 데이터베이스 찾기
        var database = AssetDatabase.LoadAssetAtPath<ElementDatabaseSO>(DatabasePath);

        if (database == null)
        {
            // 경로로 못 찾으면 프로젝트 전체 검색
            string[] guids = AssetDatabase.FindAssets("t:ElementDatabaseSO");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                database = AssetDatabase.LoadAssetAtPath<ElementDatabaseSO>(path);
                Debug.Log($"[ElementSetupTool] 데이터베이스 발견: {path}");
            }
        }

        if (database == null)
        {
            Debug.LogError("[ElementSetupTool] ElementDatabaseSO를 찾을 수 없습니다. " +
                           "수동으로 데이터베이스에 원소를 등록해주세요.");
            return;
        }

        // ID 순서대로 정렬된 리스트 생성
        var sorted = new List<ElementDefinitionSO>();
        for (byte id = 0; id <= 13; id++)
        {
            if (elements.ContainsKey(id))
                sorted.Add(elements[id]);
        }

        // SerializedObject로 elements 필드 설정
        var so = new SerializedObject(database);

        // 필드 이름 후보 탐색
        string[] fieldCandidates = { "elements", "definitions", "_elements", "_definitions" };
        SerializedProperty prop = null;

        foreach (string fieldName in fieldCandidates)
        {
            prop = so.FindProperty(fieldName);
            if (prop != null && prop.isArray)
                break;
            prop = null;
        }

        if (prop == null)
        {
            // 모든 프로퍼티를 순회하여 배열 찾기
            var iter = so.GetIterator();
            iter.Next(true);
            while (iter.NextVisible(false))
            {
                if (iter.isArray && iter.propertyType == SerializedPropertyType.ObjectReference)
                {
                    prop = so.FindProperty(iter.name);
                    break;
                }
                // Generic 배열도 체크
                if (iter.isArray && iter.arrayElementType != null &&
                    iter.arrayElementType.Contains("ElementDefinitionSO"))
                {
                    prop = so.FindProperty(iter.name);
                    break;
                }
            }
        }

        if (prop != null && prop.isArray)
        {
            prop.ClearArray();
            for (int i = 0; i < sorted.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sorted[i];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            Debug.Log($"[ElementSetupTool] 데이터베이스에 {sorted.Count}종 등록 완료.");
        }
        else
        {
            Debug.LogWarning("[ElementSetupTool] 데이터베이스의 elements 배열 필드를 찾지 못했습니다. " +
                             "수동으로 Inspector에서 원소를 등록해주세요.");
        }
    }

    // ================================================================
    //  헬퍼
    // ================================================================

    private struct ElementSpec
    {
        public byte Id;
        public string Name;
        public string DisplayName;
        public ElementBehaviorType Behavior;
        public DisplacementPriority Priority;
        public bool IsSolid;
        public float Density;
        public int DefaultMass;
        public int MaxMass;
        public int Viscosity;
        public int MinSpreadMass;
        public int LateralRetainMass;
        public Color32 BaseColor;
        public float ThermalConductivity;
        public float SpecificHeatCapacity;
        public float DefaultTemperature;
    }

    private static ElementDefinitionSO CreateElement(ElementSpec spec)
    {
        var so = ScriptableObject.CreateInstance<ElementDefinitionSO>();
        string assetPath = $"{ElementFolder}/{spec.Name}.asset";

        // SerializedObject로 모든 private 필드 설정
        AssetDatabase.CreateAsset(so, assetPath);
        var serialized = new SerializedObject(so);

        serialized.FindProperty("id").intValue = spec.Id;
        serialized.FindProperty("elementName").stringValue = spec.DisplayName;
        serialized.FindProperty("behaviorType").enumValueIndex = (int)spec.Behavior;
        serialized.FindProperty("displacementPriority").enumValueIndex = (int)spec.Priority;
        serialized.FindProperty("isSolid").boolValue = spec.IsSolid;
        serialized.FindProperty("density").floatValue = spec.Density;
        serialized.FindProperty("defaultMass").intValue = spec.DefaultMass;
        serialized.FindProperty("maxMass").intValue = spec.MaxMass;
        serialized.FindProperty("viscosity").intValue = spec.Viscosity;
        serialized.FindProperty("minSpreadMass").intValue = spec.MinSpreadMass;
        serialized.FindProperty("lateralRetainMass").intValue = spec.LateralRetainMass;

        var colorProp = serialized.FindProperty("baseColor");
        colorProp.colorValue = spec.BaseColor;

        serialized.FindProperty("thermalConductivity").floatValue = spec.ThermalConductivity;
        serialized.FindProperty("specificHeatCapacity").floatValue = spec.SpecificHeatCapacity;
        serialized.FindProperty("defaultTemperature").floatValue = spec.DefaultTemperature;

        // 전환 필드 초기화 (나중에 SetupTransitions에서 설정)
        serialized.FindProperty("highTransitionTemp").floatValue = 0f;
        serialized.FindProperty("lowTransitionTemp").floatValue = 0f;
        serialized.FindProperty("highTransitionOreMassRatio").floatValue = 0f;
        serialized.FindProperty("lowTransitionOreMassRatio").floatValue = 0f;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(so);

        return so;
    }

    private static void SetTransition(ElementDefinitionSO element,
        float highTemp = 0f, ElementDefinitionSO highTarget = null,
        ElementDefinitionSO highOre = null, float highOreRatio = 0f,
        float lowTemp = 0f, ElementDefinitionSO lowTarget = null,
        ElementDefinitionSO lowOre = null, float lowOreRatio = 0f)
    {
        var serialized = new SerializedObject(element);

        serialized.FindProperty("highTransitionTemp").floatValue = highTemp;
        serialized.FindProperty("highTransitionTarget").objectReferenceValue = highTarget;
        serialized.FindProperty("highTransitionOre").objectReferenceValue = highOre;
        serialized.FindProperty("highTransitionOreMassRatio").floatValue = highOreRatio;

        serialized.FindProperty("lowTransitionTemp").floatValue = lowTemp;
        serialized.FindProperty("lowTransitionTarget").objectReferenceValue = lowTarget;
        serialized.FindProperty("lowTransitionOre").objectReferenceValue = lowOre;
        serialized.FindProperty("lowTransitionOreMassRatio").floatValue = lowOreRatio;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(element);
    }
}
#endif