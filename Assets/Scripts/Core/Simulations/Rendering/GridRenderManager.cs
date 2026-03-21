using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 모든 레이어 렌더러를 조율하는 중앙 관리자.
    ///
    /// Phase 5-1: DirtyTracker 통합.
    ///   - 변경 없음 → 렌더링 완전 스킵
    ///   - 소량 변경 → RefreshDirty (부분 갱신)
    ///   - 대량 변경(30%+) → Refresh (전체 갱신)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridRenderManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationWorld simulationWorld;

        [Header("Layer Renderers")]
        [SerializeField] private BackgroundRenderer backgroundRenderer;
        [SerializeField] private SolidTilemapRenderer solidRenderer;
        [SerializeField] private LiquidMeshRenderer liquidRenderer;
        [SerializeField] private GasOverlayRenderer gasRenderer;
        [SerializeField] private DebugOverlayRenderer debugOverlayRenderer;

        private IGridLayerRenderer[] _renderers;
        private DirtyTracker _dirtyTracker;
        private bool _needsRefresh;
        private bool _initialized;

        public bool IsInitialized => _initialized;

        // ================================================================
        //  초기화
        // ================================================================

        private void Reset()
        {
            if (simulationWorld == null)
                simulationWorld = GetComponentInParent<SimulationWorld>();

            if (backgroundRenderer == null)
                backgroundRenderer = GetComponentInChildren<BackgroundRenderer>();

            if (solidRenderer == null)
                solidRenderer = GetComponentInChildren<SolidTilemapRenderer>();

            if (liquidRenderer == null)
                liquidRenderer = GetComponentInChildren<LiquidMeshRenderer>();

            if (gasRenderer == null)
                gasRenderer = GetComponentInChildren<GasOverlayRenderer>();

            if (debugOverlayRenderer == null)
                debugOverlayRenderer = GetComponentInChildren<DebugOverlayRenderer>();
        }

        public void Initialize(SimulationWorld world)
        {
            if (world == null)
            {
                Debug.LogError("GridRenderManager.Initialize: world is null.", this);
                return;
            }

            UnsubscribeEvents();

            simulationWorld = world;

            _renderers = new IGridLayerRenderer[]
            {
                backgroundRenderer,
                solidRenderer,
                liquidRenderer,
                gasRenderer,
                debugOverlayRenderer
            };

            // DirtyTracker 초기화
            int cellCount = world.Grid.Width * world.Grid.Height;
            _dirtyTracker = new DirtyTracker(cellCount);

            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i]?.Initialize(simulationWorld);

            SubscribeEvents();
            _initialized = true;
        }

        // ================================================================
        //  갱신
        // ================================================================

        private void LateUpdate()
        {
            if (!_needsRefresh || !_initialized)
                return;

            _needsRefresh = false;

            // Dirty 감지
            _dirtyTracker.DetectDirty(simulationWorld.Grid);
            // Debug.Log($"[Dirty] {_dirtyTracker.DirtyIndices.Count}/{simulationWorld.Grid.Width * simulationWorld.Grid.Height} cells | {(_dirtyTracker.IsClean ? "SKIP" : _dirtyTracker.ShouldFullRefresh ? "FULL" : "PARTIAL")}");

            // 변경 없음 → 완전 스킵
            if (_dirtyTracker.IsClean)
                return;

            // 대량 변경 → 전체 갱신
            if (_dirtyTracker.ShouldFullRefresh)
            {
                for (int i = 0; i < _renderers.Length; i++)
                    _renderers[i]?.Refresh();

                return;
            }

            // 소량 변경 → 부분 갱신
            int gridWidth = simulationWorld.Grid.Width;
            var dirtyIndices = _dirtyTracker.DirtyIndices;

            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i]?.RefreshDirty(dirtyIndices, gridWidth);
        }

        /// <summary>
        /// 다음 LateUpdate에서 갱신하도록 마킹한다.
        /// </summary>
        public void MarkDirty()
        {
            _needsRefresh = true;
        }

        /// <summary>
        /// 즉시 모든 레이어를 전체 갱신한다.
        /// </summary>
        public void RefreshAll()
        {
            if (!_initialized || _renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i]?.Refresh();
        }

        // ================================================================
        //  좌표 변환
        // ================================================================

        public bool TryWorldToCell(Vector3 worldPosition, out int x, out int y)
        {
            x = -1;
            y = -1;

            if (simulationWorld == null || simulationWorld.Grid == null)
                return false;

            Vector3 local = transform.InverseTransformPoint(worldPosition);

            int w = simulationWorld.Grid.Width;
            int h = simulationWorld.Grid.Height;

            x = Mathf.FloorToInt(local.x + (w * 0.5f));
            y = Mathf.FloorToInt(local.y + (h * 0.5f));

            return simulationWorld.Grid.InBounds(x, y);
        }

        // ================================================================
        //  이벤트 구독
        // ================================================================

        private void SubscribeEvents()
        {
            if (simulationWorld == null)
                return;

            simulationWorld.OnTickCompleted += HandleTickCompleted;
            simulationWorld.OnWorldModified += HandleWorldModified;
        }

        private void UnsubscribeEvents()
        {
            if (simulationWorld == null)
                return;

            simulationWorld.OnTickCompleted -= HandleTickCompleted;
            simulationWorld.OnWorldModified -= HandleWorldModified;
        }

        private void HandleTickCompleted()
        {
            _needsRefresh = true;
        }

        private void HandleWorldModified()
        {
            _needsRefresh = true;
        }

        // ================================================================
        //  정리
        // ================================================================

        private void OnDestroy()
        {
            UnsubscribeEvents();

            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                    _renderers[i]?.Cleanup();
            }
        }
    }
}