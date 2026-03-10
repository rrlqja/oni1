using System;
using Core.Simulation.Definitions;
using Core.Simulation.Rendering;
using UnityEngine;

namespace Core.Simulation.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SimulationWorld : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ElementDatabaseSO elementDatabase;

        [Header("World Size")]
        [Min(1)]
        [SerializeField] private int width = 20;

        [Min(1)]
        [SerializeField] private int height = 12;

        [Header("View")]
        [SerializeField] private GridRenderer gridRenderer;

        [Header("Simulation")]
        [Min(0.1f)]
        [SerializeField] private float ticksPerSecond = 10f;

        [SerializeField] private bool startPaused = true;

        private SimulationRunner _simulationRunner;

        public ElementRegistry ElementRegistry { get; private set; }
        public WorldGrid Grid { get; private set; }

        public int Width => width;
        public int Height => height;
        public bool IsPaused => _isPaused;
        public int CurrentTick => _currentTick;
        public float TicksPerSecond => ticksPerSecond;

        private bool _isPaused;
        private float _tickAccumulator;
        private int _currentTick;

        private void Reset()
        {
            if (gridRenderer == null)
                gridRenderer = GetComponentInChildren<GridRenderer>();
        }

        private void OnEnable()
        {
            Debug.Log("SimulationWorld OnEnable", this);
        }

        private void Awake()
        {
            Debug.Log("SimulationWorld Awake START", this);

            if (gridRenderer == null)
                gridRenderer = GetComponentInChildren<GridRenderer>();

            if (elementDatabase == null)
            {
                Debug.LogError("SimulationWorld: ElementDatabaseSO is not assigned.", this);
                enabled = false;
                return;
            }

            if (gridRenderer == null)
            {
                Debug.LogError("SimulationWorld: GridRenderer is not assigned and was not found in children.", this);
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

            gridRenderer.Initialize(this);
            gridRenderer.RefreshAll();

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

            while (_tickAccumulator >= tickInterval)
            {
                _tickAccumulator -= tickInterval;
                StepOneTickInternal();
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
            if (gridRenderer == null || Grid == null || ElementRegistry == null)
                return;

            gridRenderer.Initialize(this);
            gridRenderer.RefreshAll();
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

        private void StepOneTickInternal()
        {
            _currentTick++;

            _simulationRunner?.Step(_currentTick);

            if (gridRenderer != null)
                gridRenderer.RefreshAll();

            Debug.Log($"Simulation Tick: {_currentTick}", this);
        }

        public ref readonly ElementRuntimeDefinition GetElement(byte id)
        {
            return ref ElementRegistry.Get(id);
        }
    }
}