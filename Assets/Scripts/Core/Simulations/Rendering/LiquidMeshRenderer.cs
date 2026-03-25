using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 액체 레이어 렌더러 (커스텀 Mesh 기반).
    ///
    /// Phase 5-3: 표면 곡선 + 물결 애니메이션.
    ///   - 표면 셀의 좌우 이웃 수위를 보간하여 부드러운 수면 곡선
    ///   - 시간 기반 사인파로 잔잔한 물결 효과
    ///   - 표면 Quad를 좌/우 2개 반쪽으로 분할하여 곡선 표현
    ///   - Inspector에서 waveAmplitude, waveFrequency, waveSpeed 실시간 조절
    ///
    /// 처리 대상: ElementBehaviorType.Liquid
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class LiquidMeshRenderer : MonoBehaviour, IGridLayerRenderer
    {
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;

        [Header("Visual")]
        [Tooltip("액체의 최소 밝기 (질량 0일 때)")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float minMassBrightness = 0.15f;

        [Tooltip("표면 셀의 최소 채움 비율")]
        [Range(0.05f, 0.3f)]
        [SerializeField] private float minFillRatio = 0.1f;

        [Header("Surface Curve")]
        [Tooltip("표면 곡선 보간 활성화")]
        [SerializeField] private bool enableSurfaceCurve = true;

        [Tooltip("좌우 이웃 수위 보간 강도. 0=직선, 1=완전 보간")]
        [Range(0f, 1f)]
        [SerializeField] private float curveSmoothing = 0.5f;

        [Header("Wave Animation")]
        [Tooltip("물결 애니메이션 활성화")]
        [SerializeField] private bool enableWave = true;

        [Tooltip("물결 높이 (셀 단위). 0.02 = 셀 높이의 2%")]
        [Range(0f, 0.1f)]
        [SerializeField] private float waveAmplitude = 0.03f;

        [Tooltip("물결 주파수. 클수록 파장이 짧아진다.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float waveFrequency = 3f;

        [Tooltip("물결 이동 속도.")]
        [Range(0f, 5f)]
        [SerializeField] private float waveSpeed = 1.5f;

        [Header("Pattern")]
        [Tooltip("액체 패턴 텍스처. null이면 패턴 없이 단색.")]
        [SerializeField] private Texture2D patternTexture;

        [Range(0f, 0.4f)]
        [SerializeField] private float patternStrength = 0.12f;

        [Range(0.5f, 8f)]
        [SerializeField] private float patternScale = 1.0f;

        [Range(-1f, 1f)]
        [SerializeField] private float flowSpeedX = 0.15f;

        [Range(-1f, 1f)]
        [SerializeField] private float flowSpeedY = -0.05f;

        [Header("Surface")]
        [Range(0f, 0.5f)]
        [SerializeField] private float surfaceHighlight = 0.2f;

        private SimulationWorld _world;
        private Mesh _mesh;
        private Material _material;

        private readonly List<Vector3> _vertices = new List<Vector3>(2048);
        private readonly List<int> _triangles = new List<int>(3072);
        private readonly List<Color32> _colors = new List<Color32>(2048);
        private readonly List<Vector2> _uvs = new List<Vector2>(2048);

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
            if (world == null)
                return;

            _world = world;

            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh { name = "LiquidMesh" };
            _mesh.MarkDynamic();
            meshFilter.mesh = _mesh;

            EnsureMaterial();
            transform.localPosition = Vector3.zero;
        }

        public void Refresh()
        {
            if (_world == null || _world.Grid == null || _world.ElementRegistry == null)
                return;

            ClearBuffers();
            BuildMesh();
            ApplyMesh();
            UpdateMaterialParams();
        }

        public void RefreshDirty(IReadOnlyList<int> dirtyIndices, int gridWidth)
        {
            // Mesh는 전체 재생성 필요
            Refresh();
        }

        public void Cleanup()
        {
            ReleaseVisuals();
        }

        // ================================================================
        //  LateUpdate — 물결 애니메이션은 매 프레임 갱신
        // ================================================================

        private void LateUpdate()
        {
            if (!enableWave || _world == null || !_world.IsPaused == false)
                return;

            // 물결은 시뮬레이션 틱과 무관하게 매 프레임 갱신
            // GridRenderManager의 dirty 판정과 별개로 동작
            if (_mesh != null && _mesh.vertexCount > 0)
            {
                ClearBuffers();
                BuildMesh();
                ApplyMesh();
            }
        }

        // ================================================================
        //  Mesh 빌드
        // ================================================================

        private void BuildMesh()
        {
            WorldGrid grid = _world.Grid;
            int w = grid.Width;
            int h = grid.Height;
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float time = Time.time;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    SimCell cell = grid.GetCell(x, y);
                    ref readonly ElementRuntimeDefinition element =
                        ref _world.GetElement(cell.ElementId);

                    if (element.BehaviorType != ElementBehaviorType.Liquid)
                        continue;
                    if (cell.Mass <= 0)
                        continue;

                    bool isSurface = IsSurfaceCell(grid, x, y);

                    Color32 color = CalculateLiquidColor(in cell, in element, isSurface);

                    if (isSurface)
                    {
                        float fillRatio = ComputeFillRatio(cell.Mass, element.MaxMass);
                        AddSurfaceQuad(grid, x, y, fillRatio, color, halfW, halfH, w, h, time);
                    }
                    else
                    {
                        // 비표면: 전체 채움 단순 Quad
                        AddQuad(x - halfW, y - halfH, 1f, 1f, color);
                    }
                }
            }
        }

        // ================================================================
        //  표면 Quad — 곡선 + 물결
        // ================================================================

        /// <summary>
        /// 표면 셀의 Quad를 좌반쪽/우반쪽으로 분할하여 곡선 수면을 표현한다.
        ///
        /// 좌측 상단 높이 = (자신 수위 + 왼쪽 이웃 수위) / 2
        /// 우측 상단 높이 = (자신 수위 + 오른쪽 이웃 수위) / 2
        /// 중앙 상단 높이 = 자신 수위
        ///
        /// 물결: 시간 + x 좌표 기반 사인파를 높이에 더한다.
        /// </summary>
        private void AddSurfaceQuad(
            WorldGrid grid, int x, int y,
            float fillRatio, Color32 color,
            float halfW, float halfH,
            int gridW, int gridH, float time)
        {
            float cellLeft = x - halfW;
            float cellBottom = y - halfH;
            float cellMid = cellLeft + 0.5f;
            float cellRight = cellLeft + 1f;

            float centerHeight = fillRatio;

            // ── 좌우 이웃 수위 보간 ──
            float leftHeight = centerHeight;
            float rightHeight = centerHeight;

            if (enableSurfaceCurve)
            {
                float leftNeighborFill = GetNeighborSurfaceFill(grid, x - 1, y, gridW, gridH);
                float rightNeighborFill = GetNeighborSurfaceFill(grid, x + 1, y, gridW, gridH);

                leftHeight = Mathf.Lerp(centerHeight, (centerHeight + leftNeighborFill) * 0.5f, curveSmoothing);
                rightHeight = Mathf.Lerp(centerHeight, (centerHeight + rightNeighborFill) * 0.5f, curveSmoothing);
            }

            // ── 물결 애니메이션 ──
            if (enableWave)
            {
                float waveL = Mathf.Sin((x - 0.5f) * waveFrequency + time * waveSpeed) * waveAmplitude;
                float waveC = Mathf.Sin(x * waveFrequency + time * waveSpeed) * waveAmplitude;
                float waveR = Mathf.Sin((x + 0.5f) * waveFrequency + time * waveSpeed) * waveAmplitude;

                leftHeight += waveL;
                centerHeight += waveC;
                rightHeight += waveR;
            }

            // 최소 채움 보장
            leftHeight = Mathf.Max(leftHeight, minFillRatio);
            centerHeight = Mathf.Max(centerHeight, minFillRatio);
            rightHeight = Mathf.Max(rightHeight, minFillRatio);

            // 최대 1.0 클램프
            leftHeight = Mathf.Min(leftHeight, 1f);
            centerHeight = Mathf.Min(centerHeight, 1f);
            rightHeight = Mathf.Min(rightHeight, 1f);

            // ── 좌반쪽 Quad (left ~ mid) ──
            int vi = _vertices.Count;

            _vertices.Add(new Vector3(cellLeft, cellBottom, 0f));                        // 0: 좌하
            _vertices.Add(new Vector3(cellMid, cellBottom, 0f));                         // 1: 중하
            _vertices.Add(new Vector3(cellMid, cellBottom + centerHeight, 0f));          // 2: 중상
            _vertices.Add(new Vector3(cellLeft, cellBottom + leftHeight, 0f));           // 3: 좌상

            _triangles.Add(vi); _triangles.Add(vi + 2); _triangles.Add(vi + 1);
            _triangles.Add(vi); _triangles.Add(vi + 3); _triangles.Add(vi + 2);

            _colors.Add(color); _colors.Add(color);
            _colors.Add(color); _colors.Add(color);

            float surfaceY = 0.5f + fillRatio * 0.5f;
            _uvs.Add(new Vector2(0f, 0.5f));        // 좌하
            _uvs.Add(new Vector2(0.5f, 0.5f));      // 중하
            _uvs.Add(new Vector2(0.5f, surfaceY));   // 중상
            _uvs.Add(new Vector2(0f, surfaceY));     // 좌상

            // ── 우반쪽 Quad (mid ~ right) ──
            vi = _vertices.Count;

            _vertices.Add(new Vector3(cellMid, cellBottom, 0f));                         // 0: 중하
            _vertices.Add(new Vector3(cellRight, cellBottom, 0f));                       // 1: 우하
            _vertices.Add(new Vector3(cellRight, cellBottom + rightHeight, 0f));         // 2: 우상
            _vertices.Add(new Vector3(cellMid, cellBottom + centerHeight, 0f));          // 3: 중상

            _triangles.Add(vi); _triangles.Add(vi + 2); _triangles.Add(vi + 1);
            _triangles.Add(vi); _triangles.Add(vi + 3); _triangles.Add(vi + 2);

            _colors.Add(color); _colors.Add(color);
            _colors.Add(color); _colors.Add(color);

            _uvs.Add(new Vector2(0.5f, 0.5f));      // 중하
            _uvs.Add(new Vector2(1f, 0.5f));        // 우하
            _uvs.Add(new Vector2(1f, surfaceY));     // 우상
            _uvs.Add(new Vector2(0.5f, surfaceY));   // 중상
        }

        /// <summary>
        /// 이웃 셀의 표면 수위를 반환한다.
        /// 이웃이 표면 액체가 아니면 0(빈 공간) 또는 1(가득 참)을 반환.
        /// </summary>
        private float GetNeighborSurfaceFill(WorldGrid grid, int nx, int ny, int gridW, int gridH)
        {
            // 범위 밖
            if (nx < 0 || nx >= gridW || ny < 0 || ny >= gridH)
                return 0f;

            SimCell neighbor = grid.GetCell(nx, ny);
            ref readonly ElementRuntimeDefinition neighborDef =
                ref _world.GetElement(neighbor.ElementId);

            // 이웃이 액체가 아님 → 빈 공간 취급
            if (neighborDef.BehaviorType != ElementBehaviorType.Liquid)
                return 0f;

            if (neighbor.Mass <= 0)
                return 0f;

            // 이웃도 표면인지 확인
            if (!IsSurfaceCell(grid, nx, ny))
                return 1f; // 비표면 = 가득 참

            return ComputeFillRatio(neighbor.Mass, neighborDef.MaxMass);
        }

        // ================================================================
        //  표면 판정 / 채움 비율
        // ================================================================

        private bool IsSurfaceCell(WorldGrid grid, int x, int y)
        {
            bool isSupported = IsSupported(grid, x, y);
            bool isExposed = IsExposed(grid, x, y);
            return isSupported && isExposed;
        }

        private bool IsSupported(WorldGrid grid, int x, int y)
        {
            if (y - 1 < 0)
                return true;

            ref readonly ElementRuntimeDefinition belowDef =
                ref _world.GetElement(grid.GetCell(x, y - 1).ElementId);

            return belowDef.IsSolid
                || belowDef.BehaviorType == ElementBehaviorType.Liquid;
        }

        private bool IsExposed(WorldGrid grid, int x, int y)
        {
            if (y + 1 >= grid.Height)
                return true;

            ref readonly ElementRuntimeDefinition aboveDef =
                ref _world.GetElement(grid.GetCell(x, y + 1).ElementId);

            return aboveDef.BehaviorType == ElementBehaviorType.Vacuum
                || aboveDef.BehaviorType == ElementBehaviorType.Gas;
        }

        private float ComputeFillRatio(int mass, int maxMass)
        {
            if (maxMass <= 0) return 1f;
            float raw = Mathf.Clamp01((float)mass / maxMass);
            return Mathf.Max(raw, minFillRatio);
        }

        // ================================================================
        //  단순 Quad (비표면용)
        // ================================================================

        private void AddQuad(float left, float bottom, float width, float height, Color32 color)
        {
            float right = left + width;
            float top = bottom + height;

            int vi = _vertices.Count;

            _vertices.Add(new Vector3(left, bottom, 0f));
            _vertices.Add(new Vector3(right, bottom, 0f));
            _vertices.Add(new Vector3(right, top, 0f));
            _vertices.Add(new Vector3(left, top, 0f));

            _triangles.Add(vi); _triangles.Add(vi + 2); _triangles.Add(vi + 1);
            _triangles.Add(vi); _triangles.Add(vi + 3); _triangles.Add(vi + 2);

            _colors.Add(color); _colors.Add(color);
            _colors.Add(color); _colors.Add(color);

            _uvs.Add(new Vector2(0f, 0f));    // 좌하
            _uvs.Add(new Vector2(1f, 0f));    // 우하
            _uvs.Add(new Vector2(1f, 0.4f));  // 우상
            _uvs.Add(new Vector2(0f, 0.4f));  // 좌상
        }

        // ================================================================
        //  색상 계산
        // ================================================================

        private Color32 CalculateLiquidColor(
            in SimCell cell,
            in ElementRuntimeDefinition element,
            bool isSurface)
        {
            if (!isSurface)
                return element.BaseColor;

            int maxMass = element.MaxMass;
            if (maxMass <= 0)
                return element.BaseColor;

            float ratio = Mathf.Clamp01((float)cell.Mass / maxMass);
            float brightness = ratio * (1f - minMassBrightness) + minMassBrightness;

            Color32 baseColor = element.BaseColor;
            return new Color32(
                (byte)(baseColor.r * brightness),
                (byte)(baseColor.g * brightness),
                (byte)(baseColor.b * brightness),
                baseColor.a);
        }

        // ================================================================
        //  Mesh 적용 / 클리어
        // ================================================================

        private void ClearBuffers()
        {
            _vertices.Clear();
            _triangles.Clear();
            _colors.Clear();
            _uvs.Clear();
        }

        private void ApplyMesh()
        {
            _mesh.Clear();

            if (_vertices.Count == 0)
                return;

            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.SetColors(_colors);

            if (_uvs.Count > 0)
            _mesh.SetUVs(0, _uvs);
        }

        // ================================================================
        //  Material
        // ================================================================

        private void EnsureMaterial()
        {
            if (_material != null)
                return;

            Shader liquidShader = Shader.Find("Simulation/Liquid");

            if (liquidShader != null)
            {
                _material = new Material(liquidShader)
                {
                    name = "LiquidMaterial"
                };
                Debug.Log("LiquidMeshRenderer: Custom Liquid shader loaded.");
            }
            else
            {
                // 폴백: 기존 셰이더 (패턴 없이 단색)
                Shader fallback = Shader.Find("Sprites/Default");
                if (fallback == null)
                    fallback = Shader.Find("Unlit/Color");

                _material = new Material(fallback)
                {
                    name = "LiquidMaterial_Fallback"
                };
                Debug.LogWarning(
                    "LiquidMeshRenderer: 'Simulation/Liquid' shader not found. " +
                    "Falling back to Sprites/Default.");
            }

            meshRenderer.material = _material;
        }

        // ================================================================
        //  리소스 해제
        // ================================================================

        private void ReleaseVisuals()
        {
            if (meshFilter != null)
                meshFilter.mesh = null;

            if (_mesh != null)
            {
                SafeDestroy(_mesh);
                _mesh = null;
            }

            if (_material != null)
            {
                SafeDestroy(_material);
                _material = null;
            }
        }

        private void UpdateMaterialParams()
        {
            if (_material == null) return;

            if (patternTexture != null)
                _material.SetTexture("_PatternTex", patternTexture);

            _material.SetFloat("_PatternStrength", patternStrength);
            _material.SetFloat("_PatternScale", patternScale);
            _material.SetFloat("_FlowSpeedX", flowSpeedX);
            _material.SetFloat("_FlowSpeedY", flowSpeedY);
            _material.SetFloat("_SurfaceHighlight", surfaceHighlight);
        }

        private void OnDestroy()
        {
            ReleaseVisuals();
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;

            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}