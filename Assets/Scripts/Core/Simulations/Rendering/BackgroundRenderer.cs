using System.Collections.Generic;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 배경 레이어 렌더러.
    ///
    /// Phase 1: 월드 전체를 단색으로 채우는 단순 배경.
    /// 향후: 배경 타일 데이터에 따라 동굴 벽면/우주 공간 등을 구분하여 렌더링.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BackgroundRenderer : MonoBehaviour, IGridLayerRenderer
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("배경 색상 (진공/기본 배경)")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);

        private SimulationWorld _world;
        private Texture2D _texture;
        private Sprite _sprite;

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
            // Phase 1: 정적 배경이므로 매 틱 갱신 불필요.
            // 월드 크기가 변경되었을 때만 재생성.
            if (_world == null || _world.Grid == null)
                return;

            if (_texture != null &&
                _texture.width == _world.Grid.Width &&
                _texture.height == _world.Grid.Height)
                return;

            CreateBackground();
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

            // 1×1 단색 텍스처를 월드 크기로 확대
            _texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BackgroundTexture"
            };

            _texture.SetPixel(0, 0, backgroundColor);
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            // 월드 크기에 맞는 Sprite 생성 (pixelsPerUnit=1이면 1x1 텍스처가 1x1 유닛)
            // Sprite의 크기를 width×height로 만들기 위해 Rect를 (0,0,1,1)로 하고
            // SpriteRenderer의 drawMode=Tiled 또는 transform.localScale로 확대
            _sprite = Sprite.Create(
                texture: _texture,
                rect: new Rect(0, 0, 1, 1),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);

            _sprite.name = "BackgroundSprite";
            spriteRenderer.sprite = _sprite;

            // 월드 크기에 맞게 스케일 조정
            transform.localScale = new Vector3(w, h, 1f);
            // 부모(GridRenderManager) 기준 로컬 위치는 (0,0)
            // GridRenderManager가 월드 중앙에 있으므로 배경도 중앙 정렬
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

        public void RefreshDirty(IReadOnlyList<int> dirtyIndices, int gridWidth)
        {
            Refresh();
        }
    }
}