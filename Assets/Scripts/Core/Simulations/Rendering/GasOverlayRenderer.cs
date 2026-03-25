using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 기체 레이어 렌더러 (Texture2D + 커스텀 셰이더).
    ///
    /// Phase 4: GasOverlay 셰이더로 가스 구름 효과를 구현한다.
    ///   - CPU: 셀당 1픽셀 데이터 텍스처 (BaseColor + 질량 기반 alpha)
    ///   - GPU: Noise UV 왜곡(흔들림) + 9-tap 블러(부드러운 경계)
    ///   - Inspector에서 noiseSpeed, noiseStrength, blurRadius 실시간 조절
    ///
    /// 셰이더가 없거나 로드 실패 시 Sprites/Default로 자동 폴백.
    ///
    /// 처리 대상: ElementBehaviorType.Gas
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GasOverlayRenderer : MonoBehaviour, IGridLayerRenderer
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Transparency")]
        [Tooltip("기체의 최소 투명도 (질량 0일 때)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float minAlpha = 0.05f;

        [Tooltip("기체의 최대 투명도 (질량 MaxMass일 때)")]
        [Range(0.3f, 0.8f)]
        [SerializeField] private float maxAlpha = 0.6f;

        [Header("Shader Effects")]
        [Tooltip("노이즈 흔들림 속도. 0이면 정적.")]
        [Range(0f, 2f)]
        [SerializeField] private float noiseSpeed = 0.3f;

        [Tooltip("노이즈 흔들림 강도 (UV offset 범위).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float noiseStrength = 0.08f;

        [Tooltip("노이즈 스케일. 클수록 작은 패턴, 작을수록 큰 패턴.")]
        [Range(1f, 20f)]
        [SerializeField] private float noiseScale = 5f;

        [Tooltip("블러 반경 (텍셀 단위). 0이면 블러 없음.")]
        [Range(0f, 3f)]
        [SerializeField] private float blurRadius = 1f;

        [Header("Cloud Pattern")]
        [Tooltip("구름 패턴 텍스처. null이면 구름 효과 없이 기존 방식으로 동작.")]
        [SerializeField] private Texture2D cloudTexture;

        [Tooltip("구름 패턴 스케일. 클수록 작은 구름, 작을수록 큰 구름.")]
        [Range(0.5f, 8f)]
        [SerializeField] private float cloudScale = 2.0f;

        [Tooltip("구름 드리프트 속도. 구름이 천천히 흘러가는 효과.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float cloudDrift = 0.05f;

        [Tooltip("구름 대비. 높을수록 덩어리가 뚜렷해짐.")]
        [Range(0.5f, 3f)]
        [SerializeField] private float cloudContrast = 1.5f;

        [Tooltip("경계 페이드 강도. 높을수록 가장자리가 부드럽게 사라짐.")]
        [Range(0f, 2f)]
        [SerializeField] private float edgeFade = 0.8f;

        private SimulationWorld _world;
        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _pixels;
        private Material _material;
        private bool _hasCustomShader;

        // 셰이더 프로퍼티 ID 캐시 (SetFloat 호출 최적화)
        private static readonly int PropNoiseSpeed = Shader.PropertyToID("_NoiseSpeed");
        private static readonly int PropNoiseStrength = Shader.PropertyToID("_NoiseStrength");
        private static readonly int PropNoiseScale = Shader.PropertyToID("_NoiseScale");
        private static readonly int PropBlurRadius = Shader.PropertyToID("_BlurRadius");
        private static readonly int PropCloudTex = Shader.PropertyToID("_CloudTex");
        private static readonly int PropCloudScale = Shader.PropertyToID("_CloudScale");
        private static readonly int PropCloudDrift = Shader.PropertyToID("_CloudDrift");
        private static readonly int PropCloudContrast = Shader.PropertyToID("_CloudContrast");
        private static readonly int PropEdgeFade = Shader.PropertyToID("_EdgeFade");

        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

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

            EnsureMaterial();
            EnsureTexture();
        }

        public void Refresh()
        {
            if (_world == null || _world.Grid == null || _world.ElementRegistry == null)
                return;

            EnsureTexture();
            UpdateDataTexture();
            UpdateShaderParameters();
        }

        public void Cleanup()
        {
            ReleaseVisuals();
        }

        // ================================================================
        //  데이터 텍스처 갱신 (CPU 측)
        // ================================================================

        private void UpdateDataTexture()
        {
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

                    if (element.BehaviorType == ElementBehaviorType.Gas && cell.Mass > 0)
                    {
                        float ratio = Mathf.Clamp01((float)cell.Mass / element.MaxMass);
                        byte alpha = (byte)(Mathf.Lerp(minAlpha, maxAlpha, ratio) * 255f);

                        _pixels[pixelIndex] = new Color32(
                            element.BaseColor.r,
                            element.BaseColor.g,
                            element.BaseColor.b,
                            alpha);
                    }
                    else
                    {
                        _pixels[pixelIndex] = Transparent;
                    }
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        // ================================================================
        //  셰이더 파라미터 갱신 (GPU 측)
        // ================================================================

        private void UpdateShaderParameters()
        {
            if (_material == null || !_hasCustomShader)
                return;

            _material.SetFloat(PropNoiseSpeed, noiseSpeed);
            _material.SetFloat(PropNoiseStrength, noiseStrength);
            _material.SetFloat(PropNoiseScale, noiseScale);
            _material.SetFloat(PropBlurRadius, blurRadius);

            if (cloudTexture != null)
            {
                _material.SetTexture(PropCloudTex, cloudTexture);
            }
            _material.SetFloat(PropCloudScale, cloudScale);
            _material.SetFloat(PropCloudDrift, cloudDrift);
            _material.SetFloat(PropCloudContrast, cloudContrast);
            _material.SetFloat(PropEdgeFade, edgeFade);
        }

        // ================================================================
        //  Material 관리
        // ================================================================

        private void EnsureMaterial()
        {
            // 커스텀 셰이더 탐색
            Shader gasShader = Shader.Find("Simulation/GasOverlay");

            if (gasShader != null)
            {
                _material = new Material(gasShader)
                {
                    name = "GasOverlayMaterial"
                };
                _hasCustomShader = true;
                Debug.Log("GasOverlayRenderer: Custom shader loaded.", this);
            }
            else
            {
                // 폴백: 기본 Sprite 셰이더 (Bilinear 보간만 동작)
                _material = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "GasOverlayMaterial_Fallback"
                };
                _hasCustomShader = false;
                Debug.LogWarning(
                    "GasOverlayRenderer: 'Simulation/GasOverlay' shader not found. " +
                    "Falling back to Sprites/Default. Noise and blur effects will be disabled.",
                    this);
            }

            spriteRenderer.material = _material;
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

            ReleaseTextureAndSprite();

            // 셀당 1픽셀 데이터 텍스처
            // Bilinear 필터링 + 셰이더 블러로 부드러운 가스 경계
            _texture = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"GasOverlayTexture_{w}x{h}"
            };

            _pixels = new Color32[w * h];

            _sprite = Sprite.Create(
                texture: _texture,
                rect: new Rect(0, 0, w, h),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);

            _sprite.name = $"GasOverlaySprite_{w}x{h}";
            spriteRenderer.sprite = _sprite;

            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
        }

        // ================================================================
        //  리소스 해제
        // ================================================================

        private void ReleaseTextureAndSprite()
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

        private void ReleaseVisuals()
        {
            ReleaseTextureAndSprite();

            if (_material != null)
            {
                SafeDestroy(_material);
                _material = null;
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