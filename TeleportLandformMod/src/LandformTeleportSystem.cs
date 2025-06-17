using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace LandformTeleport
{
    public class LandformTeleportSystem : ModSystem
    {
        ICoreServerAPI sapi;
        MethodInfo terrainHeightMethod;
        Dictionary<int, string> landformCodes = new Dictionary<int, string>();

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            // Build lookup table for all landforms in the running world
            try
            {
                var asset = api.Assets.TryGet("worldgen/landforms.json");
                if (asset != null)
                {
                    JObject obj = asset.ToObject<JObject>();
                    var arr = obj["landforms"] as JArray;
                    if (arr != null)
                    {
                        for (int i = 0; i < arr.Count; i++)
                        {
                            landformCodes[i] = (string)arr[i]["code"];
                        }
                    }
                }
            }
            catch { }
            // Locate the correct terrain height method at runtime for
            // compatibility with different Vintage Story API versions.
            var accType = sapi.World.BlockAccessor.GetType();
            terrainHeightMethod = accType.GetMethod("GetTerrainMapHeightAt", new[] { typeof(int), typeof(int) })
                ?? accType.GetMethod("GetTerrainMapheightAt", new[] { typeof(int), typeof(int) });

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.Create("tpl")
                .WithDescription("Teleport to nearest landform")
                .WithArgs(parsers.Word("landform"))
                .RequiresPrivilege("tp")
                .HandleWith(OnTeleportCommand);

            api.ChatCommands.Create("tplandforms")
                .WithDescription("List nearby landforms for /tpl")
                .RequiresPrivilege("tp")
                .HandleWith(OnListLandformsCommand);
        }

        private TextCommandResult OnTeleportCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player == null)
            {
                return TextCommandResult.Error("Command can only be used by a player.");
            }

            string landformCode = (string)args[0];

            if (!landformCodes.ContainsValue(landformCode))
            {
                return TextCommandResult.Error("Unknown landform name");
            }

            Vec3d startPos = args.Caller.Entity.Pos.XYZ;

            Vec3d target = FindNearestLandform(startPos, landformCode);

            if (target == null)
            {
                return TextCommandResult.Error("Landform not found nearby");
            }

            args.Caller.Entity?.TeleportTo((int)target.X, (int)target.Y, (int)target.Z);
            return TextCommandResult.Success("Teleported to {0}", landformCode);
        }

        private TextCommandResult OnListLandformsCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player == null)
            {
                return TextCommandResult.Error("Command can only be used by a player.");
            }

            Vec3d startPos = args.Caller.Entity.Pos.XYZ;
            HashSet<string> codes = FindLocalLandforms(startPos);

            if (codes.Count == 0)
            {
                return TextCommandResult.Success("No landforms found nearby");
            }

            return TextCommandResult.Success("Nearby landforms: {0}", string.Join(", ", codes));
        }

        // NOTE: This is a placeholder search using worldgen API. Adjust as needed
        private Vec3d FindNearestLandform(Vec3d startPos, string landformCode)
        {
            // The worldgen API provides access to landform maps via MapRegion and MapChunk.
            // Search nearby chunks (within 20 chunks) for matching landform index.
            int searchRadiusChunks = 50;
            int chunkSize = sapi.WorldManager.ChunkSize;
            int regionSize = sapi.WorldManager.RegionSize;
            int chunksPerRegion = regionSize / chunkSize;
            int originChunkX = (int)startPos.X / chunkSize;
            int originChunkZ = (int)startPos.Z / chunkSize;

            for (int r = 0; r <= searchRadiusChunks; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        int cx = originChunkX + dx;
                        int cz = originChunkZ + dz;

                        IMapChunk mapChunk = sapi.WorldManager.GetMapChunk(cx, cz);
                        if (mapChunk == null) continue;

                        int regionX = cx / chunksPerRegion;
                        int regionZ = cz / chunksPerRegion;
                        IMapRegion region = sapi.WorldManager.GetMapRegion(regionX, regionZ);
                        if (region == null) continue;

                        int lx = cx % chunksPerRegion;
                        int lz = cz % chunksPerRegion;
                        int index = region.LandformMap.GetInt(lx, lz);
                        landformCodes.TryGetValue(index, out string code);

                        if (code == landformCode)
                        {
                            double x = (cx + 0.5) * chunkSize;
                            double z = (cz + 0.5) * chunkSize;
                            // API method name varies between versions. Use helper
                            // to call whichever method is available at runtime.
                            double y = GetTerrainHeight((int)x, (int)z);
                            return new Vec3d(x, y + 1, z);
                        }
                    }
                }
            }

            return null;
        }

        private HashSet<string> FindLocalLandforms(Vec3d startPos)
        {
            int searchRadiusChunks = 20;
            int chunkSize = sapi.WorldManager.ChunkSize;
            int regionSize = sapi.WorldManager.RegionSize;
            int chunksPerRegion = regionSize / chunkSize;
            int originChunkX = (int)startPos.X / chunkSize;
            int originChunkZ = (int)startPos.Z / chunkSize;

            HashSet<string> codes = new HashSet<string>();

            for (int dx = -searchRadiusChunks; dx <= searchRadiusChunks; dx++)
            {
                for (int dz = -searchRadiusChunks; dz <= searchRadiusChunks; dz++)
                {
                    int cx = originChunkX + dx;
                    int cz = originChunkZ + dz;

                    IMapChunk mapChunk = sapi.WorldManager.GetMapChunk(cx, cz);
                    if (mapChunk == null) continue;

                    int regionX = cx / chunksPerRegion;
                    int regionZ = cz / chunksPerRegion;
                    IMapRegion region = sapi.WorldManager.GetMapRegion(regionX, regionZ);
                    if (region == null) continue;

                    int lx = cx % chunksPerRegion;
                    int lz = cz % chunksPerRegion;
                    int index = region.LandformMap.GetInt(lx, lz);
                    if (landformCodes.TryGetValue(index, out string code))
                    {
                        codes.Add(code);
                    }
                }
            }

            return codes;
        }

        // Uses reflection to support both GetTerrainMapHeightAt (new name) and
        // GetTerrainMapheightAt (old name) depending on the installed API
        // version.
        private double GetTerrainHeight(int x, int z)
        {
            if (terrainHeightMethod != null)
            {
                object val = terrainHeightMethod.Invoke(sapi.World.BlockAccessor, new object[] { x, z });
                return Convert.ToDouble(val);
            }

            // Should never happen but avoids crash if method not found
            return 0;
        }
    }
}
