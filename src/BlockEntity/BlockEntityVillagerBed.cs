namespace VsVillage
{
    public class BlockEntityVillagerBed : BlockEntityVillagerPOI
    {
        public float Yaw => Block.Attributes["yaw"].AsFloat();
        public override void AddToVillage(Village village)
        {
            village?.Beds.Add(new() { OwnerId = -1, Pos = Pos });
        }

        public override void RemoveFromVillage(Village village)
        {
            village?.Beds.RemoveAll(bed => bed.Pos.Equals(Pos));
        }
    }
}