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
                        grid.AddBigStructure(new WorldGenVillageStructure(), 0, 0);
                        break;
                    case "random":
                        int width = sapi.World.Rand.Next(1, 3);
                        int height = sapi.World.Rand.Next(1, 3);
                        grid = new VillageGrid(width, height);
                        int structures = sapi.World.Rand.Next(0, grid.capacity/16);
                        for(int i = 0; i < structures; i++){
                            var structure = new WorldGenVillageStructure();
                            structure.Size = EnumVillageStructureSize.LARGE;
                            structure.AttachmentPoint = sapi.World.Rand.Next(0, 4);
                            grid.tryAddStructure(structure, sapi.World.Rand);
                        }
                        structures = sapi.World.Rand.Next(0, grid.capacity/4);
                        for(int i = 0; i < structures; i++){
                            var structure = new WorldGenVillageStructure();
                            structure.Size = EnumVillageStructureSize.MEDIUM;
                            structure.AttachmentPoint = sapi.World.Rand.Next(0, 4);
                            grid.tryAddStructure(structure, sapi.World.Rand);
                        }
                        structures = sapi.World.Rand.Next(0, grid.capacity);
                        for(int i = 0; i < structures; i++){
                            var structure = new WorldGenVillageStructure();
                            structure.Size = EnumVillageStructureSize.SMALL;
                            structure.AttachmentPoint = sapi.World.Rand.Next(0, 4);
                            grid.tryAddStructure(structure, sapi.World.Rand);
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