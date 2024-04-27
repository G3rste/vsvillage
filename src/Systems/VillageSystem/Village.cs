using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

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
        public List<EntityBehaviorVillager> Villagers => VillagerSaveData.ConvertAll(data => Api.World.GetEntityById(data.Id)?.GetBehavior<EntityBehaviorVillager>());

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
                waypointDict.TryAdd(waypoint.Pos, waypoint);
            }
            foreach (var waypoint in Waypoints)
            {
                foreach (var neighbour in waypoint._Neighbours)
                {
                    if (waypointDict.TryGetValue(neighbour.Key, out var node))
                    {
                        waypoint.SetNeighbour(node, neighbour.Value);
                    }
                    else { waypoint._Neighbours.Remove(neighbour.Key); }
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
}