using UnityEngine;

namespace Core.Simulation.Interaction
{
    /// <summary>
    /// 2D 시뮬레이션용 카메라 컨트롤러.
    ///
    /// 기능:
    ///   - 마우스 우클릭 드래그: 팬 (카메라 이동)
    ///   - 스크롤 휠: 줌 (orthographic size 조절)
    ///   - Home 키: 월드 중앙으로 리셋
    ///   - 월드 경계 클램핑 (선택)
    ///
    /// SandboxInputController와 독립 — 카메라 조작과 셀 편집이 분리된다.
    /// 우클릭 드래그 중에는 SandboxInputController의 EraseCell이 무시되도록
    /// IsPanning 상태를 외부에서 조회할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;

        [Header("Zoom")]
        [Tooltip("최소 orthographic size (최대 줌인)")]
        [Min(0.5f)]
        [SerializeField] private float minZoom = 2f;

        [Tooltip("최대 orthographic size (최대 줌아웃)")]
        [Min(1f)]
        [SerializeField] private float maxZoom = 80f;

        [Tooltip("줌 속도 배율")]
        [Range(0.5f, 10f)]
        [SerializeField] private float zoomSpeed = 5f;

        [Tooltip("줌 부드러움 (Lerp 속도). 높을수록 즉각적.")]
        [Range(5f, 50f)]
        [SerializeField] private float zoomSmoothing = 15f;

        [Header("Pan")]
        [Tooltip("팬 버튼 (0=좌, 1=우, 2=휠)")]
        [Range(0, 2)]
        [SerializeField] private int panButton = 1;

        [Header("Bounds (선택)")]
        [Tooltip("활성화하면 카메라가 월드 경계 밖으로 나가지 않는다.")]
        [SerializeField] private bool clampToBounds = true;

        [Tooltip("월드 경계 (셀 단위). 0이면 SimulationWorld에서 자동 설정.")]
        [SerializeField] private Vector2 worldSize = Vector2.zero;

        [Tooltip("경계 밖 여유 공간 (셀 단위)")]
        [SerializeField] private float boundsPadding = 5f;

        // ── 외부 조회용 ──
        /// <summary>현재 팬 드래그 중인지 여부. InputController에서 참조.</summary>
        public bool IsPanning { get; private set; }

        private float _targetZoom;
        private Vector3 _panOrigin;
        private Vector3 _cameraOriginPos;

        private void Reset()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera != null)
                _targetZoom = targetCamera.orthographicSize;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                return;

            HandleZoom();
            HandlePan();
            HandleReset();

            // 줌 부드러운 전환
            float current = targetCamera.orthographicSize;
            if (!Mathf.Approximately(current, _targetZoom))
            {
                targetCamera.orthographicSize = Mathf.Lerp(current, _targetZoom, Time.unscaledDeltaTime * zoomSmoothing);
            }

            if (clampToBounds)
                ClampCameraPosition();
        }

        // ================================================================
        //  줌
        // ================================================================

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f))
                return;

            // UI 위에서는 줌 안 함 (향후 EventSystem 체크 추가 가능)

            // 마우스 위치 기준 줌 — 마우스 포인터를 중심으로 확대/축소
            Vector3 mouseWorldBefore = targetCamera.ScreenToWorldPoint(Input.mousePosition);

            // 로그 스케일 줌: 줌인/아웃이 어느 레벨에서든 자연스러움
            float logZoom = Mathf.Log(_targetZoom);
            logZoom -= scroll * zoomSpeed * 0.1f;
            _targetZoom = Mathf.Clamp(Mathf.Exp(logZoom), minZoom, maxZoom);

            // 즉시 적용하여 마우스 포인터 기준 보정
            targetCamera.orthographicSize = _targetZoom;

            Vector3 mouseWorldAfter = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector3 delta = mouseWorldBefore - mouseWorldAfter;
            targetCamera.transform.position += new Vector3(delta.x, delta.y, 0f);
        }

        // ================================================================
        //  팬
        // ================================================================

        private void HandlePan()
        {
            if (Input.GetMouseButtonDown(panButton))
            {
                IsPanning = true;
                _panOrigin = targetCamera.ScreenToWorldPoint(Input.mousePosition);
                _cameraOriginPos = targetCamera.transform.position;
            }

            if (Input.GetMouseButton(panButton) && IsPanning)
            {
                Vector3 currentWorld = targetCamera.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = _panOrigin - currentWorld;
                targetCamera.transform.position = _cameraOriginPos + new Vector3(delta.x, delta.y, 0f);

                // 드래그 중 origin 갱신 (누적 오차 방지)
                _cameraOriginPos = targetCamera.transform.position;
                _panOrigin = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(panButton))
            {
                IsPanning = false;
            }
        }

        // ================================================================
        //  리셋
        // ================================================================

        private void HandleReset()
        {
            if (Input.GetKeyDown(KeyCode.Home))
            {
                // 월드 중앙, 전체가 보이는 줌 레벨로 리셋
                Vector3 pos = targetCamera.transform.position;
                targetCamera.transform.position = new Vector3(0f, 0f, pos.z);

                if (worldSize.x > 0 && worldSize.y > 0)
                {
                    float aspect = targetCamera.aspect;
                    float fitHeight = worldSize.y * 0.5f + boundsPadding;
                    float fitWidth = (worldSize.x * 0.5f + boundsPadding) / aspect;
                    _targetZoom = Mathf.Min(Mathf.Max(fitHeight, fitWidth), maxZoom);
                }
            }
        }

        // ================================================================
        //  경계 클램핑
        // ================================================================

        private void ClampCameraPosition()
        {
            if (worldSize.x <= 0 || worldSize.y <= 0)
                return;

            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;

            float halfWorldW = worldSize.x * 0.5f + boundsPadding;
            float halfWorldH = worldSize.y * 0.5f + boundsPadding;

            Vector3 pos = targetCamera.transform.position;

            // 월드가 화면보다 작으면 중앙 고정
            if (halfWidth >= halfWorldW)
                pos.x = 0f;
            else
                pos.x = Mathf.Clamp(pos.x, -halfWorldW + halfWidth, halfWorldW - halfWidth);

            if (halfHeight >= halfWorldH)
                pos.y = 0f;
            else
                pos.y = Mathf.Clamp(pos.y, -halfWorldH + halfHeight, halfWorldH - halfHeight);

            targetCamera.transform.position = pos;
        }

        // ================================================================
        //  외부 설정
        // ================================================================

        /// <summary>
        /// SimulationWorld 초기화 후 월드 크기를 설정한다.
        /// </summary>
        public void SetWorldSize(int width, int height)
        {
            worldSize = new Vector2(width, height);
        }
    }
}