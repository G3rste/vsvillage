using Vintagestory.API.Common;

namespace VsVillage
{
    public class BlockEntityVillagerBrazier : BlockEntityVillagerPOI
    {

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(dt => { if (Api.World.Calendar.FullHourOfDay < 17) Extinguish(); }, 5000);
        }
        public override void AddToVillage(Village village)
        {
            village?.Gatherplaces.Add(Pos);
        }
        public override void RemoveFromVillage(Village village)
        {
            village?.Gatherplaces.Remove(Pos);
        }

        public void Extinguish()
        {
            if (Block.Variant["burnstate"] != "extinct")
            {
                var brazierExtinct = Api.World.GetBlock(Block.CodeWithVariant("burnstate", "extinct"));
                Api.World.BlockAccessor.ExchangeBlock(brazierExtinct.Id, Pos);
                this.Block = brazierExtinct;
            }
        }
        public void Ignite()
        {
            if (Block.Variant["burnstate"] != "lit")
            {
                var brazierLit = Api.World.GetBlock(Block.CodeWithVariant("burnstate", "lit"));
                Api.World.BlockAccessor.ExchangeBlock(brazierLit.Id, Pos);
                this.Block = brazierLit;
            }
        }
    }
}