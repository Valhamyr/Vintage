using System;

namespace FixedCliffs
{
    /// <summary>
    /// Minimal replacement for the FastNoiseLite library used by older examples.
    /// Provides simple gradient noise so the mod compiles without the external
    /// dependency. This is not a full implementation but mimics the required API.
    /// </summary>
    public class FastNoiseLite
    {
        private readonly PermutationTable perm;

        public FastNoiseLite(int seed)
        {
            perm = new PermutationTable(seed);
        }

        public enum NoiseType
        {
            OpenSimplex2
        }

        public NoiseType NoiseType { get; set; }

        public float GetNoise(float x, float y)
        {
            int xi = FastFloor(x);
            int yi = FastFloor(y);

            float xf = x - xi;
            float yf = y - yi;

            int aa = perm[xi + perm[yi]];
            int ab = perm[xi + perm[yi + 1]];
            int ba = perm[xi + 1 + perm[yi]];
            int bb = perm[xi + 1 + perm[yi + 1]];

            float u = Fade(xf);
            float v = Fade(yf);

            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

            return Lerp(x1, x2, v);
        }

        private static int FastFloor(float x)
        {
            return x > 0 ? (int)x : (int)x - 1;
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static float Grad(int hash, float x, float y)
        {
            switch (hash & 3)
            {
                case 0: return  x + y;
                case 1: return -x + y;
                case 2: return  x - y;
                default: return -x - y;
            }
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + t * (b - a);
        }

        private class PermutationTable
        {
            private readonly int[] perm = new int[512];

            public PermutationTable(int seed)
            {
                int[] p = new int[256];
                for (int i = 0; i < 256; i++) p[i] = i;
                var rnd = new Random(seed);
                for (int i = 255; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    int tmp = p[i];
                    p[i] = p[j];
                    p[j] = tmp;
                }
                for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
            }

            public int this[int index] => perm[index & 511];
        }
    }
}
