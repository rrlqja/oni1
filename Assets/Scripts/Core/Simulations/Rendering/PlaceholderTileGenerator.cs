using UnityEngine;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// BaseColor에서 47가지 오토타일 플레이스홀더 스프라이트를 런타임 생성한다.
    ///
    /// 8방향 비트마스크 기반:
    ///   - 이웃 없는 직선 방향 → 어두운 테두리 (2px)
    ///   - 이웃 없는 대각선 → 모서리에 어두운 삼각형 (내부 코너)
    ///   - 완전 내부 (인덱스 46) → 테두리 없음
    /// </summary>
    public static class PlaceholderTileGenerator
    {
        private const int TileSize = 16;
        private const int BorderWidth = 2;
        private const int CornerSize = 6;  // 대각선 모서리 삼각형 크기
        private const float BorderDarken = 0.4f;
        private const float InnerBrighten = 1.0f;

        /// <summary>
        /// BaseColor로 47가지 타일 스프라이트를 생성한다.
        /// 반환 배열의 인덱스 = 47-타일 인덱스 (0~46).
        /// </summary>
        public static Sprite[] Generate(Color32 baseColor)
        {
            Sprite[] sprites = new Sprite[TileBitmaskUtility.TileCount47];

            for (int i = 0; i < TileBitmaskUtility.TileCount47; i++)
            {
                byte mask = TileBitmaskUtility.GetMaskForIndex47(i);
                Texture2D tex = CreateTileTexture(baseColor, mask);
                sprites[i] = CreateSprite(tex, i);
            }

            return sprites;
        }

        public static void Destroy(Sprite[] sprites)
        {
            if (sprites == null)
                return;

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                    continue;

                Texture2D tex = sprites[i].texture;

                if (Application.isPlaying)
                {
                    Object.Destroy(sprites[i]);
                    if (tex != null) Object.Destroy(tex);
                }
                else
                {
                    Object.DestroyImmediate(sprites[i]);
                    if (tex != null) Object.DestroyImmediate(tex);
                }

                sprites[i] = null;
            }
        }

        // ================================================================
        //  텍스처 생성
        // ================================================================

        private static Texture2D CreateTileTexture(Color32 baseColor, byte mask)
        {
            var tex = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"PlaceholderTile47_{mask:D3}"
            };

            Color32 inner = ApplyBrightness(baseColor, InnerBrighten);
            Color32 border = ApplyBrightness(baseColor, BorderDarken);

            // 직선 이웃 여부
            bool hasN = (mask & TileBitmaskUtility.N) != 0;
            bool hasE = (mask & TileBitmaskUtility.E) != 0;
            bool hasS = (mask & TileBitmaskUtility.S) != 0;
            bool hasW = (mask & TileBitmaskUtility.W) != 0;

            // 대각선 이웃 여부
            bool hasNE = (mask & TileBitmaskUtility.NE) != 0;
            bool hasSE = (mask & TileBitmaskUtility.SE) != 0;
            bool hasSW = (mask & TileBitmaskUtility.SW) != 0;
            bool hasNW = (mask & TileBitmaskUtility.NW) != 0;

            var pixels = new Color32[TileSize * TileSize];

            for (int py = 0; py < TileSize; py++)
            {
                for (int px = 0; px < TileSize; px++)
                {
                    bool isBorder = false;

                    // 직선 테두리
                    if (!hasS && py < BorderWidth) isBorder = true;
                    if (!hasN && py >= TileSize - BorderWidth) isBorder = true;
                    if (!hasW && px < BorderWidth) isBorder = true;
                    if (!hasE && px >= TileSize - BorderWidth) isBorder = true;

                    // 대각선 내부 코너:
                    // 직선 이웃은 있지만 대각선 이웃이 없을 때 모서리에 삼각형
                    if (!isBorder)
                    {
                        // 좌하 모서리 (SW): S와 W는 있지만 SW가 없음
                        if (hasS && hasW && !hasSW)
                        {
                            if (px < CornerSize && py < CornerSize &&
                                px + py < CornerSize)
                                isBorder = true;
                        }

                        // 우하 모서리 (SE): S와 E는 있지만 SE가 없음
                        if (hasS && hasE && !hasSE)
                        {
                            int rx = TileSize - 1 - px;
                            if (rx < CornerSize && py < CornerSize &&
                                rx + py < CornerSize)
                                isBorder = true;
                        }

                        // 좌상 모서리 (NW): N과 W는 있지만 NW가 없음
                        if (hasN && hasW && !hasNW)
                        {
                            int ry = TileSize - 1 - py;
                            if (px < CornerSize && ry < CornerSize &&
                                px + ry < CornerSize)
                                isBorder = true;
                        }

                        // 우상 모서리 (NE): N과 E는 있지만 NE가 없음
                        if (hasN && hasE && !hasNE)
                        {
                            int rx = TileSize - 1 - px;
                            int ry = TileSize - 1 - py;
                            if (rx < CornerSize && ry < CornerSize &&
                                rx + ry < CornerSize)
                                isBorder = true;
                        }
                    }

                    pixels[py * TileSize + px] = isBorder ? border : inner;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            return tex;
        }

        private static Sprite CreateSprite(Texture2D tex, int index)
        {
            var sprite = Sprite.Create(
                texture: tex,
                rect: new Rect(0, 0, TileSize, TileSize),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: TileSize);

            sprite.name = $"PlaceholderTileSprite47_{index:D2}";
            return sprite;
        }

        private static Color32 ApplyBrightness(Color32 color, float factor)
        {
            return new Color32(
                (byte)Mathf.Min(color.r * factor, 255f),
                (byte)Mathf.Min(color.g * factor, 255f),
                (byte)Mathf.Min(color.b * factor, 255f),
                color.a);
        }
    }
}