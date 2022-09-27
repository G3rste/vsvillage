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

        public override double ExecuteOrder() => 0.45;

        public List<WorldGenVillageStructure> structures;
        public List<VillageType> villages;
        public VillageConfig Config;
        private ICoreServerAPI sapi;

        private IWorldGenBlockAccessor worldgenBlockAccessor;
        private LCGRandom rand;
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            rand = new LCGRandom(sapi.World.Seed);
            api.Event.InitWorldGenerator(initWorldGen, "standard");
            api.Event.ChunkColumnGeneration(handler, EnumWorldGenPass.TerrainFeatures, "standard");
            api.Event.GetWorldgenBlockAccessor(chunkProvider => worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false));

            api.RegisterCommand("genvillage", "debug command for printing village layout", "[square|village]", (player, groupId, args) => onCmdDebugVillage(player, groupId, args, api), Privilege.controlserver);

            try
            {
                Config = api.LoadModConfig<VillageConfig>("villageconfig.json");
                if (Config != null)
                {
                    api.Logger.Notification("Mod Config successfully loaded.");
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    Config = new VillageConfig();
                }
            }
            catch
            {
                Config = new VillageConfig();
                api.Logger.Error("Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig(Config, "villageconfig.json");
            }
        }

        private void onCmdDebugVillage(IServerPlayer player, int groupId, CmdArgs args, ICoreServerAPI sapi)
        {
            VillageType village;
            if (args.Length < 1)
            {
                village = villages[sapi.World.Rand.Next(0, villages.Count)];
            }
            else
            {
                string villageName = args[0];
                village = villages.Find(match => match.Code == villageName);
                if (village == null)
                {
                    sapi.SendMessage(player, GlobalConstants.AllChatGroups, string.Format("Could not find village with name {0}.", villageName), EnumChatType.CommandError);
                    return;
                }
            }

            var grid = new VillageGrid(village.Length, village.Height);
            grid.Init(village, rand);
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
                    tolerance -= (max - min) / 10;
                }
            }
            return tolerance > 0;
        }

        private void handler(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams)
        {
            IMapRegion region = chunks[0].MapChunk.MapRegion;

            if (chunkX % 4 != 0 || chunkZ % 4 != 0) { return; }
            if (rand.NextFloat() > Config.VillageChance) { return; }
            if (region.GeneratedStructures.Find(structure => structure.Group == "village") != null) { return; }

            var village = villages[rand.NextInt(villages.Count)];
            // we mock the grid here and do the expensive generation later
            var grid = new VillageGrid(village.Length, village.Height);
            var start = new BlockPos(chunksize * chunkX, 0, chunksize * chunkZ);
            var end = grid.getEnd(start);

            // check if all chunks are generated, still throws a bunch of exceptions when travelling south but I dont know how to properly check if a chunk is generated/ loaded
            if (worldgenBlockAccessor.GetChunk(start.X / chunksize, 0, start.Z / chunksize) == null
                || worldgenBlockAccessor.GetChunk(start.X / chunksize, 0, end.Z / chunksize) == null
                || worldgenBlockAccessor.GetChunk(end.X / chunksize, 0, start.Z / chunksize) == null
                || worldgenBlockAccessor.GetChunk(end.X / chunksize, 0, end.Z / chunksize) == null) { return; }

            worldgenBlockAccessor.BeginColumn();
            if (probeTerrain(start, grid, worldgenBlockAccessor))
            {
                grid.Init(village, rand);
                region.GeneratedStructures.Add(new GeneratedStructure() { Code = grid.VillageType.Code, Group = "village", Location = new Cuboidi(start, end) });
                grid.connectStreets();
                grid.GenerateHouses(start, worldgenBlockAccessor, sapi.World);
                grid.GenerateStreets(start, worldgenBlockAccessor, sapi.World);
            }

        }
    }
    public class VillageConfig
    {
        public float VillageChance = 0.05f;
    }
}