using System;
using Vintagestory.API.Common;

namespace VsVillage
{
    public class BlockEntityVillagerWorkstation : BlockEntityVillagerPOI
    {

        public EnumVillagerProfession Type => Enum.Parse<EnumVillagerProfession>(Block.Variant["profession"]);

        public override void AddToVillage(Village village)
        {
            village.Workstations[Pos]= new() { OwnerId = -1, Pos = Pos, Profession = Type };
        }

        public override void RemoveFromVillage(Village village)
        {
            village?.Workstations.Remove(Pos);
        }

        public override bool BelongsToVillage(Village village)
        {
            return village.Id == VillageId
                && village.Name == VillageName
                && village.Workstations.ContainsKey(Pos);
        }
    }
}