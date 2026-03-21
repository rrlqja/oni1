using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 8방향 비트마스크 계산 유틸리티 + 256→47 인덱스 매핑.
    ///
    /// 비트 배치 (8방향):
    ///   bit 0 (1)   = N  (위)
    ///   bit 1 (2)   = NE (우상)
    ///   bit 2 (4)   = E  (오른쪽)
    ///   bit 3 (8)   = SE (우하)
    ///   bit 4 (16)  = S  (아래)
    ///   bit 5 (32)  = SW (좌하)
    ///   bit 6 (64)  = W  (왼쪽)
    ///   bit 7 (128) = NW (좌상)
    ///
    /// 대각선 마스킹 규칙:
    ///   대각선은 양쪽 직선 이웃이 모두 있을 때만 카운트한다.
    ///   예: NE는 N과 E가 모두 있을 때만 비트가 1이 된다.
    ///   이렇게 하면 유효한 조합이 256 → 47로 축소된다.
    /// </summary>
    public static class TileBitmaskUtility
    {
        // 4방향 (하위 호환)
        public const byte Up    = 1;
        public const byte Left  = 2;
        public const byte Right = 4;
        public const byte Down  = 8;

        // 8방향
        public const byte N  = 1;
        public const byte NE = 2;
        public const byte E  = 4;
        public const byte SE = 8;
        public const byte S  = 16;
        public const byte SW = 32;
        public const byte W  = 64;
        public const byte NW = 128;

        /// <summary>47-타일 인덱스 총 개수</summary>
        public const int TileCount47 = 47;

        /// <summary>
        /// 4방향 비트마스크 계산 (Phase 2 호환).
        /// </summary>
        public static byte Compute4(WorldGrid grid, int x, int y)
        {
            byte elementId = grid.GetCell(x, y).ElementId;
            byte mask = 0;

            if (y + 1 < grid.Height && grid.GetCell(x, y + 1).ElementId == elementId)
                mask |= Up;
            if (x - 1 >= 0 && grid.GetCell(x - 1, y).ElementId == elementId)
                mask |= Left;
            if (x + 1 < grid.Width && grid.GetCell(x + 1, y).ElementId == elementId)
                mask |= Right;
            if (y - 1 >= 0 && grid.GetCell(x, y - 1).ElementId == elementId)
                mask |= Down;

            return mask;
        }

        // 하위 호환 별칭
        public static byte Compute(WorldGrid grid, int x, int y) => Compute4(grid, x, y);

        /// <summary>
        /// 8방향 비트마스크를 계산하고 대각선 마스킹을 적용한 뒤,
        /// 47-타일 인덱스(0~46)를 반환한다.
        /// </summary>
        public static byte Compute47(WorldGrid grid, int x, int y)
        {
            byte raw = ComputeRaw8(grid, x, y);
            byte masked = MaskDiagonals(raw);
            return MapTo47[masked];
        }

        /// <summary>
        /// 8방향 원시 비트마스크 계산 (대각선 마스킹 전).
        /// </summary>
        public static byte ComputeRaw8(WorldGrid grid, int x, int y)
        {
            byte elementId = grid.GetCell(x, y).ElementId;
            int w = grid.Width;
            int h = grid.Height;
            byte mask = 0;

            bool hasN = y + 1 < h && grid.GetCell(x, y + 1).ElementId == elementId;
            bool hasE = x + 1 < w && grid.GetCell(x + 1, y).ElementId == elementId;
            bool hasS = y - 1 >= 0 && grid.GetCell(x, y - 1).ElementId == elementId;
            bool hasW = x - 1 >= 0 && grid.GetCell(x - 1, y).ElementId == elementId;

            if (hasN) mask |= N;
            if (hasE) mask |= E;
            if (hasS) mask |= S;
            if (hasW) mask |= W;

            // 대각선: 양쪽 직선 이웃이 모두 있을 때만 체크
            if (hasN && hasE && y + 1 < h && x + 1 < w &&
                grid.GetCell(x + 1, y + 1).ElementId == elementId)
                mask |= NE;

            if (hasS && hasE && y - 1 >= 0 && x + 1 < w &&
                grid.GetCell(x + 1, y - 1).ElementId == elementId)
                mask |= SE;

            if (hasS && hasW && y - 1 >= 0 && x - 1 >= 0 &&
                grid.GetCell(x - 1, y - 1).ElementId == elementId)
                mask |= SW;

            if (hasN && hasW && y + 1 < h && x - 1 >= 0 &&
                grid.GetCell(x - 1, y + 1).ElementId == elementId)
                mask |= NW;

            return mask;
        }

        /// <summary>
        /// 대각선 마스킹: 양쪽 직선 이웃이 없는 대각선 비트를 제거한다.
        /// ComputeRaw8에서 이미 처리하지만, 외부에서 원시 마스크를 넘길 때 사용.
        /// </summary>
        public static byte MaskDiagonals(byte raw)
        {
            byte result = raw;

            if ((raw & N) == 0 || (raw & E) == 0) result &= unchecked((byte)~NE);
            if ((raw & S) == 0 || (raw & E) == 0) result &= unchecked((byte)~SE);
            if ((raw & S) == 0 || (raw & W) == 0) result &= unchecked((byte)~SW);
            if ((raw & N) == 0 || (raw & W) == 0) result &= unchecked((byte)~NW);

            return result;
        }

        // ================================================================
        //  256 → 47 매핑 테이블
        //
        //  대각선 마스킹 적용 후 유효한 조합은 47가지뿐이다.
        //  이 LUT는 마스킹된 8비트 값(0~255) → 47-인덱스(0~46)를 매핑한다.
        //  BuildMapTo47()로 한 번만 생성.
        // ================================================================

        private static readonly byte[] MapTo47 = BuildMapTo47();

        /// <summary>
        /// 47가지 유효 마스크를 정의하고, 각 마스크에 0~46 인덱스를 부여한다.
        /// 나머지 256-47 = 209개 무효 값은 가장 가까운 유효 인덱스로 매핑한다.
        /// </summary>
        private static byte[] BuildMapTo47()
        {
            // 47가지 유효 마스크 (대각선 마스킹 적용 후)
            // 인덱스 순서는 일반적인 47-타일 아틀라스 규약을 따른다.
            byte[] validMasks = new byte[47]
            {
                //  0: 이웃 없음
                0,
                //  1~4: 1방향만
                N, E, S, W,
                //  5~10: 2방향 (직선 + 꺾임)
                N|S, E|W,                              // 직선 통로
                N|E, E|S, S|W, W|N,                    // L자 꺾임
                // 11~14: 2방향 꺾임 + 대각선 채움
                N|E|NE, E|S|SE, S|W|SW, W|N|NW,
                // 15~18: 3방향 (T자)
                N|E|S, E|S|W, S|W|N, W|N|E,
                // 19~24: 3방향 + 대각선 1개
                N|E|S|NE, N|E|S|SE,
                E|S|W|SE, E|S|W|SW,
                S|W|N|SW, S|W|N|NW,
                // 25~30: 3방향 + 대각선 1개 (나머지)
                W|N|E|NW, W|N|E|NE,
                // 28~30: 3방향 + 대각선 2개
                N|E|S|NE|SE,
                E|S|W|SE|SW,
                S|W|N|SW|NW,
                W|N|E|NW|NE,
                // 32: 4방향 대각선 없음
                N|E|S|W,
                // 33~36: 4방향 + 대각선 1개
                N|E|S|W|NE,
                N|E|S|W|SE,
                N|E|S|W|SW,
                N|E|S|W|NW,
                // 37~42: 4방향 + 대각선 2개
                N|E|S|W|NE|SE,
                N|E|S|W|SE|SW,
                N|E|S|W|SW|NW,
                N|E|S|W|NW|NE,
                N|E|S|W|NE|SW,
                N|E|S|W|SE|NW,
                // 43~45: 4방향 + 대각선 3개
                N|E|S|W|NE|SE|SW,
                N|E|S|W|SE|SW|NW,
                N|E|S|W|SW|NW|NE,
                N|E|S|W|NW|NE|SE,
                // 46: 완전 내부 (8방향 모두)
                N|NE|E|SE|S|SW|W|NW,
            };

            byte[] lut = new byte[256];

            // 유효 마스크에 인덱스 직접 매핑
            for (byte i = 0; i < 47; i++)
                lut[validMasks[i]] = i;

            // 무효 값은 대각선을 제거하면서 유효 마스크를 찾는다
            for (int raw = 0; raw < 256; raw++)
            {
                byte masked = MaskDiagonals((byte)raw);
                if (masked == (byte)raw)
                    continue; // 이미 유효하거나 할당됨

                // 마스킹 후 값이 유효 테이블에 있으면 해당 인덱스 사용
                // 아니면 직선 성분만으로 폴백
                bool found = false;
                for (byte i = 0; i < 47; i++)
                {
                    if (validMasks[i] == masked)
                    {
                        lut[raw] = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // 직선 성분만 추출해서 매핑
                    byte cardinal = (byte)(masked & (N | E | S | W));
                    for (byte i = 0; i < 47; i++)
                    {
                        if (validMasks[i] == cardinal)
                        {
                            lut[raw] = i;
                            break;
                        }
                    }
                }
            }

            return lut;
        }

        /// <summary>
        /// 47 인덱스에 대응하는 유효 마스크를 반환한다.
        /// PlaceholderTileGenerator에서 어떤 테두리를 그릴지 결정할 때 사용.
        /// </summary>
        public static byte GetMaskForIndex47(int index)
        {
            byte[] validMasks = new byte[47]
            {
                0,
                N, E, S, W,
                N|S, E|W,
                N|E, E|S, S|W, W|N,
                N|E|NE, E|S|SE, S|W|SW, W|N|NW,
                N|E|S, E|S|W, S|W|N, W|N|E,
                N|E|S|NE, N|E|S|SE,
                E|S|W|SE, E|S|W|SW,
                S|W|N|SW, S|W|N|NW,
                W|N|E|NW, W|N|E|NE,
                N|E|S|NE|SE,
                E|S|W|SE|SW,
                S|W|N|SW|NW,
                W|N|E|NW|NE,
                N|E|S|W,
                N|E|S|W|NE,
                N|E|S|W|SE,
                N|E|S|W|SW,
                N|E|S|W|NW,
                N|E|S|W|NE|SE,
                N|E|S|W|SE|SW,
                N|E|S|W|SW|NW,
                N|E|S|W|NW|NE,
                N|E|S|W|NE|SW,
                N|E|S|W|SE|NW,
                N|E|S|W|NE|SE|SW,
                N|E|S|W|SE|SW|NW,
                N|E|S|W|SW|NW|NE,
                N|E|S|W|NW|NE|SE,
                N|NE|E|SE|S|SW|W|NW,
            };

            if (index >= 0 && index < 47)
                return validMasks[index];

            return 0;
        }
    }
}