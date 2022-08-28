using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace VsVillage
{
    public class VillageGenerator : ModStdWorldGen
    {

        public override double ExecuteOrder() => 0.6;
        public override void StartServerSide(ICoreServerAPI api)
        {
            //api.Event.ChunkColumnGeneration(handler, EnumWorldGenPass.TerrainFeatures, "standard");
            //api.Event.MapRegionGeneration(mapHandler, "standard");

            api.RegisterCommand("vds", "debug command for printing village layout", "[square|village]", (player, groupId, args) => onCmdDebugVillage(player, groupId, args, api), Privilege.controlserver);
        }

        private void onCmdDebugVillage(IServerPlayer player, int groupId, CmdArgs args, ICoreServerAPI sapi)
        {
            if (args[0] == "square")
            {
                var grid = new VillageGrid();
                switch (args[1])
                {
                    case "small":
                        for (int i = 0; i < 4; i++)
                        {
                            for (int k = 0; k < 4; k++)
                            {
                                var structure = new WorldGenVillageStructure();
                                structure.AttachmentPoint = k;
                                grid.AddSmallStructure(structure, i, k);
                            }
                        }
                        break;
                    case "medium":
                        int point = 0;
                        for (int i = 0; i < 2; i++)
                        {
                            for (int k = 0; k < 2; k++)
                            {
                                var structure = new WorldGenVillageStructure();
                                structure.AttachmentPoint = point;
                                grid.AddMediumStructure(structure, i, k);
                                point++;
                            }
                        }
                        break;
                    case "big":
                        grid.AddBigStructure(new WorldGenVillageStructure());
                        break;
                    case "random":
                        var medium = new WorldGenVillageStructure();
                        medium.Size = EnumVillageStructureSize.MEDIUM;
                        medium.AttachmentPoint = sapi.World.Rand.Next(0, 4);
                        grid.tryAddStructure(medium, sapi.World.Rand);
                        for (int i = 0; i < sapi.World.Rand.Next(0, 12); i++)
                        {
                            var small = new WorldGenVillageStructure();
                            small.Size = EnumVillageStructureSize.SMALL;
                            small.AttachmentPoint = sapi.World.Rand.Next(0, 4);
                            grid.tryAddStructure(small, sapi.World.Rand);
                        }
                        break;
                }
                grid.connectStreets();
                player.SendMessage(GlobalConstants.AllChatGroups, grid.debugPrintGrid(), EnumChatType.CommandSuccess);
            }
        }

        private void mapHandler(IMapRegion mapRegion, int regionX, int regionZ)
        {
            throw new NotImplementedException();
        }

        private void handler(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams)
        {
            throw new NotImplementedException();
        }
    }
}