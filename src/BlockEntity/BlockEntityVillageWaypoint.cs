using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace VsVillage
{
    public class BlockEntityVillagerWaypoint : BlockEntityVillagerPOI
    {
        public long listenerId;
        public override void AddToVillage(Village village)
        {
            if (village != null && Api.Side == EnumAppSide.Server)
            {
                var waypoint = new VillageWaypoint() { Pos = Pos };
                village.Waypoints[Pos] = waypoint;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            var village = Api.ModLoader.GetModSystem<VillageManager>().GetVillage(VillageId);
            if (village != null)
            {
                var waypoint = village.Waypoints[Pos];
                listenerId = Api.World.RegisterGameTickListener(dt => UpdateWaypoints(waypoint, village, dt), 60000);
            }
        }

        private void UpdateWaypoints(VillageWaypoint thisWaypoint, Village village, float dt)
        {
            var waypointAStar = new WaypointAStar(Api.World.GetCachingBlockAccessor(true, true));
            thisWaypoint.Neighbours.Keys.Where(neighbour => !village.Waypoints.ContainsKey(neighbour.Pos))
                 .ToList()
                 .ForEach(thisWaypoint.RemoveNeighbour);
            List<VillageWaypoint> potentialNeighbours = village.Waypoints.Values
                .Where(waypoint => Pos.ManhattenDistance(waypoint.Pos) < 50)
                .Where(waypoint => !thisWaypoint.Neighbours.ContainsKey(waypoint))
                .ToList();
            foreach (var candidate in potentialNeighbours)
            {
                var path = waypointAStar.FindPath(Pos, candidate.Pos, 200);
                if (path != null)
                {
                    thisWaypoint.SetNeighbour(candidate, path.Count);
                    candidate.SetNeighbour(thisWaypoint, path.Count);
                }
            }
            thisWaypoint.UpdateReachableNodes();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (Api.Side == EnumAppSide.Server)
            {
                Api.World.UnregisterGameTickListener(listenerId);
            }
        }
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (Api.Side == EnumAppSide.Server)
            {
                Api.World.UnregisterGameTickListener(listenerId);
            }
        }

        public override void RemoveFromVillage(Village village)
        {
            village?.RemoveWaypoint(Pos);
        }

        public override bool BelongsToVillage(Village village)
        {
            return village.Id == VillageId
                && village.Name == VillageName
                && village.Waypoints.ContainsKey(Pos);
        }
    }
}