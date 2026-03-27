using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 고체 레이어 렌더러 — ONI 스타일 연속 텍스처 방식.
    ///
    /// 원소별 서브메시로 분리하여, 각 원소에 다른 텍스처를 적용.
    /// MeshRenderer의 materials 배열로 원소별 Material 할당.
    /// 월드UV로 텍스처 연속 타일링 + 가장자리 셰이더 마스크.
    ///
    /// 처리 대상: ElementBehaviorType.StaticSolid, FallingSolid
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class SolidMeshRenderer : MonoBehaviour, IGridLayerRenderer
    {
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;

        [Header("Shared Textures")]
        [Tooltip("공유 균열 마스크 텍스처. 전용 텍스처 없는 원소에 질감을 추가.")]
        [SerializeField] private Texture2D crackMaskTexture;

        [Header("Edge")]
        [Range(0f, 0.5f)]
        [SerializeField] private float edgeDarken = 0.15f;

        [Range(0.02f, 0.25f)]
        [SerializeField] private float edgeWidth = 0.08f;

        [Header("Crack")]
        [Range(0f, 1f)]
        [SerializeField] private float crackStrength = 0.3f;

        [Range(0.1f, 4f)]
        [SerializeField] private float crackScale = 1.0f;

        [Header("Texture")]
        [Range(0.1f, 2f)]
        [SerializeField] private float defaultTextureScale = 0.25f;

        private SimulationWorld _world;
        private Mesh _mesh;
        private Shader _solidShader;

        // 원소별 빌드 데이터
        private readonly Dictionary<byte, ElementMeshData> _elemData = new(8);

        // 원소별 Material (텍스처별로 다른 Material)
        private readonly Dictionary<byte, Material> _elemMaterials = new(8);

        // 활성 원소 ID 순서 (서브메시 인덱스 매핑)
        private readonly List<byte> _activeElementIds = new(8);

        // 원소별 텍스처 캐시
        private readonly Dictionary<byte, Texture2D> _elemTextures = new(8);
        private readonly Dictionary<byte, float> _elemTextureScales = new(8);
        private readonly Dictionary<byte, float> _elemCrackStrengths = new(8);

        // 셰이더 프로퍼티 ID
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int PropCrackTex = Shader.PropertyToID("_CrackTex");
        private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int PropTextureStrength = Shader.PropertyToID("_TextureStrength");
        private static readonly int PropCrackStrength = Shader.PropertyToID("_CrackStrength");
        private static readonly int PropCrackScale = Shader.PropertyToID("_CrackScale");
        private static readonly int PropEdgeDarken = Shader.PropertyToID("_EdgeDarken");
        private static readonly int PropEdgeWidth = Shader.PropertyToID("_EdgeWidth");

        private sealed class ElementMeshData
        {
            public readonly List<Vector3> Vertices = new(512);
            public readonly List<int> Triangles = new(768);
            public readonly List<Vector2> UV0 = new(512);       // 월드UV
            public readonly List<Vector2> UV1 = new(512);       // 로컬UV
            public readonly List<Vector2> UV2 = new(512);       // edge L,R
            public readonly List<Vector2> UV3 = new(512);       // edge B,T

            public void Clear()
            {
                Vertices.Clear();
                Triangles.Clear();
                UV0.Clear();
                UV1.Clear();
                UV2.Clear();
                UV3.Clear();
            }
        }

        private void Reset()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        // ================================================================
        //  IGridLayerRenderer 구현
        // ================================================================

        public void Initialize(SimulationWorld world)
        {
            if (world == null) return;

            _world = world;

            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh { name = "SolidMesh" };
            _mesh.MarkDynamic();
            meshFilter.mesh = _mesh;

            _solidShader = Shader.Find("Simulation/Solid");
            if (_solidShader == null)
            {
                _solidShader = Shader.Find("Sprites/Default");
                Debug.LogWarning("SolidMeshRenderer: 'Simulation/Solid' shader not found.", this);
            }

            LoadElementTextures();
            transform.localPosition = Vector3.zero;
        }

        public void Refresh()
        {
            if (_world == null || _world.Grid == null || _world.ElementRegistry == null)
                return;

            ClearAllBuffers();
            BuildMesh();
            ApplyMesh();
        }

        public void RefreshDirty(IReadOnlyList<int> dirtyIndices, int gridWidth)
        {
            Refresh();
        }

        public void Cleanup()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
                _mesh = null;
            }

            foreach (var kvp in _elemMaterials)
            {
                if (kvp.Value != null)
                {
                    if (Application.isPlaying) Destroy(kvp.Value);
                    else DestroyImmediate(kvp.Value);
                }
            }
            _elemMaterials.Clear();
        }

        private void OnDestroy() => Cleanup();

        // ================================================================
        //  원소별 텍스처 로드
        // ================================================================

        private void LoadElementTextures()
        {
            _elemTextures.Clear();
            _elemTextureScales.Clear();
            _elemCrackStrengths.Clear();

            var database = _world.ElementDatabase;
            if (database == null) return;

            foreach (var so in database.Elements)
            {
                if (so == null) continue;

                if (so.SolidTexture != null)
                    _elemTextures[so.Id] = so.SolidTexture;

                _elemTextureScales[so.Id] = so.SolidTextureScale;
                _elemCrackStrengths[so.Id] = so.SolidCrackStrength;
            }
        }

        // ================================================================
        //  Mesh 빌드
        // ================================================================

        private void ClearAllBuffers()
        {
            foreach (var kvp in _elemData)
                kvp.Value.Clear();
            _activeElementIds.Clear();
        }

        private void BuildMesh()
        {
            WorldGrid grid = _world.Grid;
            int w = grid.Width;
            int h = grid.Height;
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    SimCell cell = grid.GetCell(x, y);
                    ref readonly ElementRuntimeDefinition element =
                        ref _world.GetElement(cell.ElementId);

                    if (element.BehaviorType != ElementBehaviorType.StaticSolid &&
                        element.BehaviorType != ElementBehaviorType.FallingSolid)
                        continue;

                    if (cell.Mass <= 0 && element.BehaviorType == ElementBehaviorType.FallingSolid)
                        continue;

                    byte elemId = cell.ElementId;

                    if (!_elemData.TryGetValue(elemId, out var data))
                    {
                        data = new ElementMeshData();
                        _elemData[elemId] = data;
                    }

                    // 활성 원소 추적 (서브메시 순서)
                    if (!_activeElementIds.Contains(elemId))
                        _activeElementIds.Add(elemId);

                    float edgeL = IsSameSolid(grid, x - 1, y, elemId) ? 0f : 1f;
                    float edgeR = IsSameSolid(grid, x + 1, y, elemId) ? 0f : 1f;
                    float edgeB = IsSameSolid(grid, x, y - 1, elemId) ? 0f : 1f;
                    float edgeT = IsSameSolid(grid, x, y + 1, elemId) ? 0f : 1f;

                    float texScale = defaultTextureScale;
                    if (_elemTextureScales.TryGetValue(elemId, out float s))
                        texScale = s;

                    AddQuad(data, x, y, halfW, halfH, texScale,
                            edgeL, edgeR, edgeB, edgeT);
                }
            }
        }

        private bool IsSameSolid(WorldGrid grid, int x, int y, byte elemId)
        {
            if (!grid.InBounds(x, y)) return false;
            return grid.GetCell(x, y).ElementId == elemId;
        }

        private void AddQuad(
            ElementMeshData data,
            int cellX, int cellY,
            float halfW, float halfH,
            float texScale,
            float edgeL, float edgeR, float edgeB, float edgeT)
        {
            float left = cellX - halfW;
            float bottom = cellY - halfH;
            float right = left + 1f;
            float top = bottom + 1f;

            // 삼각형 인덱스는 서브메시 내부 기준 (0부터)
            int vi = data.Vertices.Count;

            data.Vertices.Add(new Vector3(left, bottom, 0f));
            data.Vertices.Add(new Vector3(right, bottom, 0f));
            data.Vertices.Add(new Vector3(right, top, 0f));
            data.Vertices.Add(new Vector3(left, top, 0f));

            data.Triangles.Add(vi);     data.Triangles.Add(vi + 2); data.Triangles.Add(vi + 1);
            data.Triangles.Add(vi);     data.Triangles.Add(vi + 3); data.Triangles.Add(vi + 2);

            // UV0: 월드UV
            float uvL = cellX * texScale;
            float uvR = (cellX + 1) * texScale;
            float uvB = cellY * texScale;
            float uvT = (cellY + 1) * texScale;

            data.UV0.Add(new Vector2(uvL, uvB));
            data.UV0.Add(new Vector2(uvR, uvB));
            data.UV0.Add(new Vector2(uvR, uvT));
            data.UV0.Add(new Vector2(uvL, uvT));

            // UV1: 로컬UV
            data.UV1.Add(new Vector2(0f, 0f));
            data.UV1.Add(new Vector2(1f, 0f));
            data.UV1.Add(new Vector2(1f, 1f));
            data.UV1.Add(new Vector2(0f, 1f));

            // UV2: edge L,R
            Vector2 edgeLR = new Vector2(edgeL, edgeR);
            data.UV2.Add(edgeLR); data.UV2.Add(edgeLR);
            data.UV2.Add(edgeLR); data.UV2.Add(edgeLR);

            // UV3: edge B,T
            Vector2 edgeBT = new Vector2(edgeB, edgeT);
            data.UV3.Add(edgeBT); data.UV3.Add(edgeBT);
            data.UV3.Add(edgeBT); data.UV3.Add(edgeBT);
        }

        // ================================================================
        //  Mesh 적용 — 서브메시 + Material 배열
        // ================================================================

        private void ApplyMesh()
        {
            _mesh.Clear();

            if (_activeElementIds.Count == 0)
            {
                meshRenderer.materials = new Material[0];
                return;
            }

            // 전체 버텍스 합치기 (서브메시는 버텍스를 공유하지만, 여기서는 원소별 분리)
            var allVerts = new List<Vector3>(2048);
            var allUV0 = new List<Vector2>(2048);
            var allUV1 = new List<Vector2>(2048);
            var allUV2 = new List<Vector2>(2048);
            var allUV3 = new List<Vector2>(2048);

            // 원소별 버텍스 오프셋 기록
            var elemVertexOffsets = new Dictionary<byte, int>(8);

            foreach (byte elemId in _activeElementIds)
            {
                var data = _elemData[elemId];
                if (data.Vertices.Count == 0) continue;

                elemVertexOffsets[elemId] = allVerts.Count;

                allVerts.AddRange(data.Vertices);
                allUV0.AddRange(data.UV0);
                allUV1.AddRange(data.UV1);
                allUV2.AddRange(data.UV2);
                allUV3.AddRange(data.UV3);
            }

            if (allVerts.Count == 0)
            {
                meshRenderer.materials = new Material[0];
                return;
            }

            _mesh.SetVertices(allVerts);
            _mesh.SetUVs(0, allUV0);
            _mesh.SetUVs(1, allUV1);
            _mesh.SetUVs(2, allUV2);
            _mesh.SetUVs(3, allUV3);

            // 서브메시 설정
            int subMeshCount = 0;
            var materialList = new List<Material>(8);

            // 먼저 활성 서브메시 수 카운트
            foreach (byte elemId in _activeElementIds)
            {
                if (_elemData[elemId].Vertices.Count > 0)
                    subMeshCount++;
            }

            _mesh.subMeshCount = subMeshCount;

            int subMeshIndex = 0;
            foreach (byte elemId in _activeElementIds)
            {
                var data = _elemData[elemId];
                if (data.Vertices.Count == 0) continue;

                int offset = elemVertexOffsets[elemId];

                // 삼각형 인덱스에 오프셋 적용
                var offsetTris = new int[data.Triangles.Count];
                for (int i = 0; i < data.Triangles.Count; i++)
                    offsetTris[i] = data.Triangles[i] + offset;

                _mesh.SetTriangles(offsetTris, subMeshIndex);

                // Material 확보 + 텍스처 세팅
                Material mat = GetOrCreateMaterial(elemId);
                materialList.Add(mat);

                subMeshIndex++;
            }

            meshRenderer.materials = materialList.ToArray();
        }

        // ================================================================
        //  원소별 Material 관리
        // ================================================================

        private Material GetOrCreateMaterial(byte elemId)
        {
            if (_elemMaterials.TryGetValue(elemId, out var existing))
            {
                UpdateMaterial(existing, elemId);
                return existing;
            }

            var mat = new Material(_solidShader)
            {
                name = $"SolidMaterial_{elemId}"
            };

            UpdateMaterial(mat, elemId);
            _elemMaterials[elemId] = mat;
            return mat;
        }

        private void UpdateMaterial(Material mat, byte elemId)
        {
            ref readonly var def = ref _world.GetElement(elemId);

            // BaseColor
            mat.SetColor(PropBaseColor, def.BaseColor);

            // 전용 텍스처
            if (_elemTextures.TryGetValue(elemId, out var tex))
            {
                mat.SetTexture(PropMainTex, tex);
                mat.SetFloat(PropTextureStrength, 1.0f);
            }
            else
            {
                mat.SetFloat(PropTextureStrength, 0.0f);
            }

            // 균열 마스크
            if (crackMaskTexture != null)
            {
                mat.SetTexture(PropCrackTex, crackMaskTexture);
                float cs = crackStrength;
                if (_elemCrackStrengths.TryGetValue(elemId, out float elemCs))
                    cs = elemCs;
                mat.SetFloat(PropCrackStrength, cs);
            }
            else
            {
                mat.SetFloat(PropCrackStrength, 0f);
            }

            mat.SetFloat(PropCrackScale, crackScale);
            mat.SetFloat(PropEdgeDarken, edgeDarken);
            mat.SetFloat(PropEdgeWidth, edgeWidth);
        }
    }
}
