using System;
using Core.Simulation.Definitions;
using Core.Simulation.Rendering;
using Core.Simulation.Interaction;
using UnityEngine;

namespace Core.Simulation.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SimulationWorld : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ElementDatabaseSO elementDatabase;

        public ElementDatabaseSO ElementDatabase => elementDatabase;

        [Header("World Size")]
        [Min(1)]
        [SerializeField] private int width = 128;

        [Min(1)]
        [SerializeField] private int height = 64;

        [Header("View")]
        [SerializeField] private GridRenderManager gridRenderManager;

        [Header("Simulation")]
        [Min(0.1f)]
        [SerializeField] private float ticksPerSecond = 10f;

        [SerializeField] private bool startPaused = true;

        private const int MAX_CATCHUP_TICKS = 5;

        private SimulationRunner _simulationRunner;

        public SimulationRunner Runner => _simulationRunner;

        public ElementRegistry ElementRegistry { get; private set; }
        public WorldGrid Grid { get; private set; }

        public int Width => width;
        public int Height => height;
        public bool IsPaused => _isPaused;
        public int CurrentTick => _currentTick;
        public float TicksPerSecond => ticksPerSecond;

        // ── 이벤트 ──
        // 렌더러, UI, 디버그 뷰 등이 구독하여 갱신 타이밍을 받는다.
        // SimulationWorld가 GridRenderer를 직접 알 필요가 없어진다.

        /// <summary>
        /// 시뮬레이션 틱이 완료된 후 발생한다.
        /// GridRenderer 등이 구독하여 화면을 갱신한다.
        /// </summary>
        public event Action OnTickCompleted;

        /// <summary>
        /// 월드가 외부에서 수정(편집, 생성 등)된 후 발생한다.
        /// 시뮬레이션 틱이 아닌 직접적인 셀 수정 시 사용.
        /// </summary>
        public event Action OnWorldModified;

        private bool _isPaused;
        private float _tickAccumulator;
        private int _currentTick;

        private void Reset()
        {
            if (gridRenderManager == null)
                gridRenderManager = GetComponentInChildren<GridRenderManager>();
        }

        private void OnEnable()
        {
            Debug.Log("SimulationWorld OnEnable", this);
        }

        private void Awake()
        {
            Debug.Log("SimulationWorld Awake START", this);

            if (gridRenderManager == null)
                gridRenderManager = GetComponentInChildren<GridRenderManager>();

            if (elementDatabase == null)
            {
                Debug.LogError("SimulationWorld: ElementDatabaseSO is not assigned.", this);
                enabled = false;
                return;
            }

            if (gridRenderManager == null)
            {
                Debug.LogError("SimulationWorld: GridRenderManager is not assigned and was not found in children.", this);
                enabled = false;
                return;
            }

            Debug.Log("ElementDatabase assigned", this);

            ElementRegistry = new ElementRegistry(elementDatabase);
            Debug.Log("ElementRegistry created", this);

            Grid = new WorldGrid(width, height);
            Debug.Log($"WorldGrid created: {width}x{height}", this);

            _simulationRunner = new SimulationRunner(Grid, ElementRegistry);

            GenerateWorld();
            Debug.Log("World generated", this);

            gridRenderManager.Initialize(this);
            gridRenderManager.RefreshAll();

            var cam = FindFirstObjectByType<CameraController>();
            if (cam != null)
                cam.SetWorldSize(width, height);

            _isPaused = startPaused;
            _tickAccumulator = 0f;
            _currentTick = 0;
        }

        private void Update()
        {
            if (_isPaused)
                return;

            RunRealtimeTicks(Time.deltaTime);
        }

        private void Start()
        {
            Debug.Log("SimulationWorld Start", this);
        }

        private void OnDestroy()
        {
            Grid?.Dispose();
            Grid = null;
            ElementRegistry = null;
        }

        private void RunRealtimeTicks(float deltaTime)
        {
            float tickInterval = 1f / ticksPerSecond;
            _tickAccumulator += deltaTime;

            int ticksThisFrame = 0;

            while (_tickAccumulator >= tickInterval)
            {
                _tickAccumulator -= tickInterval;

                if (ticksThisFrame >= MAX_CATCHUP_TICKS)
                {
                    // 남은 시간을 버려서 악순환 방지
                    _tickAccumulator = 0f;
                    break;
                }

                StepOneTickInternal();
                ticksThisFrame++;
            }
        }

        [ContextMenu("Generate World")]
        public void GenerateWorld()
        {
            if (Grid == null || ElementRegistry == null)
                return;

            BasicWorldGenerator.Generate(Grid, ElementRegistry);
        }

        [ContextMenu("Refresh Renderer")]
        public void RefreshRenderer()
        {
            if (gridRenderManager == null || Grid == null || ElementRegistry == null)
                return;

            gridRenderManager.Initialize(this);
            gridRenderManager.RefreshAll();
        }

        public void RegenerateAndRefresh()
        {
            GenerateWorld();
            RefreshRenderer();
        }

        public void TogglePause()
        {
            _isPaused = !_isPaused;
            Debug.Log(_isPaused ? "Simulation Paused" : "Simulation Resumed", this);
        }

        public void Pause()
        {
            if (_isPaused)
                return;

            _isPaused = true;
            Debug.Log("Simulation Paused", this);
        }

        public void Resume()
        {
            if (!_isPaused)
                return;

            _isPaused = false;
            Debug.Log("Simulation Resumed", this);
        }

        public void StepOneTick()
        {
            if (!_isPaused)
            {
                Debug.Log("StepOneTick ignored because simulation is running. Pause first or use playback input.", this);
                return;
            }

            StepOneTickInternal();
        }

        /// <summary>
        /// 외부 수정(WorldEditService 등) 후 호출하여 구독자에게 알린다.
        /// </summary>
        public void NotifyWorldModified()
        {
            OnWorldModified?.Invoke();
        }

        private void StepOneTickInternal()
        {
            _currentTick++;

            _simulationRunner?.Step(_currentTick);

            // 이벤트 기반 통지 — 렌더러가 구독하여 갱신
            OnTickCompleted?.Invoke();
        }
        
        /// <summary>
        /// 시뮬레이션 속도를 변경한다 (틱/초).
        /// </summary>
        public void SetTicksPerSecond(float tps)
        {
            ticksPerSecond = Mathf.Max(0.1f, tps);
        }

        public ref readonly ElementRuntimeDefinition GetElement(byte id)
        {
            return ref ElementRegistry.Get(id);
        }
    }
}