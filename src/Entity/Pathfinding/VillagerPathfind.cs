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

        public BlockPos GetStartPos(Vec3d startPos){
            return villagerAStar.GetStartPos(startPos);
        }

        public List<VillagerPathNode> FindPath(BlockPos start, BlockPos end, Village village)
        {
            var path = villagerAStar.FindPath(start, end);
            if (path == null && village != null && village.Waypoints.Count > 0 && end!= null)
            {
                var startWaypoint = village.FindNearesWaypoint(start);
                var endWaypoint = startWaypoint?.FindNearestReachableWaypoint(end);

                if (startWaypoint == null || endWaypoint == null) return null;
                var stops = startWaypoint.FindPath(endWaypoint, village.Waypoints.Count);
                endWaypoint = stops[stops.Count - 1];
                path = villagerAStar.FindPath(start, startWaypoint.Pos, 4999);
                if (path == null || stops == null) return null;
                for (int i = 0; i < stops.Count - 1; i++)
                {
                    var nextPath = waypointAStar.FindPath(stops[i].Pos, stops[i + 1].Pos);
                    if (nextPath == null) return path;
                    path.AddRange(nextPath);
                }
                var lastPath = villagerAStar.FindPath(endWaypoint.Pos, end, 4999);
                if (lastPath == null) return path;
                path.AddRange(lastPath);
            }
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