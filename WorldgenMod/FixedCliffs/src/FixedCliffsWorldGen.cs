using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using System.Collections.Generic;

namespace FixedCliffs
{
    /// <summary>
    /// Simple worldgen hook that remaps terrain noise for the custom landforms.
    /// Implements optional domain warping and an S-curve transformation as
    /// described in terrain_generation_guide.md.
    /// </summary>
    public class FixedCliffsWorldGen : ModSystem
    {
        ICoreServerAPI sapi;
        FastNoiseLite mainNoise;
        FastNoiseLite warpNoiseX;
        FastNoiseLite warpNoiseZ;

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

            sapi.WorldManager.ChunkGen.RegisterChunkColumnModifier(GenChunkColumn);
        }

        private void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            int chunkSize = sapi.WorldManager.ChunkSize;
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    int worldX = chunkX * chunkSize + x;
                    int worldZ = chunkZ * chunkSize + z;

                    float warpX = warpNoiseX.GetNoise(worldX * 0.01f, worldZ * 0.01f) * 20f;
                    float warpZ = warpNoiseZ.GetNoise(worldX * 0.01f + 1000, worldZ * 0.01f + 1000) * 20f;
                    float raw = mainNoise.GetNoise(worldX + warpX, worldZ + warpZ);

                    float n = (raw + 1f) * 0.5f;
                    n = 3 * n * n - 2 * n * n * n;

                    int height = (int)(sapi.WorldManager.MapSizeY * n);
                    chunks[0].Blocks[(yindex(height) << 5) | (z << 5) | x] = 1;
                }
            }
        }

        private int yindex(int height)
        {
            return GameMath.Clamp(height, 0, sapi.WorldManager.MapSizeY - 1);
        }
    }
}
