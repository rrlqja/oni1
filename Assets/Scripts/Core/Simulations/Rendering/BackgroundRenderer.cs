using System.Collections.Generic;
using Core.Simulation.Runtime;
using Core.Simulation.Runtime.WorldGeneration;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 배경 레이어 렌더러.
    ///
    /// WorldGenerator가 있으면 바이옴별 배경색을 셀 단위로 렌더링.
    /// 없으면 기존처럼 단색 배경.
    ///
    /// ONI처럼 원소를 지우면 바이옴 배경색이 보이는 효과.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BackgroundRenderer : MonoBehaviour, IGridLayerRenderer
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("기본 배경 색상 (바이옴 없을 때)")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);

        private SimulationWorld _world;
        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _pixels;

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

            CreateBackground();
        }

        public void Refresh()
        {
            if (_world == null || _world.Grid == null)
                return;

            int w = _world.Grid.Width;
            int h = _world.Grid.Height;

            // 크기 변경 시 재생성
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                CreateBackground();
                return;
            }
        }

        public void RefreshDirty(IReadOnlyList<int> dirtyIndices, int gridWidth)
        {
            // 배경은 정적 — 생성 시 한 번만 그림
        }

        public void Cleanup()
        {
            ReleaseVisuals();
        }

        // ================================================================
        //  배경 생성
        // ================================================================

        private void CreateBackground()
        {
            if (_world == null || _world.Grid == null)
                return;

            ReleaseVisuals();

            int w = _world.Grid.Width;
            int h = _world.Grid.Height;

            var generator = _world.LastWorldGenerator;

            if (generator != null && generator.BiomeMap != null && generator.BiomeList.Count > 0)
            {
                // 바이옴 배경색 모드: 셀 단위 텍스처
                CreateBiomeBackground(w, h, generator);
            }
            else
            {
                // 폴백: 단색 배경
                CreateSolidBackground(w, h);
            }
        }

        /// <summary>
        /// 바이옴별 배경색을 셀 단위로 렌더링.
        /// </summary>
        private void CreateBiomeBackground(int w, int h, WorldGenerator generator)
        {
            _texture = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"BiomeBackground_{w}x{h}"
            };

            _pixels = new Color32[w * h];

            for (int i = 0; i < _pixels.Length; i++)
            {
                Color c = generator.GetBiomeColor(i);
                _pixels[i] = c;
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            _sprite = Sprite.Create(
                texture: _texture,
                rect: new Rect(0, 0, w, h),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);

            _sprite.name = "BiomeBackgroundSprite";
            spriteRenderer.sprite = _sprite;

            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// 폴백: 단색 배경 (WorldGenerator 없을 때).
        /// </summary>
        private void CreateSolidBackground(int w, int h)
        {
            _texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BackgroundTexture"
            };

            _texture.SetPixel(0, 0, backgroundColor);
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            _sprite = Sprite.Create(
                texture: _texture,
                rect: new Rect(0, 0, 1, 1),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);

            _sprite.name = "BackgroundSprite";
            spriteRenderer.sprite = _sprite;

            transform.localScale = new Vector3(w, h, 1f);
            transform.localPosition = Vector3.zero;
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
                SafeDestroy(_sprite);
                _sprite = null;
            }

            if (_texture != null)
            {
                SafeDestroy(_texture);
                _texture = null;
            }

            _pixels = null;
        }

        private void OnDestroy()
        {
            ReleaseVisuals();
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;

            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}