using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 디버그 오버레이 렌더러.
    ///
    /// 시뮬레이션 데이터를 색상으로 시각화하는 개발용 오버레이.
    /// 키 입력으로 모드를 전환한다.
    ///
    /// 모드:
    ///   Off      — 오버레이 비활성화
    ///   Mass     — 질량을 히트맵으로 표시 (파랑=0, 빨강=MaxMass)
    ///   Element  — 원소별 고유 색상 (BaseColor, 불투명)
    ///   Temperature — 온도를 히트맵으로 표시 (파랑=저온, 빨강=고온)
    ///   Density  — 원소 밀도를 히트맵으로 표시
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DebugOverlayRenderer : MonoBehaviour, IGridLayerRenderer
    {
        public enum DebugMode
        {
            Off = 0,
            Mass = 1,
            Element = 2,
            Temperature = 3,
            Density = 4
        }

        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Settings")]
        [SerializeField] private DebugMode currentMode = DebugMode.Off;

        [Tooltip("오버레이 투명도")]
        [Range(0.3f, 1f)]
        [SerializeField] private float overlayAlpha = 0.7f;

        [Header("Key Bindings")]
        [Tooltip("디버그 모드 순환 키")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Header("Temperature Range")]
        [Tooltip("히트맵 최저 온도")]
        [SerializeField] private short tempMin = -50;
        [Tooltip("히트맵 최고 온도")]
        [SerializeField] private short tempMax = 300;

        private SimulationWorld _world;
        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _pixels;

        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        public DebugMode CurrentMode => currentMode;

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // ================================================================
        //  IGridLayerRenderer 구현
        // ================================================================

        public void Initialize(SimulationWorld world)
        {
            if (world == null)
                return;

            _world = world;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            EnsureTexture();
            UpdateVisibility();
        }

        public void Refresh()
        {
            if (_world == null || _world.Grid == null)
                return;

            if (currentMode == DebugMode.Off)
                return;

            EnsureTexture();
            RenderOverlay();
        }

        public void RefreshDirty(IReadOnlyList<int> dirtyIndices, int gridWidth)
        {
            Refresh();
        }

        public void Cleanup()
        {
            ReleaseVisuals();
        }

        // ================================================================
        //  Update — 키 입력 처리
        // ================================================================

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                CycleMode();
            }
        }

        /// <summary>
        /// 디버그 모드를 순환한다: Off → Mass → Element → Temperature → Density → Off
        /// </summary>
        public void CycleMode()
        {
            int next = ((int)currentMode + 1) % 5;
            SetMode((DebugMode)next);
        }

        public void SetMode(DebugMode mode)
        {
            currentMode = mode;
            UpdateVisibility();

            if (currentMode != DebugMode.Off)
            {
                Refresh();
                Debug.Log($"[DebugOverlay] Mode: {currentMode}");
            }
            else
            {
                Debug.Log("[DebugOverlay] Off");
            }
        }

        private void UpdateVisibility()
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = currentMode != DebugMode.Off;
        }

        // ================================================================
        //  오버레이 렌더링
        // ================================================================

        private void RenderOverlay()
        {
            WorldGrid grid = _world.Grid;
            int w = grid.Width;
            int h = grid.Height;
            byte alpha = (byte)(overlayAlpha * 255f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    SimCell cell = grid.GetCell(x, y);
                    ref readonly ElementRuntimeDefinition element =
                        ref _world.GetElement(cell.ElementId);

                    int pixelIndex = y * w + x;

                    Color32 color;

                    switch (currentMode)
                    {
                        case DebugMode.Mass:
                            color = GetMassColor(in cell, in element, alpha);
                            break;
                        case DebugMode.Element:
                            color = GetElementColor(in element, alpha);
                            break;
                        case DebugMode.Temperature:
                            color = GetTemperatureColor(in cell, alpha);
                            break;
                        case DebugMode.Density:
                            color = GetDensityColor(in element, alpha);
                            break;
                        default:
                            color = Transparent;
                            break;
                    }

                    _pixels[pixelIndex] = color;
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        // ================================================================
        //  모드별 색상 계산
        // ================================================================

        /// <summary>
        /// 질량 히트맵: 파랑(0) → 초록(중간) → 빨강(MaxMass)
        /// 진공은 투명.
        /// </summary>
        private Color32 GetMassColor(in SimCell cell, in ElementRuntimeDefinition element, byte alpha)
        {
            if (element.BehaviorType == ElementBehaviorType.Vacuum)
                return Transparent;

            if (element.MaxMass <= 0)
                return new Color32(128, 128, 128, alpha); // 질량 개념 없는 원소 (StaticSolid)

            float ratio = Mathf.Clamp01((float)cell.Mass / element.MaxMass);
            return HeatmapColor(ratio, alpha);
        }

        /// <summary>
        /// 원소별 고유 색상: BaseColor를 불투명하게 표시.
        /// 각 원소의 분포를 한눈에 확인할 수 있다.
        /// </summary>
        private Color32 GetElementColor(in ElementRuntimeDefinition element, byte alpha)
        {
            if (element.BehaviorType == ElementBehaviorType.Vacuum)
                return Transparent;

            return new Color32(
                element.BaseColor.r,
                element.BaseColor.g,
                element.BaseColor.b,
                alpha);
        }

        /// <summary>
        /// 온도 히트맵: 파랑(저온) → 초록(상온) → 빨강(고온)
        /// </summary>
        private Color32 GetTemperatureColor(in SimCell cell, byte alpha)
        {
            float range = tempMax - tempMin;
            if (range <= 0) range = 1;

            float ratio = Mathf.Clamp01((cell.Temperature - tempMin) / range);
            return HeatmapColor(ratio, alpha);
        }

        /// <summary>
        /// 밀도 히트맵: 원소의 Density를 시각화.
        /// 가벼운 기체(파랑) → 무거운 고체(빨강)
        /// </summary>
        private Color32 GetDensityColor(in ElementRuntimeDefinition element, byte alpha)
        {
            if (element.BehaviorType == ElementBehaviorType.Vacuum)
                return Transparent;

            // 밀도 범위: 0 ~ 10000 (대략적 범위)
            float ratio = Mathf.Clamp01(element.Density / 10000f);
            return HeatmapColor(ratio, alpha);
        }

        // ================================================================
        //  히트맵 색상 유틸
        // ================================================================

        /// <summary>
        /// 0~1 비율을 파랑 → 시안 → 초록 → 노랑 → 빨강 히트맵으로 변환한다.
        /// </summary>
        private static Color32 HeatmapColor(float ratio, byte alpha)
        {
            // 5단계 히트맵
            float r, g, b;

            if (ratio < 0.25f)
            {
                // 파랑 → 시안
                float t = ratio / 0.25f;
                r = 0f; g = t; b = 1f;
            }
            else if (ratio < 0.5f)
            {
                // 시안 → 초록
                float t = (ratio - 0.25f) / 0.25f;
                r = 0f; g = 1f; b = 1f - t;
            }
            else if (ratio < 0.75f)
            {
                // 초록 → 노랑
                float t = (ratio - 0.5f) / 0.25f;
                r = t; g = 1f; b = 0f;
            }
            else
            {
                // 노랑 → 빨강
                float t = (ratio - 0.75f) / 0.25f;
                r = 1f; g = 1f - t; b = 0f;
            }

            return new Color32(
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255),
                alpha);
        }

        // ================================================================
        //  텍스처 관리
        // ================================================================

        private void EnsureTexture()
        {
            if (_world == null || _world.Grid == null)
                return;

            int w = _world.Grid.Width;
            int h = _world.Grid.Height;

            if (_texture != null &&
                _texture.width == w &&
                _texture.height == h &&
                _sprite != null)
            {
                return;
            }

            ReleaseVisuals();

            _texture = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                filterMode = FilterMode.Point,  // 디버그용이므로 셀 경계 선명하게
                wrapMode = TextureWrapMode.Clamp,
                name = $"DebugOverlayTexture_{w}x{h}"
            };

            _pixels = new Color32[w * h];

            _sprite = Sprite.Create(
                texture: _texture,
                rect: new Rect(0, 0, w, h),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);

            _sprite.name = $"DebugOverlaySprite_{w}x{h}";
            spriteRenderer.sprite = _sprite;

            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
        }

        // ================================================================
        //  리소스 해제
        // ================================================================

        private void ReleaseVisuals()
        {
            if (spriteRenderer != null)
                spriteRenderer.sprite = null;

            if (_sprite != null)
            {
                DestroyObject(_sprite);
                _sprite = null;
            }

            if (_texture != null)
            {
                DestroyObject(_texture);
                _texture = null;
            }

            _pixels = null;
        }

        private void OnDestroy()
        {
            ReleaseVisuals();
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null) return;

            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}