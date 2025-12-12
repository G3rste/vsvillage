using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class VillageCommands : ModSystem
    {

        BlockPos start;
        BlockPos end;

        ICoreServerAPI sapi;
        bool revertHighlightPaths = true;
        bool revertHighlightVillage = true;
        bool revertHighlightVillagerBelongings = true;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            var cmdApi = sapi.ChatCommands;
            var parsers = cmdApi.Parsers;
            cmdApi
                .Create("villagerpath")
                .WithAlias("vp")
                .WithDescription("A* path finding debug testing for villagers")
                .WithArgs(parsers.WordRange("stage", "start", "end"))
                .RequiresPrivilege(Privilege.root)
                .WithExamples("villagerpath start", "villagerpath end")
                .HandleWith(args => onCmdAStar(args));
            cmdApi
                .Create("waypointpath")
                .WithAlias("wp")
                .WithDescription("A* path finding debug testing for villagers")
                .WithArgs(parsers.WordRange("stage", "start", "end"))
                .RequiresPrivilege(Privilege.root)
                .WithExamples("waypointpath start", "waypointpath end")
                .HandleWith(args => onCmdAStar(args, new WaypointAStar(sapi.World.GetCachingBlockAccessor(true, true))));
            cmdApi
                .Create("highlightvillagewaypoints")
                .WithAlias("hvw")
                .WithDescription("Highlight all paths between waypoints in your village")
                .RequiresPrivilege(Privilege.root)
                .WithExamples("highlightvillagewaypoints", "hvw")
                .HandleWith(onCmdHighlightWaypoints);
            cmdApi
                .Create("highlightvillageplaces")
                .WithAlias("hvp")
                .WithDescription("Highlight this village center blue, the village border red, the beds yellow, the workstations green, the gather places purple and the waypoints turquoise")
                .RequiresPrivilege(Privilege.root)
                .WithExamples("highlightvillageplaces", "hvp")
                .HandleWith(onCmdHighlightPlaces);
            cmdApi
                .Create("highlightvillagersbelongings")
                .WithAlias("hvb")
                .WithDescription("Gets the closest villager/bed/workstation and highlight the villagers posistion red, its beds blue and workstation green")
                .RequiresPrivilege(Privilege.root)
                .WithExamples("highlightvillagersbelongings", "hvb")
                .HandleWith(onCmdHighlightVillagerBelongings);
        }

        private TextCommandResult onCmdHighlightVillagerBelongings(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            revertHighlightVillagerBelongings = !revertHighlightVillagerBelongings;
            if (revertHighlightVillagerBelongings)
            {
                sapi.World.HighlightBlocks(player, 1, new(), new List<int>() { ColorUtil.ColorFromRgba(0, 0, 128, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                sapi.World.HighlightBlocks(player, 2, new(), new List<int>() { ColorUtil.ColorFromRgba(128, 128, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                sapi.World.HighlightBlocks(player, 3, new(), new List<int>() { ColorUtil.ColorFromRgba(128, 0, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                return TextCommandResult.Success("Highlighted villager/bed/workstation been unhighlighted");
            }
            BlockPos plrPos = player.Entity.ServerPos.XYZ.AsBlockPos;
            var village = sapi.ModLoader.GetModSystem<VillageManager>().GetVillage(plrPos);
            if (village == null) return TextCommandResult.Error("No village found");
            var closestVillager = sapi.World.GetNearestEntity(plrPos.ToVec3d(), 10, 10, candidate => candidate is EntityVillager);
            var closestWorkstation = village.Workstations.Values.MinBy(candidate => candidate.Pos.DistanceTo(plrPos));
            var closestBed = village.Beds.Values.MinBy(candidate => candidate.Pos.DistanceTo(plrPos));
            var villagerDistance = closestVillager != null ? closestVillager.Pos.AsBlockPos.DistanceTo(plrPos) : float.MaxValue;
            var workstationDistance = closestWorkstation != null ? closestWorkstation.Pos.DistanceTo(plrPos) : float.MaxValue;
            var bedDistance = closestBed != null ? closestBed.Pos.DistanceTo(plrPos) : float.MaxValue;

            if (villagerDistance < bedDistance && villagerDistance < workstationDistance)
            {
                closestWorkstation = village.Workstations.Values.FirstOrDefault(candidate => candidate.OwnerId == closestVillager.EntityId);
                closestBed = village.Beds.Values.FirstOrDefault(candidate => candidate.OwnerId == closestVillager.EntityId);
            }
            else if (workstationDistance < bedDistance && workstationDistance < villagerDistance)
            {
                closestVillager = sapi.World.GetEntityById(closestWorkstation.OwnerId) as EntityVillager;
                closestBed = village.Beds.Values.FirstOrDefault(candidate => candidate.OwnerId == closestVillager?.EntityId);
            }
            else if (bedDistance < workstationDistance && bedDistance < villagerDistance)
            {
                closestVillager = sapi.World.GetEntityById(closestBed.OwnerId) as EntityVillager;
                closestWorkstation = village.Workstations.Values.FirstOrDefault(candidate => candidate.OwnerId == closestVillager?.EntityId);
            }
            else
            {
                return TextCommandResult.Error("No villager/bed/workstation could be found closeby");
            }

            var pos = village.Pos.Copy();
            pos.Y = sapi.World.BlockAccessor.GetTerrainMapheightAt(village.Pos);

            var workstation = closestWorkstation != null ? addBlockHeight(new List<BlockPos> { closestWorkstation.Pos }, 20) : new();
            var bed = closestBed != null ? addBlockHeight(new List<BlockPos> { closestBed.Pos }, 20) : new();
            var villager = closestVillager != null ? addBlockHeight(new List<BlockPos> { closestVillager.Pos.AsBlockPos }, 20) : new();

            villager = addBlockHeight(villager);

            sapi.World.HighlightBlocks(player, 1, workstation, new List<int>() { ColorUtil.ColorFromRgba(0, 0, 128, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            sapi.World.HighlightBlocks(player, 2, bed, new List<int>() { ColorUtil.ColorFromRgba(128, 128, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            sapi.World.HighlightBlocks(player, 3, villager, new List<int>() { ColorUtil.ColorFromRgba(128, 0, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            return TextCommandResult.Success("A villager together with is belonging bed and workstation have been highlighted.");
        }

        private TextCommandResult onCmdHighlightPlaces(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            revertHighlightVillage = !revertHighlightVillage;
            if (revertHighlightVillage)
            {
                sapi.World.HighlightBlocks(player, 4, new(), new List<int>() { ColorUtil.ColorFromRgba(0, 0, 128, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                sapi.World.HighlightBlocks(player, 5, new(), new List<int>() { ColorUtil.ColorFromRgba(0, 128, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                sapi.World.HighlightBlocks(player, 6, new(), new List<int>() { ColorUtil.ColorFromRgba(128, 128, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                sapi.World.HighlightBlocks(player, 7, new(), new List<int>() { ColorUtil.ColorFromRgba(128, 0, 128, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                sapi.World.HighlightBlocks(player, 8, new(), new List<int>() { ColorUtil.ColorFromRgba(0, 128, 128, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                sapi.World.HighlightBlocks(player, 9, new(), new List<int>() { ColorUtil.ColorFromRgba(128, 0, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                return TextCommandResult.Success("Highlighted Points of interest in this village have been unhighlighted");
            }
            BlockPos plrPos = player.Entity.ServerPos.XYZ.AsBlockPos;
            var village = sapi.ModLoader.GetModSystem<VillageManager>().GetVillage(plrPos);
            if (village == null) return TextCommandResult.Error("No village found");

            var pos = village.Pos.Copy();
            pos.Y = sapi.World.BlockAccessor.GetTerrainMapheightAt(village.Pos);

            var center = addBlockHeight(new List<BlockPos>() { pos }, 10);
            var workstations = addBlockHeight(village.Workstations.Keys.ToList(), 10);
            var beds = addBlockHeight(village.Beds.Keys.ToList(), 10);
            var gatherplaces = addBlockHeight(village.Gatherplaces.ToList(), 10);
            var waypoints = addBlockHeight(village.Waypoints.ToList());
            var border = addBlockHeight(new List<BlockPos>());

            for (var i = -village.Radius; i <= village.Radius; i++)
            {
                border.Add(pos.AddCopy(i, 0, -village.Radius));
                border.Add(pos.AddCopy(i, 0, village.Radius));
            }
            for (var k = -village.Radius + 1; k < village.Radius; k++)
            {
                border.Add(pos.AddCopy(-village.Radius, 0, k));
                border.Add(pos.AddCopy(village.Radius, 0, k));
            }

            border = addBlockHeight(border);

            sapi.World.HighlightBlocks(player, 4, center, new List<int>() { ColorUtil.ColorFromRgba(0, 0, 128, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            sapi.World.HighlightBlocks(player, 5, workstations, new List<int>() { ColorUtil.ColorFromRgba(0, 128, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            sapi.World.HighlightBlocks(player, 6, beds, new List<int>() { ColorUtil.ColorFromRgba(128, 128, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            sapi.World.HighlightBlocks(player, 7, gatherplaces, new List<int>() { ColorUtil.ColorFromRgba(128, 0, 128, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            sapi.World.HighlightBlocks(player, 8, waypoints, new List<int>() { ColorUtil.ColorFromRgba(0, 128, 128, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            sapi.World.HighlightBlocks(player, 9, border, new List<int>() { ColorUtil.ColorFromRgba(128, 0, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            return TextCommandResult.Success("All Points of interest in this village have been highlighted.");
        }

        private List<BlockPos> addBlockHeight(List<BlockPos> list, int height = 5)
        {
            var result = new List<BlockPos>();
            foreach (var pos in list)
            {
                for (int i = 0; i < height; i++)
                {
                    result.Add(pos.UpCopy(i));
                }
            }
            return result;
        }

        private TextCommandResult onCmdHighlightWaypoints(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            revertHighlightPaths = !revertHighlightPaths;
            if (revertHighlightPaths)
            {
                sapi.World.HighlightBlocks(player, 3, new List<BlockPos>(), new List<int>() { ColorUtil.ColorFromRgba(128, 0, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                return TextCommandResult.Success("Highlighted paths have been unhighlighted");
            }
            var waypointAStar = new WaypointAStar(sapi.World.GetCachingBlockAccessor(true, true));
            BlockPos plrPos = player.Entity.ServerPos.XYZ.AsBlockPos;
            HashSet<BlockPos> allPaths = new();
            var village = sapi.ModLoader.GetModSystem<VillageManager>().GetVillage(plrPos);
            if (village == null) return TextCommandResult.Error("No village found");

            sapi.World.HighlightBlocks(player, 3, new List<BlockPos>(allPaths), new List<int>() { ColorUtil.ColorFromRgba(128, 0, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            return TextCommandResult.Success("All paths have been highlighted");
        }

        private TextCommandResult onCmdAStar(TextCommandCallingArgs args, WaypointAStar waypointAStar = null)
        {
            string subcmd = (string)args[0];
            var player = args.Caller.Player;

            BlockPos plrPos = player.Entity.ServerPos.XYZ.AsBlockPos;
            VillagerAStarNew villagerPathfind = waypointAStar ?? new VillagerAStarNew(sapi.World.GetCachingBlockAccessor(true, true));


            Cuboidf narrow = new Cuboidf(-0.4f, 0, -0.4f, 0.4f, 1.5f, 0.4f);
            Cuboidf narrower = new Cuboidf(-0.2f, 0, -0.2f, 0.2f, 1.5f, 0.2f);
            Cuboidf wide = new Cuboidf(-0.6f, 0, -0.6f, 0.6f, 1.5f, 0.6f);

            Cuboidf collbox = narrow;
            const float stepHeight = 1.01f;


            switch (subcmd)
            {
                case "start":
                    start = villagerPathfind.GetStartPos(player.Entity.ServerPos.XYZ);
                    sapi.World.HighlightBlocks(player, 26, new List<BlockPos>() { start }, new List<int>() { ColorUtil.ColorFromRgba(255, 255, 0, 128) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                    break;
                case "end":
                    end = plrPos.Copy();
                    sapi.World.HighlightBlocks(player, 27, new List<BlockPos>() { end }, new List<int>() { ColorUtil.ColorFromRgba(255, 0, 255, 128) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                    break;
                case "bench":
                    if (start == null || end == null) return TextCommandResult.Deferred;

                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    for (int i = 0; i < 15; i++)
                    {
                        List<VillagerPathNode> nodes = villagerPathfind.FindPath(start, end);
                    }

                    sw.Stop();
                    float timeMs = (float)sw.ElapsedMilliseconds / 15f;

                    return TextCommandResult.Success(string.Format("15 searches average: {0} ms", (int)timeMs));

                case "clear":
                    start = null;
                    end = null;

                    sapi.World.HighlightBlocks(player, 2, new List<BlockPos>(), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                    sapi.World.HighlightBlocks(player, 26, new List<BlockPos>(), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                    sapi.World.HighlightBlocks(player, 27, new List<BlockPos>(), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                    break;
            }

            if (start == null || end == null)
            {
                sapi.World.HighlightBlocks(player, 2, new List<BlockPos>(), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
            }
            if (start != null && end != null)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                List<VillagerPathNode> nodes = villagerPathfind.FindPath(start, end);

                sw.Stop();
                int timeMs = (int)sw.ElapsedMilliseconds;

                if (nodes == null)
                {

                    sapi.World.HighlightBlocks(player, 2, new List<BlockPos>(), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                    sapi.World.HighlightBlocks(player, 3, new List<BlockPos>(), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
                    return TextCommandResult.Error("No path found");
                }

                List<BlockPos> poses = new List<BlockPos>();
                foreach (var node in nodes)
                {
                    poses.Add(node.BlockPos);
                }

                sapi.World.HighlightBlocks(player, 2, poses, new List<int>() { ColorUtil.ColorFromRgba(128, 128, 128, 30) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);


                List<Vec3d> wps = nodes.ConvertAll(node => node.BlockPos.ToVec3d());
                poses = new List<BlockPos>();
                foreach (var node in wps)
                {
                    poses.Add(node.AsBlockPos);
                }

                sapi.World.HighlightBlocks(player, 3, poses, new List<int>() { ColorUtil.ColorFromRgba(128, 0, 0, 100) }, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);

                return TextCommandResult.Success(string.Format("Search took {0} ms, {1} nodes checked", timeMs, 0));
            }
            return TextCommandResult.Deferred;
        }
    }
}