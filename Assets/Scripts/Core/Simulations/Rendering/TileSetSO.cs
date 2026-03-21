using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 47-타일 오토타일 스프라이트 세트.
    ///
    /// 8방향 비트마스크 기반 47가지 타일 변형을 담는다.
    /// 대각선 마스킹 적용 후 유효한 조합만 47개이며,
    /// 각 인덱스는 TileBitmaskUtility.Compute47()의 반환값에 대응한다.
    ///
    /// null 슬롯은 PlaceholderTileGenerator의 폴백으로 처리된다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TileSet",
        menuName = "Simulation/Tile Set")]
    public sealed class TileSetSO : ScriptableObject
    {
        [Tooltip("47-타일 인덱스(0~46) → 스프라이트 매핑")]
        [SerializeField] private Sprite[] tiles = new Sprite[TileBitmaskUtility.TileCount47];

        /// <summary>
        /// 47-타일 인덱스에 해당하는 스프라이트를 반환한다.
        /// </summary>
        public Sprite GetTile(byte index47)
        {
            if (index47 < tiles.Length)
                return tiles[index47];

            return null;
        }

        /// <summary>
        /// 하위 호환: 4방향 비트마스크(0~15)로 조회.
        /// 4방향 마스크를 8방향 직선 성분으로 변환하여 47 인덱스를 찾는다.
        /// </summary>
        public Sprite GetTileByBitmask4(byte bitmask4)
        {
            // 4방향 비트 → 8방향 직선 비트 변환
            // 4방향: Up=1, Left=2, Right=4, Down=8
            // 8방향: N=1, E=4, S=16, W=64
            byte mask8 = 0;
            if ((bitmask4 & 1) != 0) mask8 |= TileBitmaskUtility.N;
            if ((bitmask4 & 2) != 0) mask8 |= TileBitmaskUtility.W;
            if ((bitmask4 & 4) != 0) mask8 |= TileBitmaskUtility.E;
            if ((bitmask4 & 8) != 0) mask8 |= TileBitmaskUtility.S;

            // 직선 성분만 있는 마스크로 47 인덱스 조회
            byte index47 = TileBitmaskUtility.GetMaskForIndex47(0); // 폴백
            for (int i = 0; i < TileBitmaskUtility.TileCount47; i++)
            {
                if (TileBitmaskUtility.GetMaskForIndex47(i) == mask8)
                {
                    index47 = (byte)i;
                    break;
                }
            }

            return GetTile(index47);
        }

        public bool IsComplete
        {
            get
            {
                if (tiles == null || tiles.Length < TileBitmaskUtility.TileCount47)
                    return false;

                for (int i = 0; i < TileBitmaskUtility.TileCount47; i++)
                {
                    if (tiles[i] == null)
                        return false;
                }

                return true;
            }
        }

#if UNITY_EDITOR
        public void SetTilesForEditor(Sprite[] sprites)
        {
            if (sprites == null || sprites.Length < TileBitmaskUtility.TileCount47)
                return;

            if (tiles == null || tiles.Length < TileBitmaskUtility.TileCount47)
                tiles = new Sprite[TileBitmaskUtility.TileCount47];

            for (int i = 0; i < TileBitmaskUtility.TileCount47; i++)
                tiles[i] = sprites[i];
        }

        private void OnValidate()
        {
            if (tiles == null || tiles.Length != TileBitmaskUtility.TileCount47)
            {
                var old = tiles;
                tiles = new Sprite[TileBitmaskUtility.TileCount47];
                if (old != null)
                {
                    for (int i = 0; i < Mathf.Min(old.Length, tiles.Length); i++)
                        tiles[i] = old[i];
                }
            }
        }
#endif
    }
}