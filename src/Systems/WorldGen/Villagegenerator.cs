using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace VsVillage
{
    public class VillageGenerator : ModStdWorldGen
    {

        public override double ExecuteOrder() => 0.45;

        public List<WorldGenVillageStructure> Structures;
        public Dictionary<string, List<string>> VillageNames;
        public List<VillageType> Villages;
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

        private TextCommandResult onCmdDebugVillage(TextCommandCallingArgs args)
        {
            VillageType villageType;
            if (args.ArgCount < 1)
            {
                villageType = Villages[sapi.World.Rand.Next(0, Villages.Count)];
            }
            else
            {
                string villageName = (string)args[0];
                villageType = Villages.Find(match => match.Code == villageName);
                if (villageType == null)
                {
                    return TextCommandResult.Error(string.Format("Could not find village with name {0}.", villageName));
                }
            }

            var grid = new VillageGrid(villageType.Length, villageType.Height);
            grid.Init(villageType, rand);
            var start = args.Caller.Player.Entity.ServerPos.XYZInt.ToBlockPos();
            if (args.ArgCount > 1 && (string)args[1] == "probeTerrain" && !probeTerrain(start, grid, sapi.World.BlockAccessor))
            {
                return TextCommandResult.Error("Terrain is too steep/ damp for generating a village");
            }
            else
            {
                grid.connectStreets();

                Village village = new()
                {
                    Pos = grid.getMiddle(start),
                    Name = VillageNames[villageType.Names][rand.NextInt(villageType.Names.Length)],
                    Api = sapi,
                    Gatherplaces = new(),
                    Workstations = new(),
                    Beds = new(),
                    VillagerSaveData = new(),
                    Radius = VillageGrid.GridDistToMapDist(grid.width)
                };
                sapi.ModLoader.GetModSystem<VillageManager>().Villages.TryAdd(village.Id, village);
                grid.GenerateHouses(start, sapi.World.BlockAccessor, sapi.World);
                grid.GenerateStreets(start, sapi.World.BlockAccessor, sapi.World);
                return TextCommandResult.Success();
            }
        }

        private void initWorldGen()
        {
            LoadGlobalConfig(sapi);

            Structures = sapi.Assets.Get<List<WorldGenVillageStructure>>(new AssetLocation("vsvillage", "config/villagestructures.json"));
            Villages = sapi.Assets.Get<List<VillageType>>(new AssetLocation("vsvillage", "config/villagetypes.json"));
            VillageNames = sapi.Assets.Get<Dictionary<string, List<string>>>(new AssetLocation("vsvillage", "config/villagenames.json"));
            foreach (var structure in Structures)
            {
                sapi.Logger.Event("Loading structure {0}", structure.Code);
                structure.Init(sapi);
                foreach (var village in Villages)
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
            foreach (var village in Villages)
            {
                village.StructureGroups.Sort((a, b) => ((int)b.Size).CompareTo((int)a.Size));
            }


            var cmdApi = sapi.ChatCommands;
            var parsers = cmdApi.Parsers;
            cmdApi
                .Create("genvillage")
                .WithDescription("Generate a village right where you are standing right now.")
                .WithArgs(parsers.OptionalWordRange("villagetype", Villages.ConvertAll<string>(type => type.Code).ToArray()), parsers.OptionalWord("probeTerrain"))
                .RequiresPrivilege(Privilege.root)
                .WithExamples("genvillage tiny probeTerrain", "genvillage aged-village1")
                .HandleWith(onCmdDebugVillage);
        }

        private bool probeTerrain(BlockPos start, VillageGrid grid, IBlockAccessor blockAccessor)
        {
            int max;
            int min;
            int current;
            int tolerance = (grid.width * grid.height) * 4;
            int waterspots = 0;
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
                            if (i == 0 && k == 0 &&
                                blockAccessor.GetBlock(new BlockPos(start.X + coords.X, current + 1, start.Z + coords.Y, 0), BlockLayersAccess.Fluid).Id != 0)
                            {
                                waterspots++;
                            }
                        }
                    }
                    tolerance -= (max - min);
                }
            }
            return tolerance > 0 && waterspots < grid.width * grid.height / 2;
        }

        private void handler(IChunkColumnGenerateRequest request)
        {
            IMapRegion region = request.Chunks[0].MapChunk.MapRegion;

            if (request.ChunkX % 4 != 0 || request.ChunkZ % 4 != 0) { return; }
            if (rand.NextFloat() > Config.VillageChance) { return; }
            if (region.GeneratedStructures.Find(structure => structure.Group == "village") != null) { return; }

            var villageType = Villages[rand.NextInt(Villages.Count)];
            // we mock the grid here and do the expensive generation later
            var grid = new VillageGrid(villageType.Length, villageType.Height);
            var start = new BlockPos(chunksize * request.ChunkX, 0, chunksize * request.ChunkZ, 0);
            var end = grid.getEnd(start);

            // check if all chunks are generated, still throws a bunch of exceptions when travelling south but I dont know how to properly check if a chunk is generated/ loaded
            if (worldgenBlockAccessor.GetChunk(start.X / chunksize, 0, start.Z / chunksize) == null
                || worldgenBlockAccessor.GetChunk(start.X / chunksize, 0, end.Z / chunksize) == null
                || worldgenBlockAccessor.GetChunk(end.X / chunksize, 0, start.Z / chunksize) == null
                || worldgenBlockAccessor.GetChunk(end.X / chunksize, 0, end.Z / chunksize) == null) { return; }

            worldgenBlockAccessor.BeginColumn();
            if (probeTerrain(start, grid, worldgenBlockAccessor))
            {
                grid.Init(villageType, rand);
                region.GeneratedStructures.Add(new GeneratedStructure() { Code = grid.VillageType.Code, Group = "village", Location = new Cuboidi(start, end) });
                grid.connectStreets();
                Village village = new()
                {
                    Pos = grid.getMiddle(start),
                    Name = VillageNames[villageType.Names][rand.NextInt(villageType.Names.Length)],
                    Api = sapi,
                    Gatherplaces = new(),
                    Workstations = new(),
                    Beds = new(),
                    VillagerSaveData = new(),
                    Radius = VillageGrid.GridDistToMapDist(grid.width)
                };
                sapi.ModLoader.GetModSystem<VillageManager>().Villages.TryAdd(village.Id, village);
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