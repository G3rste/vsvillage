using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace VsVillage
{
    public class BlockEntityVillagerWaypoint : BlockEntityVillagerPOI
    {
        public override void AddToVillage(Village village)
        {
            if (village != null && Api is ICoreServerAPI sapi)
            {
                var waypoint = new VillageWaypoint() { Pos = Pos };
                var waypointAStar = new WaypointAStar(sapi);
                List<VillageWaypoint> potentialNeighbours = village.Waypoints.Values.Where(waypoint => Pos.ManhattenDistance(waypoint.Pos) < 50).ToList();
                foreach (var candidate in potentialNeighbours)
                {
                    var path = waypointAStar.FindPath(Pos, candidate.Pos, 2, 1.01f);
                    if (path != null)
                    {
                        waypoint.SetNeighbour(candidate, path.Count);
                        candidate.SetNeighbour(waypoint, path.Count);
                    }
                }
                village.Waypoints[Pos] = waypoint;
                village.RecalculateWaypoints();
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