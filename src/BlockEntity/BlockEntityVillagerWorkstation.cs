using Vintagestory.API.Common;

namespace VsVillage
{
    public class BlockEntityVillagerWorkstation : BlockEntityVillagerPOI
    {

        public string Type => Block.Variant["profession"];

        public override void AddToVillage(Village village)
        {
            village?.Workstations.Add(new() { OwnerId = -1, Pos = Pos, Profession = Type });
        }

        public override void RemoveFromVillage(Village village)
        {
            village?.Workstations.RemoveAll(workstation => workstation.Pos.Equals(Pos));

        }
    }
}