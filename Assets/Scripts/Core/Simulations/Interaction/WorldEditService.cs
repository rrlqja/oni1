using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Rendering;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Interaction
{
    [DisallowMultipleComponent]
    public sealed class WorldEditService : MonoBehaviour
    {
        [SerializeField] private SimulationWorld simulationWorld;
        [SerializeField] private GridRenderer gridRenderer;
        [SerializeField] private byte selectedElementId = BuiltInElementIds.Sand;

        public byte SelectedElementId => selectedElementId;

        private void Reset()
        {
            if (simulationWorld == null)
                simulationWorld = GetComponent<SimulationWorld>() ?? GetComponentInParent<SimulationWorld>();

            if (gridRenderer == null)
                gridRenderer = GetComponentInChildren<GridRenderer>() ?? GetComponentInParent<GridRenderer>();
        }

        private void Awake()
        {
            if (simulationWorld == null)
                simulationWorld = GetComponent<SimulationWorld>() ?? GetComponentInParent<SimulationWorld>();

            if (gridRenderer == null)
                gridRenderer = GetComponentInChildren<GridRenderer>() ?? GetComponentInParent<GridRenderer>();
        }

        public bool SetSelectedElement(byte elementId)
        {
            if (!IsReady())
                return false;

            if (!simulationWorld.ElementRegistry.IsRegistered(elementId))
            {
                Debug.LogWarning($"WorldEditService: element id {elementId} is not registered.", this);
                return false;
            }

            selectedElementId = elementId;

            ref readonly var element = ref simulationWorld.GetElement(elementId);
            Debug.Log($"Selected Element: {element.Name} (Id={element.Id})", this);

            return true;
        }

        public bool PaintCell(int x, int y)
        {
            return SetCell(x, y, selectedElementId);
        }

        public bool EraseCell(int x, int y)
        {
            return SetCell(x, y, BuiltInElementIds.Vacuum);
        }

        public bool SetCell(int x, int y, byte elementId)
        {
            if (!IsReady()) return false;
            if (!simulationWorld.Grid.InBounds(x, y)) return false;
            if (!simulationWorld.ElementRegistry.IsRegistered(elementId)) return false;

            ref readonly var element = ref simulationWorld.GetElement(elementId);

            SimCell newCell = new SimCell(
                elementId: element.Id,
                mass: element.DefaultMass,
                temperature: 0,
                flags: SimCellFlags.None);

            int index = simulationWorld.Grid.ToIndex(x, y);

            // 기존 원소를 밀어내고 새 원소를 배치
            DisplacementResolver.TryPlaceWithDisplacement(
                simulationWorld.Grid,
                simulationWorld.ElementRegistry,
                index,
                newCell);

            gridRenderer.RefreshAll();

            return true;
        }

        public void LogCellInfo(int x, int y)
        {
            if (!IsReady())
                return;

            if (!simulationWorld.Grid.InBounds(x, y))
            {
                Debug.Log($"Cell ({x}, {y}) is out of bounds.", this);
                return;
            }

            SimCell cell = simulationWorld.Grid.GetCell(x, y);
            ref readonly var element = ref simulationWorld.GetElement(cell.ElementId);

            Debug.Log(
                $"Cell ({x}, {y}) | Element={element.Name} | Id={cell.ElementId} | " +
                $"Behavior={element.BehaviorType} | Density={element.Density} | " +
                $"Mass={cell.Mass} | Temp={cell.Temperature} | Flags={cell.Flags}",
                this);
        }

        private bool IsReady()
        {
            if (simulationWorld == null || gridRenderer == null)
            {
                Debug.LogWarning("WorldEditService is missing references.", this);
                return false;
            }

            if (simulationWorld.Grid == null || simulationWorld.ElementRegistry == null)
            {
                Debug.LogWarning("SimulationWorld is not initialized yet.", this);
                return false;
            }

            return true;
        }
    }
}