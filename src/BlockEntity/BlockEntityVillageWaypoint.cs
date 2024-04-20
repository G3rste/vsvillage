using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityVillagerWaypoint : BlockEntity
    {

        public string VillageId { get; set; }
        public string VillageName { get; set; }

        public Vec3d Position => Pos.ToVec3d();

        public string Type => Block.Variant["profession"];

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (string.IsNullOrEmpty(VillageId) && api is ICoreServerAPI sapi)
            {
                var village = Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(Pos);
                if (village != null)
                {
                    VillageId = village.Id;
                    VillageName = village.Name;
                    var waypoint = new VillageWaypoint() { Pos = Pos };
                    var waypointAStar = new WaypointAStar(sapi);
                    List<VillageWaypoint> potentialNeighbours = village.Waypoints.Where(waypoint => Pos.ManhattenDistance(waypoint.Pos)<50).ToList();
                    foreach(var candidate in potentialNeighbours){
                        var path = waypointAStar.FindPath(Pos, candidate.Pos, 1, 1f, 999);
                        if(path != null){
                            waypoint.SetNeighbour(candidate, path.Count);
                            candidate.SetNeighbour(waypoint, path.Count);
                        }
                    }
                    village.Waypoints.Add(waypoint);
                    village.RecalculateWaypoints();
                }
            }
            else
            {
                //load the village if not loaded
                Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(Pos)?.RemoveWaypoint(Pos);
        }

        public void RemoveVillage()
        {
            VillageId = null;
            VillageName = null;
            MarkDirty();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId)?.Waypoints.RemoveAll(waypoint => waypoint.Pos.Equals(Pos));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            VillageId = tree.GetString("villageId");
            VillageName = tree.GetString("villageName");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("villageId", VillageId);
            tree.SetString("villageName", VillageName);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (!string.IsNullOrEmpty(VillageName))
            {
                dsc.AppendLine().Append(Lang.Get("vsvillage:resides-in", VillageName));
            }
        }
    }
}