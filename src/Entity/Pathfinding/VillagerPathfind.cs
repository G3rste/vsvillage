using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;

namespace VsVillage
{
    public class VillagerPathfind
    {
        private VillagerAStarNew villagerAStar;
        private WaypointAStar waypointAStar;

        public VillagerPathfind(ICoreServerAPI sapi)
        {
            var blockAccessor = sapi.World.GetCachingBlockAccessor(true, true);
            villagerAStar = new VillagerAStarNew(blockAccessor);
            waypointAStar = new WaypointAStar(blockAccessor);
        }

        public BlockPos GetStartPos(Vec3d startPos)
        {
            return villagerAStar.GetStartPos(startPos);
        }

        public List<VillagerPathNode> FindPath(BlockPos start, BlockPos end, Village village)
        {
            var path = villagerAStar.FindPath(start, end, 5000);
            return path;
        }

        public List<Vec3d> FindPathAsWaypoints(BlockPos start, BlockPos end, Village village)
        {
            List<VillagerPathNode> nodes = FindPath(start, end, village);
            return nodes == null ? null : ToWaypoints(nodes);
        }
        public List<Vec3d> ToWaypoints(List<VillagerPathNode> path)
        {
            List<Vec3d> waypoints = new List<Vec3d>(path.Count + 1);
            for (int i = 1; i < path.Count; i++)
            {
                waypoints.Add(path[i].ToWaypoint());
            }

            return waypoints;
        }

    }
}