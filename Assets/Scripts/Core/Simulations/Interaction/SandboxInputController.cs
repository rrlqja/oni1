using Core.Simulation.Definitions;
using Core.Simulation.Rendering;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Interaction
{
    /// <summary>
    /// 샌드박스 입력 컨트롤러.
    ///
    /// 개선사항:
    ///   - 브러시 크기 조절 ([ ] 키 또는 Shift+스크롤)
    ///   - 카메라 팬 중 편집 차단 (CameraController.IsPanning 참조)
    ///   - 원소 선택: ElementDatabaseSO 기반 데이터 주도 매핑 (1~9, 0 키 → DB 순서)
    ///   - 좌클릭: 페인트, 우클릭: 카메라 팬 (EraseCell은 Ctrl+좌클릭으로 이동)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxInputController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private GridRenderManager gridRenderManager;
        [SerializeField] private WorldEditService worldEditService;
        [SerializeField] private CameraController cameraController;

        [Header("Element Selection")]
        [Tooltip("원소 데이터베이스. 1~9,0 키가 DB 순서(Vacuum 제외)대로 매핑된다.")]
        [SerializeField] private ElementDatabaseSO elementDatabase;

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

        // Vacuum을 제외한 선택 가능 원소 ID 목록 (키 매핑용)
        private byte[] _selectableElementIds;

        // 숫자 키 매핑 (1~9, 0 → 인덱스 0~9)
        private static readonly KeyCode[] NumberKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
            KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.Alpha0
        };

        private static readonly KeyCode[] NumpadKeys =
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3,
            KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6,
            KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9,
            KeyCode.Keypad0
        };

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

            if (elementDatabase == null)
                elementDatabase = FindAnyObjectByType<SimulationWorld>()?.GetComponent<SandboxUI>()
                    ?.GetType().GetField("elementDatabase",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(FindAnyObjectByType<SandboxUI>()) as ElementDatabaseSO;
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

            BuildSelectableElementList();
        }

        /// <summary>
        /// ElementDatabaseSO에서 Vacuum을 제외한 선택 가능 원소 ID 목록을 구성한다.
        /// 키 1~9, 0이 이 목록의 인덱스 0~9에 대응한다.
        /// </summary>
        private void BuildSelectableElementList()
        {
            if (elementDatabase == null)
            {
                Debug.LogWarning("SandboxInputController: ElementDatabaseSO가 할당되지 않았습니다. " +
                                 "키보드 원소 선택이 비활성화됩니다.", this);
                _selectableElementIds = System.Array.Empty<byte>();
                return;
            }

            var list = new System.Collections.Generic.List<byte>();

            foreach (var element in elementDatabase.Elements)
            {
                if (element == null) continue;
                if (element.Id == BuiltInElementIds.Vacuum) continue;

                list.Add(element.Id);
            }

            _selectableElementIds = list.ToArray();

            // 디버그 로그: 키 매핑 확인
            for (int i = 0; i < _selectableElementIds.Length && i < 10; i++)
            {
                int keyLabel = (i < 9) ? (i + 1) : 0;
                Debug.Log($"[ElementKey] {keyLabel} → ID {_selectableElementIds[i]}");
            }
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
        //  원소 선택 (키보드 — 데이터 주도)
        // ================================================================

        private void HandleSelectionKeys()
        {
            if (_selectableElementIds == null || _selectableElementIds.Length == 0)
                return;

            int maxIndex = Mathf.Min(_selectableElementIds.Length, 10);

            for (int i = 0; i < maxIndex; i++)
            {
                if (Input.GetKeyDown(NumberKeys[i]) || Input.GetKeyDown(NumpadKeys[i]))
                {
                    worldEditService.SetSelectedElement(_selectableElementIds[i]);
                    return;
                }
            }
        }

        // ================================================================
        //  브러시 크기
        // ================================================================

        private void HandleBrushSizeKeys()
        {
            if (Input.GetKeyDown(KeyCode.RightBracket))
                BrushSize++;

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                BrushSize--;

            // Shift+스크롤로 브러시 크기 조절
            if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                && !Mathf.Approximately(Input.mouseScrollDelta.y, 0f))
            {
                BrushSize += (int)Mathf.Sign(Input.mouseScrollDelta.y);
            }
        }

        // ================================================================
        //  마우스 페인팅
        // ================================================================

        private void HandleMousePainting(int x, int y)
        {
            bool leftDown = Input.GetMouseButton(0);
            bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (!leftDown)
            {
                ResetLastEdited();
                return;
            }

            int button = ctrlHeld ? 1 : 0;

            if (ShouldSkipRepeatedEdit(x, y, button))
                return;

            RememberLastEdited(x, y, button);
            ApplyBrush(x, y, ctrlHeld);
        }

        private void ApplyBrush(int centerX, int centerY, bool erase)
        {
            byte savedElement = worldEditService.SelectedElementId;
            byte elementId = erase ? BuiltInElementIds.Vacuum : savedElement;

            if (erase)
                worldEditService.SetSelectedElement(BuiltInElementIds.Vacuum);

            int radius = brushSize - 1;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius)
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