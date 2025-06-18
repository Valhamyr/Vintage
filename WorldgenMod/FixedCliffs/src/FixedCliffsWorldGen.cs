using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using System.Collections.Generic;
using System;
using Newtonsoft.Json.Linq;

namespace FixedCliffs
{
    /// <summary>
    /// Simple worldgen hook that remaps terrain noise for the custom landforms.
    /// Implements optional domain warping and an S-curve transformation as
    /// described in terrain_generation_guide.md.
    /// </summary>
    public class FixedCliffsWorldGen : ModSystem
    {
        class LandformParams
        {
            public float BaseHeight;
            public float NoiseScale;
            public float Threshold;
            public float HeightOffset;
            public float[] TerrainOctaves = System.Array.Empty<float>();
            public float[] TerrainOctaveThresholds = System.Array.Empty<float>();
            public float[] TerrainYKeyPositions = System.Array.Empty<float>();
            public float[] TerrainYKeyThresholds = System.Array.Empty<float>();
        }

        ICoreServerAPI sapi;
        FastNoiseLite mainNoise;
        FastNoiseLite warpNoiseX;
        FastNoiseLite warpNoiseZ;

        LandformParams[] landforms;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.InitWorldGenerator(InitWorldGen, "standard");
        }

        private void InitWorldGen()
        {
            int seed = sapi.WorldManager.Seed;
            mainNoise = new FastNoiseLite(seed) { NoiseType = FastNoiseLite.NoiseType.OpenSimplex2 };
            warpNoiseX = new FastNoiseLite(seed + 1) { NoiseType = FastNoiseLite.NoiseType.OpenSimplex2 };
            warpNoiseZ = new FastNoiseLite(seed + 2) { NoiseType = FastNoiseLite.NoiseType.OpenSimplex2 };


            LoadLandforms();

            // API version differences require reflection to register our chunk
            // column callback. Older releases exposed a ChunkGen property while
            // newer versions provide the registration method directly on the
            // world manager instance.
            var wm = sapi.WorldManager;
            var wmType = wm.GetType();
            var chunkGenProp = wmType.GetProperty("ChunkGen");
            if (chunkGenProp != null)
            {
                object chunkGen = chunkGenProp.GetValue(wm);
                var method = chunkGenProp.PropertyType.GetMethod(
                    "RegisterChunkColumnModifier",
                    new[] { typeof(Action<IServerChunk[], int, int>) });
                method?.Invoke(chunkGen, new object[] { (Action<IServerChunk[], int, int>)GenChunkColumn });
            }
            else
            {
                var method = wmType.GetMethod(
                    "RegisterChunkColumnModifier",
                    new[] { typeof(Action<IServerChunk[], int, int>) })
                    ?? wmType.GetMethod(
                        "RegisterChunkColumnGeneration",
                        new[] { typeof(Action<IServerChunk[], int, int>) });
                method?.Invoke(wm, new object[] { (Action<IServerChunk[], int, int>)GenChunkColumn });
            }
        }

        private void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            int chunkSize = sapi.WorldManager.ChunkSize;
            int mapHeight = sapi.WorldManager.MapSizeY;

            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    int worldX = chunkX * chunkSize + x;
                    int worldZ = chunkZ * chunkSize + z;

                    // Simple region-based selection of landform
                    int region = ((worldX >> 9) + (worldZ >> 9)) % landforms.Length;
                    if (region < 0) region += landforms.Length;
                    LandformParams lp = landforms[region];

                    float noiseVal = SampleLandform(lp, worldX, worldZ);
                    if (noiseVal < 0) continue;

                    int height = (int)(mapHeight * noiseVal);
                    chunks[0].Blocks[(yindex(height) << 5) | (z << 5) | x] = 1;
                }
            }
        }

        private float SampleLandform(LandformParams p, int worldX, int worldZ)
        {
            float warpX = warpNoiseX.GetNoise(worldX * 0.01f, worldZ * 0.01f) * 20f;
            float warpZ = warpNoiseZ.GetNoise(worldX * 0.01f + 1000, worldZ * 0.01f + 1000) * 20f;

            float total = 0f;
            int octCount = p.TerrainOctaves.Length;
            for (int i = 0; i < octCount; i++)
            {
                float amp = p.TerrainOctaves[i];
                float freq = 1 << i;
                float thr = (i < p.TerrainOctaveThresholds.Length) ? p.TerrainOctaveThresholds[i] : 0f;
                float raw = mainNoise.GetNoise((worldX + warpX) * p.NoiseScale * freq, (worldZ + warpZ) * p.NoiseScale * freq);
                float n = (raw + 1f) * 0.5f;
                n = GameMath.Max(0f, n - thr);
                n = 3f * n * n - 2f * n * n * n;
                total += amp * n;
            }

            total = GameMath.Clamp(total, 0f, 1f);

            if (total < p.Threshold) return -1f;

            float yFactor = 1f;
            if (p.TerrainYKeyPositions.Length > 1 && p.TerrainYKeyThresholds.Length >= p.TerrainYKeyPositions.Length)
            {
                yFactor = p.TerrainYKeyThresholds[p.TerrainYKeyThresholds.Length - 1];
                for (int i = 0; i < p.TerrainYKeyPositions.Length - 1; i++)
                {
                    if (total <= p.TerrainYKeyPositions[i + 1])
                    {
                        float p1 = p.TerrainYKeyPositions[i];
                        float p2 = p.TerrainYKeyPositions[i + 1];
                        float t1 = p.TerrainYKeyThresholds[i];
                        float t2 = p.TerrainYKeyThresholds[i + 1];
                        float ratio = p2 == p1 ? 0f : (total - p1) / (p2 - p1);
                        yFactor = t1 + (t2 - t1) * ratio;
                        break;
                    }
                }
            }

            total = GameMath.Clamp(total * yFactor, 0f, 1f);

            return p.BaseHeight + p.HeightOffset * total;
        }

        private int yindex(int height)
        {
            return GameMath.Clamp(height, 0, sapi.WorldManager.MapSizeY - 1);
        }

        private void LoadLandforms()
        {
            try
            {
                var asset = sapi.Assets.TryGet(new AssetLocation("worldgen/landforms.json", "fixedcliffs"));
                if (asset != null)
                {
                    JObject obj = asset.ToObject<JObject>();
                    var arr = obj["variants"] as JArray;
                    if (arr != null)
                    {
                        List<LandformParams> list = new List<LandformParams>();
                        foreach (JObject lf in arr)
                        {
                            LandformParams lp = new LandformParams();
                            lp.BaseHeight = lf.Value<float>("baseHeight");
                            lp.NoiseScale = lf.Value<float>("noiseScale");
                            lp.Threshold = lf.Value<float>("threshold");
                            lp.HeightOffset = lf.Value<float>("heightOffset");
                            lp.TerrainOctaves = lf["terrainOctaves"]?.ToObject<float[]>() ?? Array.Empty<float>();
                            lp.TerrainOctaveThresholds = lf["terrainOctaveThresholds"]?.ToObject<float[]>() ?? Array.Empty<float>();
                            lp.TerrainYKeyPositions = lf["terrainYKeyPositions"]?.ToObject<float[]>() ?? Array.Empty<float>();
                            lp.TerrainYKeyThresholds = lf["terrainYKeyThresholds"]?.ToObject<float[]>() ?? Array.Empty<float>();
                            list.Add(lp);
                        }
                        if (list.Count > 0)
                        {
                            landforms = list.ToArray();
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                sapi.Logger.Warning("failed loading fixedcliffs landforms.json: {0}", e);
            }

            landforms = GetDefaultLandforms();
        }

        private LandformParams[] GetDefaultLandforms()
        {
            return new LandformParams[]
            {
                new LandformParams
                {
                    BaseHeight = 0.2f,
                    NoiseScale = 0.001f,
                    Threshold = 0.5f,
                    HeightOffset = 0.55f,
                    TerrainOctaves = new float[] {0f,0f,0.1f,0.15f,0.2f,0f,0.4f},
                    TerrainOctaveThresholds = new float[] {0f,0f,0f,0f,0f,0f,0.4f},
                    TerrainYKeyPositions = new float[] {0f,0.440f,0.460f,0.470f},
                    TerrainYKeyThresholds = new float[] {1f,1f,0.5f,0f}
                },
                new LandformParams
                {
                    BaseHeight = 0.25f,
                    NoiseScale = 0.0005f,
                    Threshold = 0.95f,
                    HeightOffset = 0.55f,
                    TerrainOctaves = new float[] {0.1f,0.2f,0.4f,0.6f,1f,0.6f,0.4f,0.3f,0.2f},
                    TerrainOctaveThresholds = new float[] {0f,0f,0f,0f,0.3f,0f,0f,0f,0f},
                    TerrainYKeyPositions = new float[] {0f,0.35f,0.45f,0.46f,0.6f,0.7f},
                    TerrainYKeyThresholds = new float[] {1f,1f,0.5f,0.5f,0.2f,0f}
                },
                new LandformParams
                {
                    BaseHeight = 0.2f,
                    NoiseScale = 0.00025f,
                    Threshold = 0.2f,
                    HeightOffset = 0.75f,
                    TerrainOctaves = new float[] {0f,0f,0.1f,0.2f,0.4f,1f,1f,0.8f,0.3f},
                    TerrainOctaveThresholds = new float[] {0f,0f,0f,0f,0f,0f,0f,0f,0f},
                    TerrainYKeyPositions = new float[] {0f,0.35f,0.55f,0.70f,0.80f,0.90f},
                    TerrainYKeyThresholds = new float[] {0f,0f,0.8f,1f,1f,1f}
                },
                new LandformParams
                {
                    BaseHeight = 0.25f,
                    NoiseScale = 0.0005f,
                    Threshold = 0.97f,
                    HeightOffset = 0.55f,
                    TerrainOctaves = new float[] {0f,0f,0f,0f,0.2f,0.5f,1f,0.9f,0.4f},
                    TerrainOctaveThresholds = new float[] {0f,0f,0f,0f,0f,0f,0f,0f,0.3f},
                    TerrainYKeyPositions = new float[] {0f,0.45f,0.48f,0.55f,0.63f,0.70f,0.75f,0.80f,0.86f,0.90f},
                    TerrainYKeyThresholds = new float[] {1f,1f,0.45f,0.30f,0.22f,0.20f,0.18f,0.15f,0.14f,0f}
                },
                new LandformParams
                {
                    BaseHeight = 0.20f,
                    NoiseScale = 0.0002f,
                    Threshold = 0.4f,
                    HeightOffset = 0.80f,
                    TerrainOctaves = new float[] {0f,0.8f,0.8f,1f,1f,0.4f,0.2f,0.1f,0.1f},
                    TerrainOctaveThresholds = new float[] {0f,0f,0f,0.4f,0f,0f,0f,0f,0f},
                    TerrainYKeyPositions = new float[] {0.40f,0.55f,0.70f,0.85f,1.00f},
                    TerrainYKeyThresholds = new float[] {1f,1f,0.80f,0.60f,0f}
                }
            };
        }
    }
}
