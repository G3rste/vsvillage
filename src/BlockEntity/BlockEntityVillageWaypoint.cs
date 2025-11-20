using Vintagestory.API.Common;

namespace VsVillage
{
    public class BlockEntityVillagerWaypoint : BlockEntityVillagerPOI
    {
        public override void AddToVillage(Village village)
        {
            if (village != null && Api.Side == EnumAppSide.Server)
            {                
                village.Waypoints.Add(Pos);
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            var village = Api.ModLoader.GetModSystem<VillageManager>().GetVillage(VillageId);
            if (village != null)
            {
                village.Waypoints.Add(Pos);
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
                && village.Waypoints.Contains(Pos);
        }
    }
}