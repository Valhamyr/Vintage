using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using System;

namespace LandformTeleport
{
    public class LandformTeleportSystem : ModSystem
    {
        ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            api.ChatCommands.Create("tpl")
                .WithDesc("Teleport to nearest landform")
                .WithArgs(api.ChatCommands.Parsers.Parsers["string"]("landform"))
                .HandleWith(OnTeleportCommand);
        }

        private TextCommandResult OnTeleportCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player == null)
            {
                return TextCommandResult.Error("Command can only be used by a player.");
            }

            string landformCode = args[0];
            Vec3d startPos = args.Caller.Entity.Pos.XYZ;

            Vec3d? target = FindNearestLandform(startPos, landformCode);

            if (target == null)
            {
                return TextCommandResult.Error("Landform not found nearby");
            }

            args.Caller.Player.TeleportTo(target.Value);
            return TextCommandResult.Success("Teleported to {0}", landformCode);
        }

        // NOTE: This is a placeholder search using worldgen API. Adjust as needed
        private Vec3d? FindNearestLandform(Vec3d startPos, string landformCode)
        {
            // The worldgen API provides access to landform maps via MapRegion and MapChunk.
            // Search nearby chunks (within 20 chunks) for matching landform index.
            int searchRadiusChunks = 20;
            int chunkSize = sapi.WorldManager.ChunkSize;
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
                        string code = mapChunk.LandformCode;
                        if (code == landformCode)
                        {
                            double x = (cx + 0.5) * chunkSize;
                            double z = (cz + 0.5) * chunkSize;
                            double y = sapi.World.BlockAccessor.GetTerrainMapheightAt((int)x, (int)z);
                            return new Vec3d(x, y + 1, z);
                        }
                    }
                }
            }

            return null;
        }
    }
}
