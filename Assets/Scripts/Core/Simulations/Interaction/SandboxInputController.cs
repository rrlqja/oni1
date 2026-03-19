using Core.Simulation.Definitions;
using Core.Simulation.Rendering;
using UnityEngine;

namespace Core.Simulation.Interaction
{
    [DisallowMultipleComponent]
    public sealed class SandboxInputController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private GridRenderer gridRenderer;
        [SerializeField] private WorldEditService worldEditService;

        private int _lastEditedX = int.MinValue;
        private int _lastEditedY = int.MinValue;
        private int _lastEditedButton = int.MinValue;

        private void Reset()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (gridRenderer == null)
                gridRenderer = GetComponentInChildren<GridRenderer>() ?? GetComponentInParent<GridRenderer>();

            if (worldEditService == null)
                worldEditService = GetComponent<WorldEditService>() ?? GetComponentInParent<WorldEditService>();
        }

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (gridRenderer == null)
                gridRenderer = GetComponentInChildren<GridRenderer>() ?? GetComponentInParent<GridRenderer>();

            if (worldEditService == null)
                worldEditService = GetComponent<WorldEditService>() ?? GetComponentInParent<WorldEditService>();
        }

        private void Update()
        {
            if (targetCamera == null || gridRenderer == null || worldEditService == null)
                return;

            HandleSelectionKeys();

            if (!TryGetHoveredCell(out int x, out int y))
            {
                ResetLastEdited();
                return;
            }

            HandleMousePainting(x, y);
            HandleMiddleClickInfo(x, y);
        }

        private void HandleSelectionKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                worldEditService.SetSelectedElement(BuiltInElementIds.Vacuum);

            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                worldEditService.SetSelectedElement(BuiltInElementIds.Bedrock);

            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                worldEditService.SetSelectedElement(BuiltInElementIds.Sand);

            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                worldEditService.SetSelectedElement(BuiltInElementIds.Oxygen);

            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
                worldEditService.SetSelectedElement(BuiltInElementIds.Water);
            
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
                worldEditService.SetSelectedElement(BuiltInElementIds.Hydrogen);
        }

        private void HandleMousePainting(int x, int y)
        {
            if (Input.GetMouseButton(0))
            {
                if (ShouldSkipRepeatedEdit(x, y, 0))
                    return;

                worldEditService.PaintCell(x, y);
                RememberLastEdited(x, y, 0);
                return;
            }

            if (Input.GetMouseButton(1))
            {
                // if (ShouldSkipRepeatedEdit(x, y, 1))
                //     return;

                // worldEditService.EraseCell(x, y);
                // RememberLastEdited(x, y, 1);
                worldEditService.LogCellInfo(x, y);
                return;
            }

            ResetLastEdited();
        }

        private void HandleMiddleClickInfo(int x, int y)
        {
            if (Input.GetMouseButtonDown(2))
                worldEditService.LogCellInfo(x, y);
        }

        private bool TryGetHoveredCell(out int x, out int y)
        {
            x = -1;
            y = -1;

            Vector3 mouse = Input.mousePosition;

            if (mouse.x < 0 || mouse.y < 0 || mouse.x > Screen.width || mouse.y > Screen.height)
                return false;

            float zDistance = Mathf.Abs(targetCamera.transform.position.z - gridRenderer.transform.position.z);

            Vector3 worldPoint = targetCamera.ScreenToWorldPoint(
                new Vector3(mouse.x, mouse.y, zDistance));

            return gridRenderer.TryWorldToCell(worldPoint, out x, out y);
        }

        private bool ShouldSkipRepeatedEdit(int x, int y, int button)
        {
            return _lastEditedX == x && _lastEditedY == y && _lastEditedButton == button;
        }

        private void RememberLastEdited(int x, int y, int button)
        {
            _lastEditedX = x;
            _lastEditedY = y;
            _lastEditedButton = button;
        }

        private void ResetLastEdited()
        {
            _lastEditedX = int.MinValue;
            _lastEditedY = int.MinValue;
            _lastEditedButton = int.MinValue;
        }
    }
}