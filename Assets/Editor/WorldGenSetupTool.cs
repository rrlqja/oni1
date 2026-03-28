#if UNITY_EDITOR
using System.IO;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime.WorldGeneration;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 월드 생성 셋업 도구.
/// Tools > Simulation > Setup World Gen 메뉴로 실행.
///
/// 기본 바이옴 5종 + WorldGenProfile 1개를 생성한다.
/// </summary>
public static class WorldGenSetupTool
{
    private const string BiomeFolder = "Assets/Data/Simulation/WorldGen/Biomes";
    private const string ProfileFolder = "Assets/Data/Simulation/WorldGen";
    private const string ElementFolder = "Assets/Data/Simulation/Elements";

    [MenuItem("Tools/Simulation/Setup World Gen (바이옴 + 프로파일)")]
    public static void Setup()
    {
        if (!EditorUtility.DisplayDialog(
                "월드 생성 셋업",
                $"기본 바이옴 5종과 WorldGenProfile을 생성합니다.\n\n" +
                $"바이옴 경로: {BiomeFolder}\n" +
                $"프로파일 경로: {ProfileFolder}\n\n" +
                "계속하시겠습니까?",
                "실행", "취소"))
            return;

        EnsureDirectories();

        // 원소 SO 참조 로드
        var elements = LoadElements();
        if (elements == null) return;

        // 바이옴 5종 생성
        var biomes = CreateBiomes(elements.Value);

        // 프로파일 생성
        CreateProfile(biomes);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[WorldGenSetup] 바이옴 5종 + 프로파일 1개 생성 완료.");
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(BiomeFolder))
            Directory.CreateDirectory(BiomeFolder);
        if (!Directory.Exists(ProfileFolder))
            Directory.CreateDirectory(ProfileFolder);
    }

    // ================================================================
    //  원소 참조 로드
    // ================================================================

    private struct ElementRefs
    {
        public ElementDefinitionSO Granite, Dirt, Sand, Ice;
        public ElementDefinitionSO Water, Oil, Brine, Magma;
        public ElementDefinitionSO Oxygen, Hydrogen, CarbonDioxide, Steam;
    }

    private static ElementRefs? LoadElements()
    {
        var refs = new ElementRefs();

        refs.Granite = LoadElement("Granite");
        refs.Dirt = LoadElement("Dirt");
        refs.Sand = LoadElement("Sand");
        refs.Ice = LoadElement("Ice");
        refs.Water = LoadElement("Water");
        refs.Oil = LoadElement("Oil");
        refs.Brine = LoadElement("Brine");
        refs.Magma = LoadElement("Magma");
        refs.Oxygen = LoadElement("Oxygen");
        refs.Hydrogen = LoadElement("Hydrogen");
        refs.CarbonDioxide = LoadElement("CarbonDioxide");
        refs.Steam = LoadElement("Steam");

        // 최소한 Granite, Dirt는 있어야 함
        if (refs.Granite == null || refs.Dirt == null)
        {
            Debug.LogError("[WorldGenSetup] 필수 원소 SO를 찾을 수 없습니다. " +
                           "먼저 Tools > Simulation > Setup Elements를 실행하세요.");
            return null;
        }

        return refs;
    }

    private static ElementDefinitionSO LoadElement(string name)
    {
        string path = $"{ElementFolder}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<ElementDefinitionSO>(path);

        if (asset == null)
        {
            // 이름으로 검색
            string[] guids = AssetDatabase.FindAssets($"t:ElementDefinitionSO {name}");
            if (guids.Length > 0)
                asset = AssetDatabase.LoadAssetAtPath<ElementDefinitionSO>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (asset == null)
            Debug.LogWarning($"[WorldGenSetup] 원소 '{name}'을 찾을 수 없습니다.");

        return asset;
    }

    // ================================================================
    //  바이옴 생성
    // ================================================================

    private struct BiomeRefs
    {
        public BiomeSO Temperate, Tundra, Volcanic, Marsh, Underground;
    }

    private static BiomeRefs CreateBiomes(ElementRefs e)
    {
        var biomes = new BiomeRefs();

        // 1. 온대 (스폰 바이옴)
        biomes.Temperate = CreateBiome("Temperate", "온대", new Color(0.45f, 0.55f, 0.35f),
            new BiomeElementLayer[]
            {
                new(e.Dirt, 40f, 0.4f, 1f),      // 위층: 흙
                new(e.Granite, 40f, 0f, 0.6f),    // 아래층: 화강암
                new(e.Sand, 15f, 0.3f, 0.8f),     // 중간: 모래
                new(e.Water != null ? e.Water : e.Dirt, 5f, 0.1f, 0.5f),
            },
            caveDensity: 0.22f, caveScale: 0.07f, caveGas: e.Oxygen,
            baseTemp: 293.15f, tempVariation: 5f);

        // 2. 동토
        biomes.Tundra = CreateBiome("Tundra", "동토", new Color(0.4f, 0.55f, 0.65f),
            new BiomeElementLayer[]
            {
                new(e.Ice ?? e.Granite, 35f),
                new(e.Granite, 40f),
                new(e.Dirt, 20f),
                new(e.Sand, 5f),
            },
            caveDensity: 0.15f, caveScale: 0.06f, caveGas: e.Oxygen,
            baseTemp: 253.15f, tempVariation: 8f); // -20°C

        // 3. 화산
        biomes.Volcanic = CreateBiome("Volcanic", "화산", new Color(0.55f, 0.3f, 0.2f),
            new BiomeElementLayer[]
            {
                new(e.Granite, 55f),
                new(e.Magma ?? e.Granite, 20f, 0f, 0.4f),  // 아래쪽에 용암
                new(e.Dirt, 15f, 0.5f, 1f),
                new(e.Sand, 10f),
            },
            caveDensity: 0.12f, caveScale: 0.05f, caveGas: e.CarbonDioxide,
            baseTemp: 373.15f, tempVariation: 30f); // 100°C

        // 4. 습지
        biomes.Marsh = CreateBiome("Marsh", "습지", new Color(0.35f, 0.45f, 0.3f),
            new BiomeElementLayer[]
            {
                new(e.Dirt, 35f),
                new(e.Sand, 20f),
                new(e.Granite, 30f, 0f, 0.5f),
                new(e.Water ?? e.Dirt, 15f),
            },
            caveDensity: 0.28f, caveScale: 0.09f, caveGas: e.Oxygen,
            baseTemp: 303.15f, tempVariation: 5f); // 30°C

        // 5. 지하
        biomes.Underground = CreateBiome("Underground", "지하", new Color(0.35f, 0.32f, 0.28f),
            new BiomeElementLayer[]
            {
                new(e.Granite, 65f),
                new(e.Dirt, 20f),
                new(e.Sand, 10f),
                new(e.Oil ?? e.Granite, 5f, 0f, 0.3f),
            },
            caveDensity: 0.18f, caveScale: 0.06f, caveGas: e.CarbonDioxide,
            baseTemp: 323.15f, tempVariation: 15f); // 50°C

        return biomes;
    }

    private static BiomeSO CreateBiome(string fileName, string displayName, Color bgColor,
        BiomeElementLayer[] layers,
        float caveDensity, float caveScale, ElementDefinitionSO caveGas,
        float baseTemp, float tempVariation)
    {
        string path = $"{BiomeFolder}/{fileName}.asset";

        // 기존 에셋 있으면 삭제
        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);

        var biome = ScriptableObject.CreateInstance<BiomeSO>();
        AssetDatabase.CreateAsset(biome, path);

        var so = new SerializedObject(biome);
        so.FindProperty("biomeName").stringValue = displayName;

        var bgColorProp = so.FindProperty("backgroundColor");
        bgColorProp.colorValue = bgColor;

        // Element layers
        var layersProp = so.FindProperty("elementLayers");
        layersProp.ClearArray();
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].element == null) continue;

            layersProp.InsertArrayElementAtIndex(layersProp.arraySize);
            var elem = layersProp.GetArrayElementAtIndex(layersProp.arraySize - 1);
            elem.FindPropertyRelative("element").objectReferenceValue = layers[i].element;
            elem.FindPropertyRelative("weight").floatValue = layers[i].weight;
            elem.FindPropertyRelative("minHeightRatio").floatValue = layers[i].minHeightRatio;
            elem.FindPropertyRelative("maxHeightRatio").floatValue = layers[i].maxHeightRatio;
        }

        so.FindProperty("caveDensity").floatValue = caveDensity;
        so.FindProperty("caveScale").floatValue = caveScale;
        so.FindProperty("caveGasElement").objectReferenceValue = caveGas;
        so.FindProperty("baseTemperature").floatValue = baseTemp;
        so.FindProperty("temperatureVariation").floatValue = tempVariation;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(biome);

        return biome;
    }

    // ================================================================
    //  프로파일 생성
    // ================================================================

    private static void CreateProfile(BiomeRefs biomes)
    {
        string path = $"{ProfileFolder}/DefaultProfile.asset";

        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);

        var profile = ScriptableObject.CreateInstance<WorldGenProfileSO>();
        AssetDatabase.CreateAsset(profile, path);

        var so = new SerializedObject(profile);

        so.FindProperty("seed").intValue = 42;
        so.FindProperty("randomizeSeed").boolValue = false;
        so.FindProperty("spawnBiome").objectReferenceValue = biomes.Temperate;

        // 나머지 바이옴 목록
        var availProp = so.FindProperty("availableBiomes");
        availProp.ClearArray();

        BiomeSO[] others = { biomes.Tundra, biomes.Volcanic, biomes.Marsh, biomes.Underground };
        for (int i = 0; i < others.Length; i++)
        {
            if (others[i] == null) continue;
            availProp.InsertArrayElementAtIndex(availProp.arraySize);
            availProp.GetArrayElementAtIndex(availProp.arraySize - 1).objectReferenceValue = others[i];
        }

        so.FindProperty("biomePointCount").intValue = 8;
        so.FindProperty("minPointDistance").floatValue = 12f;
        so.FindProperty("boundaryNoise").floatValue = 10f;
        so.FindProperty("boundaryNoiseScale").floatValue = 0.06f;
        so.FindProperty("borderThickness").intValue = 1;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);

        Debug.Log($"[WorldGenSetup] 프로파일 생성: {path}");
    }
}
#endif