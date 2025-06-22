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
        class LandformMutation
        {
            public string Code = "";
            public float Chance;
            public float[] TerrainOctaves = System.Array.Empty<float>();
            public float[] TerrainOctaveThresholds = System.Array.Empty<float>();
            public float[] TerrainYKeyPositions = System.Array.Empty<float>();
            public float[] TerrainYKeyThresholds = System.Array.Empty<float>();
        }

        class LandformParams
        {
            public string Code = "";
            public float Weight = 1f;
            public float BaseHeight;
            public float NoiseScale;
            public float Threshold;
            public float HeightOffset;
            public float BaseRadius;
            public int PlateauCount;
            public float RadiusStep = 0.6f;
            public float RadiusNoiseScale;
            public float RadiusNoiseAmplitude;
            public float[] TerrainOctaves = System.Array.Empty<float>();
            public float[] TerrainOctaveThresholds = System.Array.Empty<float>();
            public float[] TerrainYKeyPositions = System.Array.Empty<float>();
            public float[] TerrainYKeyThresholds = System.Array.Empty<float>();
            public LandformMutation[] Mutations = System.Array.Empty<LandformMutation>();
        }

        ICoreServerAPI sapi;
        FastNoiseLite mainNoise;
        FastNoiseLite warpNoiseX;
        FastNoiseLite warpNoiseZ;
        FastNoiseLite selectionNoise;

        float[] cumulativeWeights = Array.Empty<float>();
        float totalWeight = 0f;

        LandformParams[] landforms;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.InitWorldGenerator(InitWorldGen, "standard");
        }

        private void InitWorldGen()
        {
            int seed = sapi.WorldManager.Seed;
            mainNoise = new FastNoiseLite(seed) { CurrentNoiseType = FastNoiseLite.NoiseType.OpenSimplex2 };
            warpNoiseX = new FastNoiseLite(seed + 1) { CurrentNoiseType = FastNoiseLite.NoiseType.OpenSimplex2 };
            warpNoiseZ = new FastNoiseLite(seed + 2) { CurrentNoiseType = FastNoiseLite.NoiseType.OpenSimplex2 };


            LoadLandforms();

            selectionNoise = new FastNoiseLite(seed + 3)
            {
                CurrentNoiseType = FastNoiseLite.NoiseType.OpenSimplex2,
                Frequency = 0.0005f
            };

            if (landforms != null && landforms.Length > 0)
            {
                cumulativeWeights = new float[landforms.Length];
                totalWeight = 0f;
                for (int i = 0; i < landforms.Length; i++)
                {
                    totalWeight += landforms[i].Weight;
                    cumulativeWeights[i] = totalWeight;
                }
            }

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

                    // Choose landform using weighted noise value
                    float sel = (selectionNoise.GetNoise(worldX, worldZ) + 1f) * 0.5f * totalWeight;
                    int lfIndex = 0;
                    while (lfIndex < cumulativeWeights.Length && sel > cumulativeWeights[lfIndex])
                    {
                        lfIndex++;
                    }
                    if (lfIndex >= landforms.Length) lfIndex = landforms.Length - 1;
                    LandformParams lp = landforms[lfIndex];

                    float noiseVal = SampleLandform(lp, worldX, worldZ);
                    if (noiseVal < 0) continue;

                    int height = (int)(mapHeight * noiseVal);
                    int chunkY = height / chunkSize;
                    if (chunkY < 0 || chunkY >= chunks.Length) continue;
                    int localY = height % chunkSize;
                    IServerChunk chunk = chunks[chunkY];
                    int index = localY * chunkSize * chunkSize + z * chunkSize + x;
                    chunk.Blocks[index] = 1;
                }
            }
        }

        private float SampleLandform(LandformParams p, int worldX, int worldZ)
        {
            float[] octaves = p.TerrainOctaves;
            float[] octaveThr = p.TerrainOctaveThresholds;
            float[] ykeys = p.TerrainYKeyPositions;
            float[] ythresh = p.TerrainYKeyThresholds;

            if (p.Mutations.Length > 0)
            {
                float rnd = Rand01(worldX, worldZ);
                foreach (var m in p.Mutations)
                {
                    if (rnd < m.Chance)
                    {
                        if (m.TerrainOctaves.Length > 0) octaves = m.TerrainOctaves;
                        if (m.TerrainOctaveThresholds.Length > 0) octaveThr = m.TerrainOctaveThresholds;
                        if (m.TerrainYKeyPositions.Length > 0) ykeys = m.TerrainYKeyPositions;
                        if (m.TerrainYKeyThresholds.Length > 0) ythresh = m.TerrainYKeyThresholds;
                        break;
                    }
                    rnd -= m.Chance;
                }
            }

            float warpX = warpNoiseX.GetNoise(worldX * 0.01f, worldZ * 0.01f) * 20f;
            float warpZ = warpNoiseZ.GetNoise(worldX * 0.01f + 1000, worldZ * 0.01f + 1000) * 20f;

            float stepFactor = 1f;
            if (p.PlateauCount > 0 && p.BaseRadius > 0f)
            {
                float cellSize = p.BaseRadius * 2f;
                int baseCellX = (int)Math.Floor(worldX / cellSize);
                int baseCellZ = (int)Math.Floor(worldZ / cellSize);

                stepFactor = 0f;
                for (int cx = baseCellX - 1; cx <= baseCellX + 1; cx++)
                {
                    for (int cz = baseCellZ - 1; cz <= baseCellZ + 1; cz++)
                    {
                        float jitterX = warpNoiseX.GetNoise(cx * 0.1f, cz * 0.1f) * cellSize * 0.4f;
                        float jitterZ = warpNoiseZ.GetNoise(cx * 0.1f + 1000, cz * 0.1f + 1000) * cellSize * 0.4f;
                        float centerX = (cx + 0.5f) * cellSize + jitterX;
                        float centerZ = (cz + 0.5f) * cellSize + jitterZ;
                        float dx = worldX - centerX;
                        float dz = worldZ - centerZ;
                        float dist = GameMath.Sqrt(dx * dx + dz * dz);

                        float radius = p.BaseRadius;
                        if (p.RadiusNoiseScale > 0f)
                        {
                            float n = warpNoiseX.GetNoise(cx * p.RadiusNoiseScale, cz * p.RadiusNoiseScale);
                            radius *= 1f + p.RadiusNoiseAmplitude * n;
                            if (radius < p.BaseRadius) radius = p.BaseRadius;
                        }

                        float innerRadius = radius;
                        for (int j = 1; j < p.PlateauCount; j++)
                        {
                            innerRadius *= p.RadiusStep;
                        }

                        float curRadius = innerRadius;
                        for (int i = p.PlateauCount - 1; i >= 0; i--)
                        {
                            float shapeNoise = 1f + warpNoiseX.GetNoise((worldX + cx * 100 + i * 50) * 0.02f, (worldZ + cz * 100 + i * 50) * 0.02f) * 0.25f;
                            if (dist <= curRadius * shapeNoise)
                            {
                                float sf = (i + 1f) / p.PlateauCount;
                                if (sf > stepFactor) stepFactor = sf;
                                break;
                            }
                            curRadius /= p.RadiusStep;
                        }
                    }
                }

                if (stepFactor == 0f) return -1f;
            }

            float total = 0f;
            int octCount = octaves.Length;
            for (int i = 0; i < octCount; i++)
            {
                float amp = octaves[i];
                float freq = 1 << i;
                float thr = (i < octaveThr.Length) ? octaveThr[i] : 0f;
                float raw = mainNoise.GetNoise((worldX + warpX) * p.NoiseScale * freq, (worldZ + warpZ) * p.NoiseScale * freq);
                float n = (raw + 1f) * 0.5f;
                n = GameMath.Max(0f, n - thr);
                n = 3f * n * n - 2f * n * n * n;
                total += amp * n;
            }

            total = GameMath.Clamp(total, 0f, 1f);

            if (total < p.Threshold) return -1f;

            float yFactor = 1f;
            if (ykeys.Length > 1 && ythresh.Length >= ykeys.Length)
            {
                yFactor = ythresh[ythresh.Length - 1];
                for (int i = 0; i < ykeys.Length - 1; i++)
                {
                    if (total <= ykeys[i + 1])
                    {
                        float p1 = ykeys[i];
                        float p2 = ykeys[i + 1];
                        float t1 = ythresh[i];
                        float t2 = ythresh[i + 1];
                        float ratio = p2 == p1 ? 0f : (total - p1) / (p2 - p1);
                        yFactor = t1 + (t2 - t1) * ratio;
                        break;
                    }
                }
            }

            total = GameMath.Clamp(total * yFactor * stepFactor, 0f, 1f);

            return p.BaseHeight + p.HeightOffset * total;
        }


        private int yindex(int height)
        {
            return GameMath.Clamp(height, 0, sapi.WorldManager.MapSizeY - 1);
        }

        private float Rand01(int x, int z, int salt = 0)
        {
            unchecked
            {
                int h = x * 73428767 ^ z * 19349663 ^ (sapi.WorldManager.Seed + salt);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
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
                            lp.Code = lf.Value<string>("code") ?? "";
                            lp.BaseHeight = lf.Value<float>("baseHeight");
                            lp.NoiseScale = lf.Value<float>("noiseScale");
                            lp.Threshold = lf.Value<float>("threshold");
                            lp.HeightOffset = lf.Value<float>("heightOffset");
                            lp.Weight = lf.Value<float?>("weight") ?? 1f;
                            lp.BaseRadius = lf.Value<float?>("baseRadius") ?? 0f;
                            lp.PlateauCount = lf.Value<int?>("plateauCount") ?? 0;
                            lp.RadiusStep = lf.Value<float?>("radiusStep") ?? 0.6f;
                            lp.RadiusNoiseScale = lf.Value<float?>("radiusNoiseScale") ?? 0f;
                            lp.RadiusNoiseAmplitude = lf.Value<float?>("radiusNoiseAmplitude") ?? 0f;
                            lp.TerrainOctaves = lf["terrainOctaves"]?.ToObject<float[]>() ?? Array.Empty<float>();
                            lp.TerrainOctaveThresholds = lf["terrainOctaveThresholds"]?.ToObject<float[]>() ?? Array.Empty<float>();
                            lp.TerrainYKeyPositions = lf["terrainYKeyPositions"]?.ToObject<float[]>() ?? Array.Empty<float>();
                            lp.TerrainYKeyThresholds = lf["terrainYKeyThresholds"]?.ToObject<float[]>() ?? Array.Empty<float>();

                            var mutArr = lf["mutations"] as JArray;
                            if (mutArr != null && mutArr.Count > 0)
                            {
                                List<LandformMutation> mlist = new List<LandformMutation>();
                                foreach (JObject mj in mutArr)
                                {
                                    LandformMutation mp = new LandformMutation();
                                    mp.Code = mj.Value<string>("code") ?? "";
                                    mp.Chance = mj.Value<float?>("chance") ?? 0f;
                                    mp.TerrainOctaves = mj["terrainOctaves"]?.ToObject<float[]>() ?? Array.Empty<float>();
                                    mp.TerrainOctaveThresholds = mj["terrainOctaveThresholds"]?.ToObject<float[]>() ?? Array.Empty<float>();
                                    mp.TerrainYKeyPositions = mj["terrainYKeyPositions"]?.ToObject<float[]>() ?? Array.Empty<float>();
                                    mp.TerrainYKeyThresholds = mj["terrainYKeyThresholds"]?.ToObject<float[]>() ?? Array.Empty<float>();
                                    mlist.Add(mp);
                                }
                                lp.Mutations = mlist.ToArray();
                            }

                            list.Add(lp);
                        }
                        if (list.Count > 0)
                        {
                            landforms = list.ToArray();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                sapi.Logger.Warning("failed loading fixedcliffs landforms.json: {0}", e);
            }

            if (landforms == null || landforms.Length == 0)
            {
                landforms = GetDefaultLandforms();
            }

            // Load weights from patched landformConfig
            try
            {
                var cfgAsset = sapi.Assets.TryGet(new AssetLocation("worldgen/landformConfig.json"));
                if (cfgAsset != null)
                {
                    JObject cfg = cfgAsset.ToObject<JObject>();
                    var lfobj = cfg["landforms"] as JObject;
                    if (lfobj != null)
                    {
                        foreach (var lp in landforms)
                        {
                            if (lfobj.TryGetValue(lp.Code, out JToken wtok))
                            {
                                lp.Weight = wtok.Value<float>("weight");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                sapi.Logger.Warning("failed loading landformConfig weights:{0}", e);
            }
        }

        private LandformParams[] GetDefaultLandforms()
        {
            return new LandformParams[]
            {
                new LandformParams
                {
                    Code = "flatlands",
                    Weight = 6324f,
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
                    Code = "sheercliffs",
                    Weight = 1084f,
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
                    Code = "canyons",
                    Weight = 2710f,
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
                    Code = "towercliffs",
                    Weight = 723f,
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
                    Code = "riceplateaus",
                    Weight = 7228f,
                    BaseHeight = 0.20f,
                    NoiseScale = 0.0002f,
                    Threshold = 0.4f,
                    HeightOffset = 0.80f,
                    BaseRadius = 60f,
                    RadiusNoiseScale = 0.05f,
                    RadiusNoiseAmplitude = 1f,
                    PlateauCount = 4,
                    RadiusStep = 0.75f,
                    TerrainOctaves = new float[] {0f,0.8f,0.8f,1f,1f,0.4f,0.2f,0.1f,0.1f},
                    TerrainOctaveThresholds = new float[] {0f,0f,0f,0.4f,0f,0f,0f,0f,0f},
                    TerrainYKeyPositions = new float[] {0.40f,0.55f,0.70f,0.85f,1.00f},
                    TerrainYKeyThresholds = new float[] {1f,1f,0.80f,0.60f,0f}
                }
            };
        }
    }
}
