using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 시뮬레이션 그리드를 Texture2D로 렌더링한다.
    ///
    /// 개선사항:
    ///   - 이벤트 기반 갱신: SimulationWorld.OnTickCompleted 구독
    ///   - Dirty 플래그: 변경이 있을 때만 텍스처 갱신 (매 프레임 낭비 제거)
    ///   - 질량 기반 색상: 액체/기체의 질량에 따라 색상 농도가 변한다
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GridRenderer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Min(1)]
        [SerializeField] private int pixelsPerUnit = 1;

        [Tooltip("배경(진공) 색상")]
        [SerializeField] private Color32 vacuumColor = new Color32(20, 20, 25, 255);

        [Tooltip("액체/기체의 최소 밝기 (질량 0일 때). 0.0 ~ 1.0")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float minMassBrightness = 0.15f;

        private SimulationWorld _world;
        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _pixels;
        private bool _needsRefresh;

        public bool IsInitialized => _world != null && _texture != null;

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(SimulationWorld world)
        {
            if (world == null)
            {
                Debug.LogError("GridRenderer.Initialize: world is null.", this);
                return;
            }

            // 이전 구독 해제 (재초기화 대비)
            UnsubscribeEvents();

            _world = world;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            EnsureTexture();

            // 이벤트 구독
            SubscribeEvents();
        }

        private void LateUpdate()
        {
            if (!_needsRefresh || !IsInitialized)
                return;

            _needsRefresh = false;
            RefreshAll();
        }

        /// <summary>
        /// 다음 LateUpdate에서 텍스처를 갱신하도록 마킹한다.
        /// WorldEditService 등 외부에서 셀을 직접 수정한 후 호출.
        /// </summary>
        public void MarkDirty()
        {
            _needsRefresh = true;
        }

        /// <summary>
        /// 즉시 전체 텍스처를 갱신한다.
        /// 초기화 시점이나 강제 갱신이 필요할 때 사용.
        /// </summary>
        public void RefreshAll()
        {
            if (_world == null || _world.Grid == null || _world.ElementRegistry == null)
                return;

            EnsureTexture();

            WorldGrid grid = _world.Grid;
            int w = grid.Width;
            int h = grid.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    SimCell cell = grid.GetCell(x, y);
                    ref readonly ElementRuntimeDefinition element =
                        ref _world.GetElement(cell.ElementId);

                    int pixelIndex = y * w + x;
                    _pixels[pixelIndex] = CalculateCellColor(in cell, in element);
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        public bool TryWorldToCell(Vector3 worldPosition, out int x, out int y)
        {
            x = -1;
            y = -1;

            if (_world == null || _world.Grid == null)
                return false;

            Vector3 local = transform.InverseTransformPoint(worldPosition);

            int w = _world.Grid.Width;
            int h = _world.Grid.Height;

            x = Mathf.FloorToInt(local.x + (w * 0.5f));
            y = Mathf.FloorToInt(local.y + (h * 0.5f));

            return _world.Grid.InBounds(x, y);
        }

        // ================================================================
        //  셀 색상 계산
        // ================================================================

        /// <summary>
        /// 셀의 현재 상태에 따라 렌더링 색상을 계산한다.
        ///
        /// - Vacuum: vacuumColor (어두운 배경)
        /// - 고체: BaseColor 그대로 (질량 변화가 시각적 의미 없음)
        /// - 액체/기체: BaseColor × 질량 기반 밝기
        ///   mass=0 → minMassBrightness, mass=MaxMass → 1.0
        /// </summary>
        private Color32 CalculateCellColor(
            in SimCell cell,
            in ElementRuntimeDefinition element)
        {
            // 진공: 배경색
            if (element.BehaviorType == ElementBehaviorType.Vacuum)
                return vacuumColor;

            // 고체: BaseColor 그대로
            if (element.IsSolid)
                return element.BaseColor;

            // 액체/기체: 질량 기반 밝기
            int maxMass = element.MaxMass;
            if (maxMass <= 0)
                return element.BaseColor;

            float ratio = cell.Mass / (float)maxMass;

            // minMassBrightness ~ 1.0 범위로 보간
            // ratio=0 → minBrightness, ratio>=1 → 1.0
            float brightness = Mathf.Clamp01(ratio) * (1f - minMassBrightness) + minMassBrightness;

            Color32 baseColor = element.BaseColor;
            return new Color32(
                (byte)(baseColor.r * brightness),
                (byte)(baseColor.g * brightness),
                (byte)(baseColor.b * brightness),
                baseColor.a);
        }

        // ================================================================
        //  이벤트 구독
        // ================================================================

        private void SubscribeEvents()
        {
            if (_world == null)
                return;

            _world.OnTickCompleted += HandleTickCompleted;
            _world.OnWorldModified += HandleWorldModified;
        }

        private void UnsubscribeEvents()
        {
            if (_world == null)
                return;

            _world.OnTickCompleted -= HandleTickCompleted;
            _world.OnWorldModified -= HandleWorldModified;
        }

        private void HandleTickCompleted()
        {
            _needsRefresh = true;
        }

        private void HandleWorldModified()
        {
            _needsRefresh = true;
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
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"SimulationGridTexture_{w}x{h}"
            };

            _pixels = new Color32[w * h];

            _sprite = Sprite.Create(
                texture: _texture,
                rect: new Rect(0, 0, w, h),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: pixelsPerUnit);

            _sprite.name = $"SimulationGridSprite_{w}x{h}";
            spriteRenderer.sprite = _sprite;
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            ReleaseVisuals();
        }

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

        private static void DestroyObject(Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}