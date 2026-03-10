using Core.Simulation.Data;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GridRenderer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Min(1)]
        [SerializeField] private int pixelsPerUnit = 1;

        [SerializeField] private bool refreshEveryFrame = false;

        private SimulationWorld _world;
        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _pixels;

        public bool IsInitialized => _world != null && _texture != null;

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            Debug.Log("GridRenderer OnEnable", this);
        }

        public void Initialize(SimulationWorld world)
        {
            Debug.Log("GridRenderer Initialize START", this);

            if (world == null)
            {
                Debug.LogError("GridRenderer.Initialize: world is null.", this);
                return;
            }

            _world = world;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            Debug.Log($"SpriteRenderer found? {spriteRenderer != null}", this);

            EnsureTexture();

            Debug.Log("GridRenderer Initialize END", this);
        }

        private void LateUpdate()
        {
            if (!refreshEveryFrame || !IsInitialized)
                return;

            RefreshAll();
        }

        public void RefreshAll()
        {
            if (_world == null || _world.Grid == null || _world.ElementRegistry == null)
                return;

            EnsureTexture();

            WorldGrid grid = _world.Grid;
            int width = grid.Width;
            int height = grid.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    SimCell cell = grid.GetCell(x, y);
                    ref readonly var element = ref _world.GetElement(cell.ElementId);

                    int pixelIndex = y * width + x;
                    _pixels[pixelIndex] = element.BaseColor;
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

            int width = _world.Grid.Width;
            int height = _world.Grid.Height;

            x = Mathf.FloorToInt(local.x + (width * 0.5f));
            y = Mathf.FloorToInt(local.y + (height * 0.5f));

            return _world.Grid.InBounds(x, y);
        }

        private void EnsureTexture()
        {
            Debug.Log("EnsureTexture called", this);

            if (_world == null || _world.Grid == null)
            {
                Debug.LogWarning($"EnsureTexture aborted. world null? {_world == null}, grid null? {_world?.Grid == null}", this);
                return;
            }

            int width = _world.Grid.Width;
            int height = _world.Grid.Height;

            Debug.Log($"Creating texture {width}x{height}", this);

            if (_texture != null &&
                _texture.width == width &&
                _texture.height == height &&
                _sprite != null)
            {
                Debug.Log("Texture already valid, skip recreate", this);
                return;
            }

            ReleaseVisuals();

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"SimulationGridTexture_{width}x{height}"
            };

            _pixels = new Color32[width * height];

            _sprite = Sprite.Create(
                texture: _texture,
                rect: new Rect(0, 0, width, height),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: pixelsPerUnit);

            _sprite.name = $"SimulationGridSprite_{width}x{height}";
            spriteRenderer.sprite = _sprite;

            Debug.Log("Sprite assigned to SpriteRenderer", this);
        }

        private void OnDestroy()
        {
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