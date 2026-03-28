using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Rendering;
using Core.Simulation.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Simulation.Interaction
{
    /// <summary>
    /// 마우스 호버 시 해당 셀의 원소 정보를 툴팁으로 표시한다.
    ///
    /// ONI 스타일: 마우스 커서 옆에 반투명 패널이 따라다니며
    /// 원소명, 상태, 질량, 온도, 밀도 등을 표시한다.
    /// 화면 가장자리에서는 자동으로 반대편에 표시.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CellInfoTooltip : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationWorld simulationWorld;
        [SerializeField] private GridRenderManager gridRenderManager;
        [SerializeField] private Camera targetCamera;

        [Header("Style")]
        [SerializeField] private int fontSize = 13;
        [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        [SerializeField] private Color labelColor = new Color(0.65f, 0.65f, 0.7f, 1f);
        [SerializeField] private Color valueColor = Color.white;
        [SerializeField] private Color elementNameColor = new Color(0.9f, 0.95f, 1f, 1f);

        [Header("Layout")]
        [Tooltip("커서에서 툴팁까지의 오프셋 (픽셀)")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(20f, -10f);

        [Tooltip("툴팁 너비 (픽셀)")]
        [SerializeField] private float tooltipWidth = 180f;

        [Tooltip("화면 가장자리 여백 (픽셀)")]
        [SerializeField] private float screenMargin = 10f;

        // ── UI 요소 ──
        private Canvas _canvas;
        private RectTransform _panelRect;
        private Text _elementNameText;
        private Text _infoText;
        private Image _colorSwatch;
        private CanvasGroup _canvasGroup;

        // ── 상태 ──
        private int _lastCellX = -1;
        private int _lastCellY = -1;
        private bool _isVisible;

        // ================================================================
        //  초기화
        // ================================================================

        private void Reset()
        {
            if (simulationWorld == null)
                simulationWorld = GetComponentInParent<SimulationWorld>();
            if (gridRenderManager == null)
                gridRenderManager = GetComponentInChildren<GridRenderManager>()
                    ?? GetComponentInParent<GridRenderManager>();
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Awake()
        {
            if (simulationWorld == null)
                simulationWorld = GetComponentInParent<SimulationWorld>();
            if (gridRenderManager == null)
                gridRenderManager = GetComponentInChildren<GridRenderManager>()
                    ?? GetComponentInParent<GridRenderManager>();
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Start()
        {
            BuildTooltipUI();
            SetVisible(false);
        }

        // ================================================================
        //  매 프레임 갱신
        // ================================================================

        private void Update()
        {
            if (simulationWorld == null || simulationWorld.Grid == null ||
                simulationWorld.ElementRegistry == null ||
                gridRenderManager == null || targetCamera == null)
            {
                SetVisible(false);
                return;
            }

            // 마우스가 화면 밖이면 숨김
            Vector3 mouse = Input.mousePosition;
            if (mouse.x < 0 || mouse.y < 0 ||
                mouse.x > Screen.width || mouse.y > Screen.height)
            {
                SetVisible(false);
                return;
            }

            // 셀 좌표 변환
            if (!TryGetCellUnderMouse(out int cellX, out int cellY))
            {
                SetVisible(false);
                _lastCellX = -1;
                _lastCellY = -1;
                return;
            }

            // 위치 업데이트 (매 프레임)
            UpdateTooltipPosition(mouse);

            // 셀이 바뀌었을 때만 내용 갱신
            if (cellX != _lastCellX || cellY != _lastCellY)
            {
                _lastCellX = cellX;
                _lastCellY = cellY;
                UpdateTooltipContent(cellX, cellY);
            }

            SetVisible(true);
        }

        // ================================================================
        //  셀 좌표 변환
        // ================================================================

        private bool TryGetCellUnderMouse(out int x, out int y)
        {
            x = -1;
            y = -1;

            Vector3 mouse = Input.mousePosition;
            float zDistance = Mathf.Abs(targetCamera.transform.position.z
                - gridRenderManager.transform.position.z);

            Vector3 worldPoint = targetCamera.ScreenToWorldPoint(
                new Vector3(mouse.x, mouse.y, zDistance));

            return gridRenderManager.TryWorldToCell(worldPoint, out x, out y);
        }

        // ================================================================
        //  내용 갱신
        // ================================================================

        private void UpdateTooltipContent(int x, int y)
        {
            SimCell cell = simulationWorld.Grid.GetCell(x, y);
            ref readonly ElementRuntimeDefinition element =
                ref simulationWorld.GetElement(cell.ElementId);

            // 원소명 + 좌표
            _elementNameText.text = $"{element.Name}";

            // 색상 견본
            _colorSwatch.color = element.BaseColor;

            // 상세 정보
            string behaviorLabel = GetBehaviorLabel(element.BehaviorType);
            float tempCelsius = cell.Temperature - 273.15f;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.AppendLine($"<color=#{ColorToHex(labelColor)}>좌표</color>  ({x}, {y})");
            sb.AppendLine($"<color=#{ColorToHex(labelColor)}>상태</color>  {behaviorLabel}");

            if (element.BehaviorType != ElementBehaviorType.Vacuum)
            {
                // 질량
                if (cell.Mass > 0 || element.DefaultMass > 0)
                {
                    string massStr = FormatMass(cell.Mass);
                    sb.AppendLine($"<color=#{ColorToHex(labelColor)}>질량</color>  {massStr}");
                }

                // 온도
                if (cell.Temperature > 0f)
                {
                    sb.AppendLine($"<color=#{ColorToHex(labelColor)}>온도</color>  {tempCelsius:F1}°C ({cell.Temperature:F1}K)");
                }

                // 밀도
                sb.AppendLine($"<color=#{ColorToHex(labelColor)}>밀도</color>  {element.Density:F0}");

                // 열전도율 / 비열
                if (element.ThermalConductivity > 0f)
                {
                    sb.AppendLine($"<color=#{ColorToHex(labelColor)}>전도율</color>  {element.ThermalConductivity:F3}");
                    sb.AppendLine($"<color=#{ColorToHex(labelColor)}>비열</color>  {element.SpecificHeatCapacity:F2}");
                }

                // 상태변환 정보
                if (element.HighTransitionTemp > 0f)
                {
                    ref readonly var target = ref simulationWorld.GetElement(element.HighTransitionTargetId);
                    float transC = element.HighTransitionTemp - 273.15f;
                    sb.AppendLine($"<color=#{ColorToHex(labelColor)}>가열</color>  {transC:F0}°C → {target.Name}");
                }

                if (element.LowTransitionTemp > 0f)
                {
                    ref readonly var target = ref simulationWorld.GetElement(element.LowTransitionTargetId);
                    float transC = element.LowTransitionTemp - 273.15f;
                    sb.AppendLine($"<color=#{ColorToHex(labelColor)}>냉각</color>  {transC:F0}°C → {target.Name}");
                }
            }

            _infoText.text = sb.ToString().TrimEnd('\n', '\r');
        }

        // ================================================================
        //  위치 갱신
        // ================================================================

        private void UpdateTooltipPosition(Vector3 mousePos)
        {
            if (_panelRect == null) return;

            float panelHeight = _panelRect.sizeDelta.y;
            float panelWidth = _panelRect.sizeDelta.x;

            // 기본 위치: 커서 오른쪽 아래
            float x = mousePos.x + cursorOffset.x;
            float y = mousePos.y + cursorOffset.y;

            // 화면 오른쪽을 넘으면 왼쪽에 표시
            if (x + panelWidth > Screen.width - screenMargin)
                x = mousePos.x - cursorOffset.x - panelWidth;

            // 화면 아래를 넘으면 위에 표시
            if (y - panelHeight < screenMargin)
                y = mousePos.y - cursorOffset.y + panelHeight;

            // 화면 위를 넘으면 클램핑
            if (y > Screen.height - screenMargin)
                y = Screen.height - screenMargin;

            _panelRect.position = new Vector3(x, y, 0f);
        }

        // ================================================================
        //  표시/숨김
        // ================================================================

        private void SetVisible(bool visible)
        {
            if (_isVisible == visible) return;
            _isVisible = visible;

            if (_canvasGroup != null)
                _canvasGroup.alpha = visible ? 1f : 0f;
        }

        // ================================================================
        //  UI 빌드
        // ================================================================

        private void BuildTooltipUI()
        {
            // 기존 SandboxUI의 Canvas 찾기 (같은 Canvas 사용)
            _canvas = GetComponentInChildren<Canvas>();
            if (_canvas == null)
                _canvas = FindAnyObjectByType<Canvas>();

            if (_canvas == null)
            {
                // 자체 Canvas 생성
                GameObject canvasObj = new GameObject("TooltipCanvas");
                canvasObj.transform.SetParent(transform, false);
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 200;
                canvasObj.AddComponent<CanvasScaler>();
            }

            // 패널
            GameObject panelObj = new GameObject("CellInfoTooltip");
            panelObj.transform.SetParent(_canvas.transform, false);

            _panelRect = panelObj.AddComponent<RectTransform>();
            _panelRect.pivot = new Vector2(0f, 1f); // 좌상단 기준
            _panelRect.sizeDelta = new Vector2(tooltipWidth, 0f);

            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.color = panelColor;
            panelBg.raycastTarget = false;

            _canvasGroup = panelObj.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _canvasGroup.alpha = 0f;

            // 수직 레이아웃
            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 2;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = panelObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 헤더 행 (색상 견본 + 원소명)
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(panelObj.transform, false);

            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, fontSize + 8);

            HorizontalLayoutGroup hlg = headerObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // 색상 견본
            GameObject swatchObj = new GameObject("Swatch");
            swatchObj.transform.SetParent(headerObj.transform, false);

            RectTransform swatchRect = swatchObj.AddComponent<RectTransform>();
            swatchRect.sizeDelta = new Vector2(fontSize, fontSize);

            _colorSwatch = swatchObj.AddComponent<Image>();
            _colorSwatch.color = Color.white;
            _colorSwatch.raycastTarget = false;

            LayoutElement swatchLayout = swatchObj.AddComponent<LayoutElement>();
            swatchLayout.preferredWidth = fontSize;
            swatchLayout.preferredHeight = fontSize;

            // 원소명
            GameObject nameObj = new GameObject("ElementName");
            nameObj.transform.SetParent(headerObj.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(tooltipWidth - 40, fontSize + 6);

            _elementNameText = nameObj.AddComponent<Text>();
            _elementNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _elementNameText.fontSize = fontSize + 1;
            _elementNameText.fontStyle = FontStyle.Bold;
            _elementNameText.color = elementNameColor;
            _elementNameText.alignment = TextAnchor.MiddleLeft;
            _elementNameText.raycastTarget = false;
            _elementNameText.text = "";

            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1f;

            // 구분선
            GameObject dividerObj = new GameObject("Divider");
            dividerObj.transform.SetParent(panelObj.transform, false);

            Image dividerImg = dividerObj.AddComponent<Image>();
            dividerImg.color = new Color(1f, 1f, 1f, 0.15f);
            dividerImg.raycastTarget = false;

            LayoutElement dividerLayout = dividerObj.AddComponent<LayoutElement>();
            dividerLayout.preferredHeight = 1;

            // 상세 정보
            GameObject infoObj = new GameObject("Info");
            infoObj.transform.SetParent(panelObj.transform, false);

            _infoText = infoObj.AddComponent<Text>();
            _infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _infoText.fontSize = fontSize;
            _infoText.color = valueColor;
            _infoText.alignment = TextAnchor.UpperLeft;
            _infoText.raycastTarget = false;
            _infoText.supportRichText = true;
            _infoText.lineSpacing = 1.2f;
            _infoText.text = "";
        }

        // ================================================================
        //  유틸리티
        // ================================================================

        private static string GetBehaviorLabel(ElementBehaviorType type)
        {
            return type switch
            {
                ElementBehaviorType.Vacuum => "진공",
                ElementBehaviorType.StaticSolid => "고체",
                ElementBehaviorType.FallingSolid => "낙하 고체",
                ElementBehaviorType.Liquid => "액체",
                ElementBehaviorType.Gas => "기체",
                _ => type.ToString()
            };
        }

        private static string FormatMass(int massInMg)
        {
            if (massInMg >= 1_000_000)
                return $"{massInMg / 1_000_000f:F2} kg";
            if (massInMg >= 1_000)
                return $"{massInMg / 1_000f:F1} g";
            return $"{massInMg} mg";
        }

        private static string ColorToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }
    }
}