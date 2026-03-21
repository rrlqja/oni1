using System.Collections.Generic;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Simulation.Interaction
{
    /// <summary>
    /// 샌드박스 도구 UI (런타임 생성 uGUI).
    ///
    /// 기능:
    ///   - 원소 선택 패널 (ElementDatabase에서 자동 생성)
    ///   - 브러시 크기 슬라이더
    ///   - 시뮬레이션 속도 슬라이더
    ///   - 일시정지/재생/1틱 진행 버튼
    ///   - 현재 틱/선택 원소 정보 표시
    ///
    /// Canvas를 코드로 생성하므로 프리팹 없이 동작한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationWorld simulationWorld;
        [SerializeField] private WorldEditService worldEditService;
        [SerializeField] private SandboxInputController inputController;
        [SerializeField] private ElementDatabaseSO elementDatabase;

        [Header("Style")]
        [SerializeField] private int fontSize = 14;
        [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
        [SerializeField] private Color buttonColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        [SerializeField] private Color selectedButtonColor = new Color(0.3f, 0.5f, 0.8f, 1f);
        [SerializeField] private Color textColor = Color.white;

        private Canvas _canvas;
        private readonly List<ElementButtonEntry> _elementButtons = new List<ElementButtonEntry>();
        private Text _infoText;
        private Text _brushSizeText;
        private Slider _brushSlider;
        private Slider _speedSlider;
        private Text _speedText;
        private Button _pauseButton;
        private Text _pauseButtonText;

        private struct ElementButtonEntry
        {
            public byte ElementId;
            public Image ButtonImage;
        }

        // ================================================================
        //  초기화
        // ================================================================

        private void Start()
        {
            if (simulationWorld == null)
                simulationWorld = GetComponentInParent<SimulationWorld>();
            if (worldEditService == null)
                worldEditService = GetComponentInParent<WorldEditService>();
            if (inputController == null)
                inputController = GetComponentInParent<SandboxInputController>();

            BuildUI();
        }

        private void Update()
        {
            UpdateInfoText();
            UpdateBrushDisplay();
            UpdatePauseButton();
            UpdateSelectedHighlight();
        }

        // ================================================================
        //  UI 빌드
        // ================================================================

        private void BuildUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("SandboxCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 좌측 패널 — 원소 선택 (스크롤 가능)
            RectTransform leftPanel = CreatePanel(canvasObj.transform, "ElementPanel",
                TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10, -10), new Vector2(200, 0));

            CreateLabel(leftPanel, "Elements", TextAnchor.MiddleCenter, 16, FontStyle.Bold);
            BuildElementButtons(leftPanel);

            // 우측 상단 — 정보 + 컨트롤
            RectTransform rightPanel = CreatePanel(canvasObj.transform, "ControlPanel",
                TextAnchor.UpperLeft, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-10, -10), new Vector2(200, 0));

            // 정보 텍스트
            _infoText = CreateLabel(rightPanel, "Tick: 0\nElement: ---", TextAnchor.MiddleLeft, fontSize, FontStyle.Normal);

            // 브러시 크기
            CreateLabel(rightPanel, "Brush Size", TextAnchor.MiddleLeft, fontSize, FontStyle.Bold);
            _brushSizeText = CreateLabel(rightPanel, "1", TextAnchor.MiddleRight, fontSize, FontStyle.Normal);
            _brushSlider = CreateSlider(rightPanel, 1, inputController != null ? inputController.MaxBrushSize : 20,
                1, OnBrushSliderChanged);

            // 시뮬레이션 속도
            CreateLabel(rightPanel, "Speed (TPS)", TextAnchor.MiddleLeft, fontSize, FontStyle.Bold);
            _speedText = CreateLabel(rightPanel, "10", TextAnchor.MiddleRight, fontSize, FontStyle.Normal);
            _speedSlider = CreateSlider(rightPanel, 1, 60,
                simulationWorld != null ? simulationWorld.TicksPerSecond : 10,
                OnSpeedSliderChanged);

            // 컨트롤 버튼
            RectTransform buttonRow = CreateHorizontalGroup(rightPanel, "Buttons", 5);

            _pauseButton = CreateButton(buttonRow, "▶ Play", OnPauseClicked);
            _pauseButtonText = _pauseButton.GetComponentInChildren<Text>();

            CreateButton(buttonRow, "→ Step", OnStepClicked);

            // 키 힌트
            CreateLabel(rightPanel, "\n[Key Hints]\nSpace: Pause/Play\n→: Step\n[ ]: Brush ±\nShift+Scroll: Brush\nHome: Reset Camera",
                TextAnchor.MiddleLeft, fontSize - 2, FontStyle.Italic);
        }

        // ================================================================
        //  원소 버튼 생성
        // ================================================================

        private void BuildElementButtons(RectTransform parent)
        {
            if (elementDatabase == null)
                return;

            foreach (var elementSO in elementDatabase.Elements)
            {
                if (elementSO == null)
                    continue;

                byte id = elementSO.Id;
                string name = elementSO.ElementName;
                Color32 baseColor = elementSO.BaseColor;

                Button btn = CreateElementButton(parent, id, name, baseColor);
                _elementButtons.Add(new ElementButtonEntry
                {
                    ElementId = id,
                    ButtonImage = btn.GetComponent<Image>()
                });
            }
        }

        private Button CreateElementButton(RectTransform parent, byte elementId, string name, Color32 color)
        {
            GameObject btnObj = new GameObject($"Btn_{name}");
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180, 30);

            Image img = btnObj.AddComponent<Image>();
            img.color = buttonColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            // 색상 미리보기 + 텍스트
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(30, 0);
            textRt.offsetMax = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.text = $"[{elementId}] {name}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleLeft;

            // 색상 프리뷰
            GameObject previewObj = new GameObject("ColorPreview");
            previewObj.transform.SetParent(btnObj.transform, false);
            RectTransform previewRt = previewObj.AddComponent<RectTransform>();
            previewRt.anchorMin = new Vector2(0, 0.15f);
            previewRt.anchorMax = new Vector2(0, 0.85f);
            previewRt.offsetMin = new Vector2(5, 0);
            previewRt.offsetMax = new Vector2(25, 0);
            previewRt.sizeDelta = new Vector2(20, 0);

            Image preview = previewObj.AddComponent<Image>();
            preview.color = color;

            btn.onClick.AddListener(() => OnElementClicked(elementId));

            return btn;
        }

        // ================================================================
        //  콜백
        // ================================================================

        private void OnElementClicked(byte elementId)
        {
            if (worldEditService != null)
                worldEditService.SetSelectedElement(elementId);
        }

        private void OnBrushSliderChanged(float value)
        {
            int size = Mathf.RoundToInt(value);
            if (inputController != null)
                inputController.BrushSize = size;
            if (_brushSizeText != null)
                _brushSizeText.text = size.ToString();
        }

        private void OnSpeedSliderChanged(float value)
        {
            if (simulationWorld != null)
                simulationWorld.SetTicksPerSecond(value);
            if (_speedText != null)
                _speedText.text = $"{value:F0}";
        }

        private void OnPauseClicked()
        {
            if (simulationWorld != null)
                simulationWorld.TogglePause();
        }

        private void OnStepClicked()
        {
            if (simulationWorld != null)
            {
                if (!simulationWorld.IsPaused)
                    simulationWorld.Pause();
                simulationWorld.StepOneTick();
            }
        }

        // ================================================================
        //  갱신
        // ================================================================

        private void UpdateInfoText()
        {
            if (_infoText == null || simulationWorld == null || worldEditService == null)
                return;

            ref readonly var element = ref simulationWorld.GetElement(worldEditService.SelectedElementId);
            _infoText.text = $"Tick: {simulationWorld.CurrentTick}\n" +
                             $"Element: {element.Name}\n" +
                             $"State: {(simulationWorld.IsPaused ? "Paused" : "Running")}";
        }

        private void UpdateBrushDisplay()
        {
            if (inputController == null || _brushSlider == null)
                return;

            // 외부(키보드)에서 변경된 값을 슬라이더에 반영
            if (!Mathf.Approximately(_brushSlider.value, inputController.BrushSize))
            {
                _brushSlider.SetValueWithoutNotify(inputController.BrushSize);
                if (_brushSizeText != null)
                    _brushSizeText.text = inputController.BrushSize.ToString();
            }
        }

        private void UpdatePauseButton()
        {
            if (_pauseButtonText == null || simulationWorld == null)
                return;

            _pauseButtonText.text = simulationWorld.IsPaused ? "▶ Play" : "⏸ Pause";
        }

        private void UpdateSelectedHighlight()
        {
            if (worldEditService == null)
                return;

            byte selectedId = worldEditService.SelectedElementId;

            for (int i = 0; i < _elementButtons.Count; i++)
            {
                var entry = _elementButtons[i];
                if (entry.ButtonImage != null)
                {
                    entry.ButtonImage.color = (entry.ElementId == selectedId)
                        ? selectedButtonColor
                        : buttonColor;
                }
            }
        }

        // ================================================================
        //  UI 헬퍼
        // ================================================================

        private RectTransform CreatePanel(Transform parent, string name,
            TextAnchor childAlign, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size)
        {
            GameObject panelObj = new GameObject(name);
            panelObj.transform.SetParent(parent, false);

            RectTransform rt = panelObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            Image bg = panelObj.AddComponent<Image>();
            bg.color = panelColor;

            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 4;
            vlg.childAlignment = childAlign;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = panelObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rt;
        }

        private Text CreateLabel(RectTransform parent, string content,
            TextAnchor alignment, int size, FontStyle style)
        {
            GameObject obj = new GameObject("Label");
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180, size + 8);

            Text text = obj.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = textColor;
            text.alignment = alignment;

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = size + 8;

            return text;
        }

        private Slider CreateSlider(RectTransform parent, float min, float max,
            float initial, UnityEngine.Events.UnityAction<float> callback)
        {
            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(parent, false);

            RectTransform rt = sliderObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180, 20);

            LayoutElement le = sliderObj.AddComponent<LayoutElement>();
            le.preferredHeight = 20;

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRt = bgObj.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.25f);
            bgRt.anchorMax = new Vector2(1, 0.75f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.18f, 1f);

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1, 0.75f);
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = selectedButtonColor;

            // Handle
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRt = handleArea.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(10, 0);
            handleAreaRt.offsetMax = new Vector2(-10, 0);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRt = handle.AddComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(16, 0);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = (min == Mathf.Floor(min) && max == Mathf.Floor(max));
            slider.value = initial;
            slider.onValueChanged.AddListener(callback);

            return slider;
        }

        private Button CreateButton(RectTransform parent, string label,
            UnityEngine.Events.UnityAction callback)
        {
            GameObject btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80, 28);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 80;
            le.preferredHeight = 28;

            Image img = btnObj.AddComponent<Image>();
            img.color = buttonColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;

            btn.onClick.AddListener(callback);

            return btn;
        }

        private RectTransform CreateHorizontalGroup(RectTransform parent, string name, float spacing)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180, 32);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 32;

            HorizontalLayoutGroup hlg = obj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            return rt;
        }
    }
}