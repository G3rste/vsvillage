using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    [ProtoContract(ImplicitFields = ImplicitFields.None)]
    public class VillageWaypoint
    {
        [ProtoMember(1)]
        public BlockPos Pos;
        [ProtoMember(2)]
        public Dictionary<BlockPos, int> _Neighbours = new();
        [ProtoMember(3)]
        public Dictionary<BlockPos, VillageWaypointPath> _ReachableNodes = new();
        public Dictionary<VillageWaypoint, int> Neighbours = new();

        public void SetNeighbour(VillageWaypoint newNeighbour, int distance)
        {
            Neighbours[newNeighbour] = distance;
            _Neighbours[newNeighbour.Pos] = distance;
        }
        public void RemoveNeighbour(VillageWaypoint waypoint)
        {
            Neighbours.Remove(waypoint);
            _Neighbours.Remove(waypoint.Pos);
        }
        public Dictionary<VillageWaypoint, VillageWaypointPath> ReachableNodes = new();
        public void SetReachableNode(VillageWaypoint waypoint, VillageWaypointPath path)
        {
            ReachableNodes[waypoint] = path;
            _ReachableNodes[waypoint.Pos] = path;
        }

        public List<VillageWaypoint> FindPath(VillageWaypoint target, int maxSearchDepth)
        {
            var current = this;
            var result = new List<VillageWaypoint>() { current };
            int searchDepth = 0;
            while (!current.Equals(target) && searchDepth++ < maxSearchDepth)
            {
                var next = current.GetNextWaypoint(target);
                if (next == null)
                {
                    return result;
                }
                result.Add(next);
                current = next;
            }
            return result;
        }

        private VillageWaypoint GetNextWaypoint(VillageWaypoint target)
        {
            if (Neighbours.ContainsKey(target))
            {
                return target;
            }
            ReachableNodes.TryGetValue(target, out var next);
            return next?.NextWaypoint;
        }

        // not optimal, but good enough, hoping that people wont do more than 20 per village
        public void UpdateReachableNodes()
        {
            foreach (var neighbour in Neighbours)
            {
                foreach (var neighbourReachableNode in neighbour.Key.Neighbours)
                {
                    ReachableNodes.TryGetValue(neighbourReachableNode.Key, out var reachableNode);
                    if (neighbourReachableNode.Key != this && (reachableNode == null || reachableNode.Distance > neighbourReachableNode.Value + neighbour.Value))
                    {
                        SetReachableNode(neighbourReachableNode.Key, new VillageWaypointPath()
                        {
                            NextWaypoint = neighbour.Key,
                            _NextWaypoint = neighbour.Key.Pos,
                            Distance = neighbour.Value + neighbourReachableNode.Value
                        });
                    }
                }
                foreach (var neighbourReachableNode in neighbour.Key.ReachableNodes)
                {
                    ReachableNodes.TryGetValue(neighbourReachableNode.Key, out var reachableNode);
                    if (neighbourReachableNode.Key != this && (reachableNode == null || reachableNode.Distance > neighbourReachableNode.Value.Distance + neighbour.Value))
                    {
                        SetReachableNode(neighbourReachableNode.Key, new VillageWaypointPath()
                        {
                            NextWaypoint = neighbour.Key,
                            _NextWaypoint = neighbour.Key.Pos,
                            Distance = neighbour.Value + neighbourReachableNode.Value.Distance
                        });
                    }
                }
            }
        }

        public VillageWaypoint FindNearestReachableWaypoint(BlockPos pos){
            VillageWaypoint result = null;
            foreach (var waypoint in Neighbours.Keys)
            {
                if (result == null || waypoint.Pos.ManhattenDistance(pos) < result.Pos.ManhattenDistance(pos))
                {
                    result = waypoint;
                }
            }
            foreach (var waypoint in ReachableNodes.Keys)
            {
                if (result == null || waypoint.Pos.ManhattenDistance(pos) < result.Pos.ManhattenDistance(pos))
                {
                    result = waypoint;
                }
            }
            return result;
        }

        public override bool Equals(object obj)
        {
            return Equals(Pos, (obj as VillageWaypoint)?.Pos);
        }

        public override int GetHashCode()
        {
            return Pos?.GetHashCode() ?? -1;
        }
    }
}