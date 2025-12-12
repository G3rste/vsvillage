using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public class BlockEntityVillagerBed : BlockEntityVillagerPOI
    {
        public float Yaw => Block.Attributes["yaw"].AsFloat();

        public override void AddToVillage(Village village)
        {
            village.Beds[Pos] = new() { OwnerId = -1, Pos = Pos };
        }

        public override void RemoveFromVillage(Village village)
        {
            village?.Beds.Remove(Pos);
        }

        public override bool BelongsToVillage(Village village)
        {
            return village.Id == VillageId
                && village.Name == VillageName
                && village.Beds.ContainsKey(Pos);
        }
    }
}