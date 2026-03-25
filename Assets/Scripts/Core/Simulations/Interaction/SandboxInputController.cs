using Core.Simulation.Definitions;
using Core.Simulation.Rendering;
using UnityEngine;

namespace Core.Simulation.Interaction
{
    /// <summary>
    /// 샌드박스 입력 컨트롤러.
    ///
    /// 개선사항:
    ///   - 브러시 크기 조절 ([ ] 키 또는 Shift+스크롤)
    ///   - 카메라 팬 중 편집 차단 (CameraController.IsPanning 참조)
    ///   - 키보드 원소 선택은 유지 (UI 패널과 병행)
    ///   - 좌클릭: 페인트, 우클릭: 카메라 팬 (EraseCell은 Ctrl+좌클릭으로 이동)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxInputController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private GridRenderManager gridRenderManager;
        [SerializeField] private WorldEditService worldEditService;
        [SerializeField] private CameraController cameraController;

        [Header("Brush")]
        [Min(1)]
        [SerializeField] private int brushSize = 1;

        [Tooltip("브러시 최대 크기")]
        [Min(1)]
        [SerializeField] private int maxBrushSize = 20;

        /// <summary>현재 브러시 크기. UI에서 참조/설정 가능.</summary>
        public int BrushSize
        {
            get => brushSize;
            set => brushSize = Mathf.Clamp(value, 1, maxBrushSize);
        }

        public int MaxBrushSize => maxBrushSize;

        private int _lastEditedX = int.MinValue;
        private int _lastEditedY = int.MinValue;
        private int _lastEditedButton = int.MinValue;

        private void Reset()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (gridRenderManager == null)
                gridRenderManager = GetComponentInChildren<GridRenderManager>()
                    ?? GetComponentInParent<GridRenderManager>();

            if (worldEditService == null)
                worldEditService = GetComponent<WorldEditService>()
                    ?? GetComponentInParent<WorldEditService>();

            if (cameraController == null)
                cameraController = FindFirstObjectByType<CameraController>();
        }

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (gridRenderManager == null)
                gridRenderManager = GetComponentInChildren<GridRenderManager>()
                    ?? GetComponentInParent<GridRenderManager>();

            if (worldEditService == null)
                worldEditService = GetComponent<WorldEditService>()
                    ?? GetComponentInParent<WorldEditService>();

            if (cameraController == null)
                cameraController = FindFirstObjectByType<CameraController>();
        }

        private void Update()
        {
            if (targetCamera == null || gridRenderManager == null || worldEditService == null)
                return;

            HandleSelectionKeys();
            HandleBrushSizeKeys();

            if (!TryGetHoveredCell(out int x, out int y))
            {
                ResetLastEdited();
                return;
            }

            // 카메라 팬 중에는 셀 편집 차단
            if (cameraController != null && cameraController.IsPanning)
            {
                ResetLastEdited();
                return;
            }

            HandleMousePainting(x, y);
            HandleMiddleClickInfo(x, y);
        }

        // ================================================================
        //  원소 선택 (키보드 — UI 패널과 병행)
        // ================================================================

        private void HandleSelectionKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                worldEditService.SetSelectedElement(BuiltInElementIds.Steam);

            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                worldEditService.SetSelectedElement(BuiltInElementIds.Bedrock);

            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                worldEditService.SetSelectedElement(BuiltInElementIds.Sand);

            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                worldEditService.SetSelectedElement(BuiltInElementIds.Water);

            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
                worldEditService.SetSelectedElement(BuiltInElementIds.Oil);

            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
                worldEditService.SetSelectedElement(BuiltInElementIds.DirtyWater);

            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7))
                worldEditService.SetSelectedElement(BuiltInElementIds.Oxygen);

            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8))
                worldEditService.SetSelectedElement(BuiltInElementIds.Hydrogen);

            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9))
                worldEditService.SetSelectedElement(BuiltInElementIds.CarbonDioxide);
            
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
                worldEditService.SetSelectedElement(BuiltInElementIds.Ice);
        }

        // ================================================================
        //  브러시 크기
        // ================================================================

        private void HandleBrushSizeKeys()
        {
            // [ ] 키로 브러시 크기 조절
            if (Input.GetKeyDown(KeyCode.LeftBracket))
                BrushSize--;

            if (Input.GetKeyDown(KeyCode.RightBracket))
                BrushSize++;

            // Shift + 스크롤로도 브러시 크기 조절
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                float scroll = Input.mouseScrollDelta.y;
                if (scroll > 0f) BrushSize++;
                else if (scroll < 0f) BrushSize--;
            }
        }

        // ================================================================
        //  마우스 페인팅
        // ================================================================

        private void HandleMousePainting(int x, int y)
        {
            // 좌클릭: 페인트 / Ctrl+좌클릭: 지우기
            if (Input.GetMouseButton(0))
            {
                // Command+좌클릭은 셀 정보 로그용 → 페인트 스킵
                if (Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand))
                    return;
                
                bool isErase = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                if (ShouldSkipRepeatedEdit(x, y, isErase ? 1 : 0))
                    return;

                if (isErase)
                    PaintArea(x, y, BuiltInElementIds.Vacuum);
                else
                    PaintArea(x, y, worldEditService.SelectedElementId);

                RememberLastEdited(x, y, isErase ? 1 : 0);
                return;
            }

            ResetLastEdited();
        }

        private void PaintArea(int centerX, int centerY, byte elementId)
        {
            if (brushSize <= 1)
            {
                if (elementId == BuiltInElementIds.Vacuum)
                    worldEditService.EraseCell(centerX, centerY);
                else
                    worldEditService.PaintCell(centerX, centerY);
                return;
            }

            // 원형 브러시
            int radius = brushSize / 2;
            int radiusSq = radius * radius;
            byte savedElement = worldEditService.SelectedElementId;

            // 임시로 원소 설정 (PaintCell이 selectedElement를 사용하므로)
            if (elementId != BuiltInElementIds.Vacuum)
                worldEditService.SetSelectedElement(elementId);

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radiusSq)
                        continue;

                    int tx = centerX + dx;
                    int ty = centerY + dy;

                    if (elementId == BuiltInElementIds.Vacuum)
                        worldEditService.EraseCell(tx, ty);
                    else
                        worldEditService.PaintCell(tx, ty);
                }
            }

            // 원소 복원
            if (elementId != BuiltInElementIds.Vacuum && savedElement != elementId)
                worldEditService.SetSelectedElement(savedElement);
        }

        private void HandleMiddleClickInfo(int x, int y)
        {
            bool middleClick = Input.GetMouseButtonDown(2);
            bool commandClick = (Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand))
                                && Input.GetMouseButtonDown(0);

            if (middleClick || commandClick)
                worldEditService.LogCellInfo(x, y);
        }

        // ================================================================
        //  좌표 변환
        // ================================================================

        private bool TryGetHoveredCell(out int x, out int y)
        {
            x = -1;
            y = -1;

            Vector3 mouse = Input.mousePosition;

            if (mouse.x < 0 || mouse.y < 0 || mouse.x > Screen.width || mouse.y > Screen.height)
                return false;

            float zDistance = Mathf.Abs(targetCamera.transform.position.z
                - gridRenderManager.transform.position.z);

            Vector3 worldPoint = targetCamera.ScreenToWorldPoint(
                new Vector3(mouse.x, mouse.y, zDistance));

            return gridRenderManager.TryWorldToCell(worldPoint, out x, out y);
        }

        // ================================================================
        //  중복 편집 방지
        // ================================================================

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