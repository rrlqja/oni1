using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Interaction
{
    [DisallowMultipleComponent]
    public sealed class SimulationPlaybackInput : MonoBehaviour
    {
        [SerializeField] private SimulationWorld simulationWorld;

        private void Reset()
        {
            if (simulationWorld == null)
                simulationWorld = GetComponent<SimulationWorld>() ?? GetComponentInParent<SimulationWorld>();
        }

        private void Awake()
        {
            if (simulationWorld == null)
                simulationWorld = GetComponent<SimulationWorld>() ?? GetComponentInParent<SimulationWorld>();
        }

        private void Update()
        {
            if (simulationWorld == null)
                return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                simulationWorld.TogglePause();
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (!simulationWorld.IsPaused)
                    simulationWorld.Pause();

                simulationWorld.StepOneTick();
            }
        }
    }
}