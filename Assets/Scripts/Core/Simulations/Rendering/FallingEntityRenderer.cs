using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 투사체 낙하 엔티티 렌더러.
    ///
    /// FallingEntityManager의 활성 엔티티를 시각화한다.
    /// 틱 간 PreviousY → CurrentY를 Lerp하여 부드러운 낙하 애니메이션.
    ///
    /// 각 엔티티를 원소 BaseColor로 작은 원형(다이아몬드) Quad로 렌더링.
    /// MeshFilter + MeshRenderer로 한 번에 배칭.
    ///
    /// SortingLayer: Liquid와 동일 레이어, Order를 1 높여서 위에 표시.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class FallingEntityRenderer : MonoBehaviour
    {
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;

        [Header("Visual")]
        [Tooltip("엔티티 하나의 렌더링 크기 (셀 단위). 1.0 = 한 셀 크기.")]
        [Range(0.3f, 1.5f)]
        [SerializeField] private float entitySize = 0.7f;

        [Tooltip("최소 밝기 (질량 기반)")]
        [Range(0.1f, 0.8f)]
        [SerializeField] private float minBrightness = 0.4f;

        private SimulationWorld _world;
        private Mesh _mesh;
        private Material _material;

        // 틱 간 보간 비율
        private float _interpolation;
        private float _tickInterval;
        private float _timeSinceLastTick;

        private readonly List<Vector3> _vertices = new(256);
        private readonly List<int> _triangles = new(384);
        private readonly List<Color32> _colors = new(256);

        private void Reset()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        // ================================================================
        //  초기화
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

            _mesh = new Mesh { name = "FallingEntityMesh" };
            _mesh.MarkDynamic();
            meshFilter.mesh = _mesh;

            EnsureMaterial();
            transform.localPosition = Vector3.zero;

            _tickInterval = 1f / world.TicksPerSecond;
            _timeSinceLastTick = 0f;

            // 이벤트 구독
            world.OnTickCompleted += OnTickCompleted;
        }

        private void OnDestroy()
        {
            if (_world != null)
                _world.OnTickCompleted -= OnTickCompleted;

            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }

            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }
        }

        private void OnTickCompleted()
        {
            // 틱 발생 시 보간 타이머 리셋
            _timeSinceLastTick = 0f;
            _tickInterval = 1f / _world.TicksPerSecond;
        }

        // ================================================================
        //  매 프레임 갱신 (보간 렌더링)
        // ================================================================

        private void LateUpdate()
        {
            if (_world == null)
                return;

            // 보간 비율 계산
            if (!_world.IsPaused)
            {
                _timeSinceLastTick += Time.deltaTime;
                _interpolation = _tickInterval > 0f
                    ? Mathf.Clamp01(_timeSinceLastTick / _tickInterval)
                    : 1f;
            }
            else
            {
                _interpolation = 1f;
            }

            BuildMesh();
        }

        // ================================================================
        //  메시 생성
        // ================================================================

        private void BuildMesh()
        {
            _vertices.Clear();
            _triangles.Clear();
            _colors.Clear();

            SimulationRunner runner = GetRunner();
            if (runner == null || runner.FallingEntities == null)
            {
                _mesh.Clear();
                return;
            }

            IReadOnlyList<FallingEntity> entities = runner.FallingEntities.ActiveEntities;

            if (entities.Count == 0)
            {
                _mesh.Clear();
                return;
            }

            int w = _world.Grid.Width;
            int h = _world.Grid.Height;
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;

            for (int i = 0; i < entities.Count; i++)
            {
                FallingEntity entity = entities[i];
                if (!entity.IsActive)
                    continue;

                // 틱 간 Y 보간
                float interpolatedY = Mathf.Lerp(entity.PreviousY, entity.CurrentY, _interpolation);

                // 셀 좌표 → 월드 좌표
                float worldX = entity.CellX - halfW + 0.5f;
                float worldY = interpolatedY - halfH + 0.5f;

                // 원소 색상
                ref readonly ElementRuntimeDefinition def =
                    ref _world.GetElement(entity.ElementId);

                Color32 color = ApplyBrightness(def.BaseColor, entity.Mass, def.MaxMass);

                AddEntityQuad(worldX, worldY, entitySize, color);
            }

            _mesh.Clear();

            if (_vertices.Count == 0)
                return;

            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.SetColors(_colors);
            _mesh.RecalculateBounds();
        }

        /// <summary>
        /// 다이아몬드 형태 Quad 추가 (물방울/모래알 느낌).
        /// </summary>
        private void AddEntityQuad(float cx, float cy, float size, Color32 color)
        {
            float half = size * 0.5f;

            int vi = _vertices.Count;

            // 다이아몬드 (45도 회전 사각형)
            _vertices.Add(new Vector3(cx, cy + half, 0));      // top
            _vertices.Add(new Vector3(cx + half, cy, 0));      // right
            _vertices.Add(new Vector3(cx, cy - half, 0));      // bottom
            _vertices.Add(new Vector3(cx - half, cy, 0));      // left

            _triangles.Add(vi);     _triangles.Add(vi + 1); _triangles.Add(vi + 2);
            _triangles.Add(vi);     _triangles.Add(vi + 2); _triangles.Add(vi + 3);

            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
        }

        // ================================================================
        //  유틸리티
        // ================================================================

        private Color32 ApplyBrightness(Color32 baseColor, int mass, int maxMass)
        {
            if (maxMass <= 0)
                return baseColor;

            float ratio = Mathf.Clamp01((float)mass / maxMass);
            float brightness = ratio * (1f - minBrightness) + minBrightness;

            return new Color32(
                (byte)(baseColor.r * brightness),
                (byte)(baseColor.g * brightness),
                (byte)(baseColor.b * brightness),
                baseColor.a);
        }

        private SimulationRunner GetRunner()
        {
            // SimulationWorld에서 Runner 참조
            // Runner가 public이 아니면 SimulationWorld에 프로퍼티 추가 필요
            return _world.Runner;
        }

        private void EnsureMaterial()
        {
            if (_material != null)
                return;

            // Vertex Color 지원 Unlit 셰이더
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            _material = new Material(shader)
            {
                name = "FallingEntityMaterial"
            };

            meshRenderer.material = _material;
        }
    }
}