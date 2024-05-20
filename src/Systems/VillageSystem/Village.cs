using System.Collections.Generic;
using System.Linq;
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
        public Dictionary<BlockPos, VillagerBed> Beds = new();
        [ProtoMember(5)]
        public Dictionary<BlockPos, VillagerWorkstation> Workstations = new();
        [ProtoMember(6)]
        public HashSet<BlockPos> Gatherplaces = new();
        [ProtoMember(7)]
        public Dictionary<long, VillagerData> VillagerSaveData = new();
        [ProtoMember(8)]
        public Dictionary<BlockPos, VillageWaypoint> Waypoints = new();

        public ICoreAPI Api;
        public List<EntityBehaviorVillager> Villagers => VillagerSaveData.Values.ToList().ConvertAll(data => Api.World.GetEntityById(data.Id)?.GetBehavior<EntityBehaviorVillager>());

        public void Init(ICoreAPI api)
        {
            Api = api;
            InitWayPoints();
        }

        public BlockPos FindFreeBed(long villagerId)
        {
            foreach (var bed in Beds.Values)
            {
                if (bed.OwnerId == -1 || bed.OwnerId == villagerId)
                {
                    bed.OwnerId = villagerId;
                    return bed.Pos;
                }
            }
            return null;
        }

        public BlockPos FindFreeWorkstation(long villagerId, EnumVillagerProfession profession)
        {
            foreach (var workstation in Workstations.Values)
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
            return Gatherplaces.ElementAt(Api.World.Rand.Next(Gatherplaces.Count));
        }

        public void RemoveVillager(long villagerId){
            VillagerSaveData.Remove(villagerId);
            foreach(var bed in Beds.Values){
                if (bed.OwnerId == villagerId){
                    bed.OwnerId = -1;
                }
            }
            foreach(var workstation in Workstations.Values){
                if (workstation.OwnerId == villagerId){
                    workstation.OwnerId = -1;
                }
            }
        }

        public VillageWaypoint FindNearesWaypoint(BlockPos pos)
        {
            VillageWaypoint result = null;
            foreach (var waypoint in Waypoints.Values)
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
            for (int i = 0; i < Waypoints.Count; i++)
            {
                foreach (var waypoint in Waypoints.Values)
                {
                    waypoint.UpdateReachableNodes();
                }
            }
        }

        public void InitWayPoints()
        {
            var waypointDict = new Dictionary<BlockPos, VillageWaypoint>();
            foreach (var waypoint in Waypoints.Values)
            {
                waypointDict.TryAdd(waypoint.Pos, waypoint);
            }
            foreach (var waypoint in Waypoints.Values)
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
            foreach (var element in Waypoints.Values)
            {
                element.RemoveNeighbour(waypoint);
            }
            RecalculateWaypoints();
        }

        public void RemoveWaypoint(BlockPos pos)
        {
            if (Waypoints.TryGetValue(pos, out var waypoint))
            {
                RemoveWaypoint(waypoint);
                Waypoints.Remove(waypoint.Pos);
                RecalculateWaypoints();
            }
        }

        public void RecalculateWaypoints()
        {
            foreach (var element in Waypoints.Values)
            {
                element.ReachableNodes = new();
                element._ReachableNodes = new();
            }
            InitWayPoints();
            DoDijkstraSimilarStuff();
        }
    }
}