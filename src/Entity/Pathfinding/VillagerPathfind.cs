using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;

namespace VsVillage
{
    public class VillagerPathfind
    {
        private ICoreServerAPI sapi;
        private VillagerAStar villagerAStar;
        private WaypointAStar waypointAStar;
        public int NodesChecked => villagerAStar.NodesChecked + waypointAStar.NodesChecked;

        public VillagerPathfind(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            villagerAStar = new VillagerAStar(sapi);
            waypointAStar = new WaypointAStar(sapi);
        }

        public BlockPos GetStartPos(Vec3d startPos){
            return villagerAStar.GetStartPos(startPos);
        }

        public List<PathNode> FindPath(BlockPos start, BlockPos end, int maxFallHeight, float stepHeight, Village village)
        {
            var path = villagerAStar.FindPath(start, end, maxFallHeight, stepHeight, 4999);
            if (path == null && village != null && village.Waypoints.Count > 0)
            {
                var startWaypoint = village.FindNearesWaypoint(start);
                var endWaypoint = startWaypoint?.FindNearestReachableWaypoint(end);

                if (startWaypoint == null || endWaypoint == null) return null;
                var stops = startWaypoint.FindPath(endWaypoint, village.Waypoints.Count);
                endWaypoint = stops[stops.Count - 1];
                path = villagerAStar.FindPath(start, startWaypoint.Pos, maxFallHeight, stepHeight, 4999);
                if (path == null || stops == null) return null;
                for (int i = 0; i < stops.Count - 1; i++)
                {
                    var nextPath = waypointAStar.FindPath(stops[i].Pos, stops[i + 1].Pos, maxFallHeight, stepHeight);
                    if (nextPath == null) return path;
                    path.AddRange(nextPath);
                }
                var lastPath = villagerAStar.FindPath(endWaypoint.Pos, end, maxFallHeight, stepHeight, 4999);
                if (lastPath == null) return path;
                path.AddRange(lastPath);
            }
            return path;
        }

        public List<Vec3d> FindPathAsWaypoints(BlockPos start, BlockPos end, int maxFallHeight, float stepHeight, Village village)
        {
            List<PathNode> nodes = FindPath(start, end, maxFallHeight, stepHeight, village);
            return nodes == null ? null : ToWaypoints(nodes);
        }
        public List<Vec3d> ToWaypoints(List<PathNode> path)
        {
            List<Vec3d> waypoints = new List<Vec3d>(path.Count + 1);
            for (int i = 1; i < path.Count; i++)
            {
                waypoints.Add(path[i].ToWaypoint().Add(VillagerAStar.centerOffsetX, 0, VillagerAStar.centerOffsetZ));
            }

            return waypoints;
        }

    }
}