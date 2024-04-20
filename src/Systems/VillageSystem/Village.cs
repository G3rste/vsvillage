using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using System;
using Vintagestory.GameContent;
using System.Linq;

namespace VsVillage
{
    [ProtoContract(ImplicitFields = ImplicitFields.None)]
    public class Village
    {
        public string Id => "village-" + Pos.ToString();
        [ProtoMember(1)]
        public BlockPos Pos;
        [ProtoMember(2)]
        public int Radius;
        [ProtoMember(3)]
        public string Name;
        [ProtoMember(4)]
        public List<VillagerBed> Beds = new();
        [ProtoMember(5)]
        public List<VillagerWorkstation> Workstations = new();
        [ProtoMember(6)]
        public List<BlockPos> Gatherplaces = new();
        [ProtoMember(7)]
        public List<VillagerData> VillagerSaveData = new();
        [ProtoMember(8)]
        public List<VillageWaypoint> Waypoints = new();

        public ICoreAPI Api;
        public List<EntityVillager> Villagers => VillagerSaveData.ConvertAll(data => Api.World.GetEntityById(data.Id) as EntityVillager);

        public void Init(ICoreAPI api)
        {
            Api = api;
            InitWayPoints();
        }

        public BlockPos FindFreeBed(long villagerId)
        {
            foreach (var bed in Beds)
            {
                if (bed.OwnerId == -1 || bed.OwnerId == villagerId)
                {
                    bed.OwnerId = villagerId;
                    return bed.Pos;
                }
            }
            return null;
        }

        public BlockPos FindFreeWorkstation(long villagerId, string profession)
        {
            foreach (var workstation in Workstations)
            {
                if (workstation.Profession == profession && (workstation.OwnerId == -1 || workstation.OwnerId == villagerId))
                {
                    workstation.OwnerId = villagerId;
                    return workstation.Pos;
                }
            }
            return null;
        }

        public BlockPos FindRandomGatherplace()
        {
            if (Gatherplaces.Count == 0)
            {
                return null;
            }
            return Gatherplaces[Api.World.Rand.Next(Gatherplaces.Count)];
        }

        public VillageWaypoint FindNearesWaypoint(BlockPos pos)
        {
            VillageWaypoint result = null;
            foreach (var waypoint in Waypoints)
            {
                if (result == null || waypoint.Pos.ManhattenDistance(pos) < result.Pos.ManhattenDistance(pos))
                {
                    result = waypoint;
                }
            }
            return result;
        }

        public void DoDijkstraSimilarStuff()
        {
            for (int i = 0; i < Waypoints.Count * Waypoints.Count; i++)
            {
                Waypoints[i % Waypoints.Count].UpdateReachableNodes();
            }
        }

        public void InitWayPoints()
        {
            var waypointDict = new Dictionary<BlockPos, VillageWaypoint>();
            foreach (var waypoint in Waypoints)
            {
                waypointDict.Add(waypoint.Pos, waypoint);
            }
            foreach (var waypoint in Waypoints)
            {
                foreach (var neighbour in waypoint._Neighbours)
                {
                    waypoint.SetNeighbour(waypointDict[neighbour.Key], neighbour.Value);
                }
                foreach (var reachable in waypoint._ReachableNodes)
                {
                    reachable.Value.NextWaypoint = waypointDict[reachable.Value._NextWaypoint];
                    waypoint.SetReachableNode(waypointDict[reachable.Key], reachable.Value);
                }
            }
        }

        public void RemoveWaypoint(VillageWaypoint waypoint)
        {
            foreach (var element in Waypoints)
            {
                element.RemoveNeighbour(waypoint);
            }
            RecalculateWaypoints();
        }

        public void RemoveWaypoint(BlockPos pos)
        {
            var waypoint = Waypoints.Find(waypoint => waypoint.Pos == pos);
            if (waypoint != null)
            {
                RemoveWaypoint(waypoint);
                Waypoints.Remove(waypoint);
                RecalculateWaypoints();
            }
        }

        public void RecalculateWaypoints()
        {
            foreach (var element in Waypoints)
            {
                element.ReachableNodes = new();
                element._ReachableNodes = new();
            }
            InitWayPoints();
            DoDijkstraSimilarStuff();
        }
    }

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
                    return null;
                }
                result.Add(next);
                current = next;
            }
            return result;
        }

        public VillageWaypoint GetNextWaypoint(VillageWaypoint target)
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

        public override bool Equals(object obj)
        {
            return Equals(Pos, (obj as VillageWaypoint)?.Pos);
        }

        public override int GetHashCode()
        {
            return Pos?.GetHashCode() ?? -1;
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.None)]
    public class VillageWaypointPath
    {
        [ProtoMember(1)]
        public int Distance;
        [ProtoMember(2)]
        public BlockPos _NextWaypoint;
        public VillageWaypoint NextWaypoint;
    }
}