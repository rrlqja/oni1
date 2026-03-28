namespace Core.Simulation.Runtime.WorldGeneration
{
    /// <summary>
    /// 시드 기반 결정적 난수 생성기 (Mulberry32).
    /// 동일 시드 → 동일 시퀀스 보장.
    /// </summary>
    public sealed class SeededRandom
    {
        private uint _state;

        public SeededRandom(int seed)
        {
            _state = (uint)seed;
            NextUint();
            NextUint();
        }

        public uint NextUint()
        {
            _state += 0x6D2B79F5u;
            uint t = _state;
            t = (t ^ (t >> 15)) * (1u | t);
            t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
            return t ^ (t >> 14);
        }

        public float NextFloat() => NextUint() / (float)uint.MaxValue;

        public int NextInt(int min, int max)
        {
            if (min >= max) return min;
            return min + (int)(NextFloat() * (max - min));
        }

        public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

        /// <summary>
        /// 2D value noise (-1 ~ 1). 결정적.
        /// </summary>
        public static float Noise2D(float x, float y, int seed)
        {
            int ix = FloorToInt(x);
            int iy = FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float n00 = Hash(ix, iy, seed);
            float n10 = Hash(ix + 1, iy, seed);
            float n01 = Hash(ix, iy + 1, seed);
            float n11 = Hash(ix + 1, iy + 1, seed);

            return Lerp(Lerp(n00, n10, fx), Lerp(n01, n11, fx), fy);
        }

        /// <summary>
        /// fBm 노이즈. 0 ~ 1 범위.
        /// </summary>
        public static float FBM(float x, float y, int seed, int octaves = 4,
            float lacunarity = 2f, float persistence = 0.5f)
        {
            float sum = 0f, amp = 1f, freq = 1f, max = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise2D(x * freq, y * freq, seed + i * 31) * amp;
                max += amp;
                amp *= persistence;
                freq *= lacunarity;
            }
            return (sum / max + 1f) * 0.5f;
        }

        private static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h = h ^ (h >> 16);
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF * 2f - 1f;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static int FloorToInt(float v) { int i = (int)v; return v < i ? i - 1 : i; }
    }
}