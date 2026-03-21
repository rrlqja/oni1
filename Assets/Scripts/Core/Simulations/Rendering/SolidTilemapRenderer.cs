using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 고체 레이어 렌더러 (Tilemap + 8방향 47-타일 오토타일링).
    ///
    /// Phase 5-2: 4방향(16타일) → 8방향(47타일) 확장.
    ///   - 대각선 모서리(내부 코너)까지 자연스럽게 처리
    ///   - TileSetSO 47슬롯 지원
    ///   - Dirty 부분 갱신 + 인접 8셀 재계산
    ///
    /// 처리 대상: ElementBehaviorType.StaticSolid, FallingSolid
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap))]
    [RequireComponent(typeof(TilemapRenderer))]
    public sealed class SolidTilemapRenderer : MonoBehaviour, IGridLayerRenderer
    {
        [SerializeField] private Tilemap tilemap;
        [SerializeField] private TilemapRenderer tilemapRenderer;

        [Header("Tile Sets (선택)")]
        [Tooltip("원소별 오토타일 세트 (47-타일). null이면 플레이스홀더를 자동 생성.")]
        [SerializeField] private TileSetSO[] elementTileSets = new TileSetSO[256];

        private SimulationWorld _world;

        // 원소 × 47인덱스 → Tile 캐시
        // _tileCache[elementId * 47 + index47]
        private Tile[] _tileCache;

        // 플레이스홀더 스프라이트 캐시 (원소별, 47개)
        private Sprite[][] _placeholderSprites = new Sprite[256][];

        // 부분 갱신 시 중복 처리 방지용
        private readonly HashSet<int> _cellsToUpdate = new HashSet<int>();

        private void Reset()
        {
            tilemap = GetComponent<Tilemap>();
            tilemapRenderer = GetComponent<TilemapRenderer>();
        }

        // ================================================================
        //  IGridLayerRenderer 구현
        // ================================================================

        public void Initialize(SimulationWorld world)
        {
            if (world == null)
                return;

            _world = world;

            if (tilemap == null)
                tilemap = GetComponent<Tilemap>();

            if (tilemapRenderer == null)
                tilemapRenderer = GetComponent<TilemapRenderer>();

            // 256 원소 × 47 타일 = 12,032 슬롯
            _tileCache = new Tile[256 * TileBitmaskUtility.TileCount47];

            int w = _world.Grid.Width;
            int h = _world.Grid.Height;
            transform.localPosition = new Vector3(-w * 0.5f, -h * 0.5f, 0f);
        }

        public void Refresh()
        {
            if (_world == null || _world.Grid == null || _world.ElementRegistry == null)
                return;

            WorldGrid grid = _world.Grid;
            int w = grid.Width;
            int h = grid.Height;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    UpdateTileAt(grid, x, y);
        }

        public void RefreshDirty(IReadOnlyList<int> dirtyIndices, int gridWidth)
        {
            if (_world == null || _world.Grid == null || _world.ElementRegistry == null)
                return;

            WorldGrid grid = _world.Grid;
            int w = grid.Width;
            int h = grid.Height;

            _cellsToUpdate.Clear();

            for (int i = 0; i < dirtyIndices.Count; i++)
            {
                int index = dirtyIndices[i];
                int x = index % w;
                int y = index / w;

                // 자신 + 인접 8셀 (대각선 비트마스크가 바뀔 수 있으므로)
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                            _cellsToUpdate.Add(ny * w + nx);
                    }
                }
            }

            foreach (int idx in _cellsToUpdate)
            {
                int cx = idx % w;
                int cy = idx / w;
                UpdateTileAt(grid, cx, cy);
            }
        }

        // ================================================================
        //  타일 갱신
        // ================================================================

        private void UpdateTileAt(WorldGrid grid, int x, int y)
        {
            SimCell cell = grid.GetCell(x, y);
            ref readonly ElementRuntimeDefinition element =
                ref _world.GetElement(cell.ElementId);

            Vector3Int tilePos = new Vector3Int(x, y, 0);

            if (element.IsSolid)
            {
                byte index47 = TileBitmaskUtility.Compute47(grid, x, y);
                Tile tile = GetOrCreateTile(cell.ElementId, index47, element.BaseColor);
                tilemap.SetTile(tilePos, tile);
            }
            else
            {
                if (tilemap.HasTile(tilePos))
                    tilemap.SetTile(tilePos, null);
            }
        }

        // ================================================================
        //  Tile 생성 / 캐싱
        // ================================================================

        private Tile GetOrCreateTile(byte elementId, byte index47, Color32 baseColor)
        {
            int cacheIndex = elementId * TileBitmaskUtility.TileCount47 + index47;

            if (_tileCache[cacheIndex] != null)
                return _tileCache[cacheIndex];

            Sprite sprite = ResolveSprite(elementId, index47, baseColor);

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.name = $"SolidTile47_{elementId}_{index47:D2}";

            _tileCache[cacheIndex] = tile;
            return tile;
        }

        private Sprite ResolveSprite(byte elementId, byte index47, Color32 baseColor)
        {
            // TileSetSO에서 조회
            if (elementId < elementTileSets.Length && elementTileSets[elementId] != null)
            {
                Sprite sprite = elementTileSets[elementId].GetTile(index47);
                if (sprite != null)
                    return sprite;
            }

            // 플레이스홀더 자동 생성
            return GetOrCreatePlaceholderSprite(elementId, index47, baseColor);
        }

        private Sprite GetOrCreatePlaceholderSprite(byte elementId, byte index47, Color32 baseColor)
        {
            if (_placeholderSprites[elementId] == null)
                _placeholderSprites[elementId] = PlaceholderTileGenerator.Generate(baseColor);

            if (index47 < _placeholderSprites[elementId].Length)
                return _placeholderSprites[elementId][index47];

            return _placeholderSprites[elementId][0]; // 폴백
        }

        // ================================================================
        //  리소스 해제
        // ================================================================

        private void ReleaseVisuals()
        {
            if (tilemap != null)
                tilemap.ClearAllTiles();

            if (_tileCache != null)
            {
                for (int i = 0; i < _tileCache.Length; i++)
                {
                    if (_tileCache[i] != null)
                    {
                        DestroyObject(_tileCache[i]);
                        _tileCache[i] = null;
                    }
                }
            }

            for (int i = 0; i < _placeholderSprites.Length; i++)
            {
                if (_placeholderSprites[i] != null)
                {
                    PlaceholderTileGenerator.Destroy(_placeholderSprites[i]);
                    _placeholderSprites[i] = null;
                }
            }
        }

        public void Cleanup()
        {
            ReleaseVisuals();
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