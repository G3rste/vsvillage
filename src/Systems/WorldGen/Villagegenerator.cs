using System;
using System.Collections.Generic;
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
        private ICoreServerAPI sapi;

        private IWorldGenBlockAccessor worldgenBlockAccessor;

        public override double ExecuteOrder() => 0.45;

        public List<WorldGenVillageStructure> structures;
        public List<VillageType> villages;
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.InitWorldGenerator(initWorldGen, "standard");
            api.Event.ChunkColumnGeneration(handler, EnumWorldGenPass.TerrainFeatures, "standard");
            api.Event.MapRegionGeneration(mapHandler, "standard");
            api.Event.GetWorldgenBlockAccessor(chunkProvider => worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false));

            api.RegisterCommand("genvillage", "debug command for printing village layout", "[square|village]", (player, groupId, args) => onCmdDebugVillage(player, groupId, args, api), Privilege.controlserver);
        }

        private void onCmdDebugVillage(IServerPlayer player, int groupId, CmdArgs args, ICoreServerAPI sapi)
        {
            var grid = new VillageGrid();

            var village = villages.Find(match => match.Code == args[0]);
            grid = village.genVillageGrid(sapi.World.Rand);
            var start = player.Entity.ServerPos.XYZInt.ToBlockPos();
            if (args.Length > 1 && args[1] == "probe" && !probeTerrain(start, grid, sapi.World.BlockAccessor))
            {
                player.SendMessage(GlobalConstants.AllChatGroups, "Terrain is too steep for generating a village", EnumChatType.CommandError);
            }
            else
            {
                grid.connectStreets();
                player.SendMessage(GlobalConstants.AllChatGroups, grid.debugPrintGrid(), EnumChatType.CommandSuccess);

                grid.GenerateHouses(start, sapi.World.BlockAccessor, sapi.World);
                grid.GenerateStreets(start, sapi.World.BlockAccessor, sapi.World);
            }
        }

        private bool probeTerrain(BlockPos start, VillageGrid grid, IBlockAccessor blockAccessor)
        {
            int max;
            int min;
            int current;
            int tolerance = (grid.width + grid.height) / 3;
            for (int x = 0; x < grid.width - 1; x++)
            {
                for (int z = 0; z < grid.height - 1; z++)
                {
                    max = blockAccessor.GetTerrainMapheightAt(start);
                    min = max;
                    for (int i = 0; i < 2; i++)
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            var coords = grid.GridCoordsToMapCoords(x + i, z + k);
                            current = blockAccessor.GetTerrainMapheightAt(start.AddCopy(coords.X, 0, coords.Y));
                            max = Math.Max(max, current);
                            min = Math.Min(min, current);
                        }
                    }
                    if (min + 10 < max) { tolerance--; }
                }
            }
            return tolerance > 0;
        }

        private void initWorldGen()
        {
            LoadGlobalConfig(sapi);
            chunksize = sapi.World.BlockAccessor.ChunkSize;

            structures = sapi.Assets.Get<List<WorldGenVillageStructure>>(new AssetLocation("vsvillage", "config/villagestructures.json"));
            villages = sapi.Assets.Get<List<VillageType>>(new AssetLocation("vsvillage", "config/villagetypes.json"));
            foreach (var structure in structures)
            {
                sapi.Logger.Event("Loading structure {0}", structure.Code);
                structure.Init(sapi);
                foreach (var village in villages)
                {
                    foreach (var group in village.StructureGroups)
                    {
                        if (structure.Group == group.Code && structure.Size == group.Size)
                        {
                            group.MatchingStructures.Add(structure);
                        }
                    }
                }
            }
            foreach (var village in villages)
            {
                village.StructureGroups.Sort((a, b) => ((int)b.Size).CompareTo((int)a.Size));
            }
        }

        private void mapHandler(IMapRegion mapRegion, int regionX, int regionZ)
        {
            //throw new NotImplementedException();
        }

        private void handler(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams)
        {
            IMapRegion region = chunks[0].MapChunk.MapRegion;
            if (villages != null && chunkX % 8 == 0 && chunkZ % 8 == 0)
            {
                worldgenBlockAccessor.BeginColumn();
                sapi.Logger.Debug("Try generating village.");
                var grid = villages[sapi.World.Rand.Next(0, villages.Count)].genVillageGrid(sapi.World.Rand);
                var start = new BlockPos(chunksize * chunkX, 0, chunksize * chunkZ);
                sapi.Logger.Debug("Checking terrain.");
                if (probeTerrain(start, grid, worldgenBlockAccessor))
                {
                    sapi.Logger.Debug("Try spawning village.");
                    grid.connectStreets();
                    grid.GenerateHouses(start, worldgenBlockAccessor, sapi.World);
                    grid.GenerateStreets(start, worldgenBlockAccessor, sapi.World);
                }
            }
        }
    }
}