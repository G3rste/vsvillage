using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
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

            api.RegisterCommand("genvillage", "debug command for printing village layout", "[square|village]", (player, groupId, args) => onCmdDebugVillage(player, groupId, args, api), Privilege.controlserver);
        }

        private void onCmdDebugVillage(IServerPlayer player, int groupId, CmdArgs args, ICoreServerAPI sapi)
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
                            grid.AddSmallStructure(structure, i, k, 0);
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
                            structure.AttachmentPoint = 0;
                            grid.AddMediumStructure(structure, i, k, 0);
                            point++;
                        }
                    }
                    break;
                case "big":
                    grid.AddBigStructure(new WorldGenVillageStructure(), 0, 0, 0);
                    break;
                case "random":
                    int width = sapi.World.Rand.Next(2, 5);
                    int height = sapi.World.Rand.Next(2, 5);
                    grid = new VillageGrid(width, height);
                    int structures = sapi.World.Rand.Next(0, grid.capacity / 32);
                    for (int i = 0; i < structures; i++)
                    {
                        var structure = new WorldGenVillageStructure();
                        structure.Size = EnumVillageStructureSize.LARGE;
                        structure.AttachmentPoint = sapi.World.Rand.Next(0, 4);
                        grid.tryAddStructure(structure, sapi.World.Rand);
                    }
                    structures = sapi.World.Rand.Next(0, grid.capacity / 8);
                    for (int i = 0; i < structures; i++)
                    {
                        var structure = new WorldGenVillageStructure();
                        structure.Size = EnumVillageStructureSize.MEDIUM;
                        structure.AttachmentPoint = sapi.World.Rand.Next(0, 4);
                        grid.tryAddStructure(structure, sapi.World.Rand);
                    }
                    structures = sapi.World.Rand.Next(0, grid.capacity / 2);
                    for (int i = 0; i < structures; i++)
                    {
                        var structure = new WorldGenVillageStructure();
                        structure.Size = EnumVillageStructureSize.SMALL;
                        structure.AttachmentPoint = sapi.World.Rand.Next(0, 4);
                        grid.tryAddStructure(structure, sapi.World.Rand);
                    }
                    break;
                case "custom":
                    grid = new VillageGrid(2, 2);
                    int.TryParse(args[2], out structures);
                    for (int i = 0; i < structures; i++)
                    {
                        var structure = new WorldGenVillageStructure();
                        structure.Size = EnumVillageStructureSize.LARGE;
                        structure.AttachmentPoint = 0;
                        structure.SchematicCode = "house-large-1";
                        structure.Init(sapi);
                        grid.tryAddStructure(structure, sapi.World.Rand);
                    }
                    int.TryParse(args[3], out structures);
                    for (int i = 0; i < structures; i++)
                    {
                        var structure = new WorldGenVillageStructure();
                        structure.Size = EnumVillageStructureSize.MEDIUM;
                        structure.AttachmentPoint = 0;
                        structure.SchematicCode = "house-medium-" + sapi.World.Rand.Next(1, 3);
                        structure.Init(sapi);
                        grid.tryAddStructure(structure, sapi.World.Rand);
                    }
                    int.TryParse(args[4], out structures);
                    for (int i = 0; i < structures; i++)
                    {
                        var structure = new WorldGenVillageStructure();
                        structure.Size = EnumVillageStructureSize.SMALL;
                        structure.AttachmentPoint = 0;
                        structure.SchematicCode = "house-small-" + sapi.World.Rand.Next(1, 7);
                        structure.Init(sapi);
                        grid.tryAddStructure(structure, sapi.World.Rand);
                    }
                    break;
            }
            grid.connectStreets();
            player.SendMessage(GlobalConstants.AllChatGroups, grid.debugPrintGrid(), EnumChatType.CommandSuccess);
            if (args[0] == "world")
            {
                grid.GenerateStreets(player.Entity.ServerPos.XYZInt, sapi.World);
                grid.GenerateDebugHouses(player.Entity.ServerPos.XYZInt, sapi.World);
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