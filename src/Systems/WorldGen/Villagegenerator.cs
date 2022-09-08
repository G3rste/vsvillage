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

        public override double ExecuteOrder() => 0.6;

        public List<WorldGenVillageStructure> structures;
        public List<VillageType> villages;
        public override void StartServerSide(ICoreServerAPI api)
        {
            //api.Event.ChunkColumnGeneration(handler, EnumWorldGenPass.TerrainFeatures, "standard");
            //api.Event.MapRegionGeneration(mapHandler, "standard");

            api.RegisterCommand("genvillage", "debug command for printing village layout", "[square|village]", (player, groupId, args) => onCmdDebugVillage(player, groupId, args, api), Privilege.controlserver);
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            if (api is ICoreServerAPI sapi)
            {
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
        }

        private void onCmdDebugVillage(IServerPlayer player, int groupId, CmdArgs args, ICoreServerAPI sapi)
        {
            var grid = new VillageGrid();
            switch (args[0])
            {
                case "preset":
                    var village = villages.Find(match => match.Code == args[1]);
                    grid = village.genVillageGrid(sapi.World.Rand);
                    break;
            }
            grid.connectStreets();
            player.SendMessage(GlobalConstants.AllChatGroups, grid.debugPrintGrid(), EnumChatType.CommandSuccess);

            grid.GenerateHouses(player.Entity.ServerPos.XYZInt, sapi.World);
            grid.GenerateStreets(player.Entity.ServerPos.XYZInt, sapi.World);
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