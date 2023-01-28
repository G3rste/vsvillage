using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityVillagerBrazier : BlockEntity, IPointOfInterest
    {
        public Vec3d Position => Pos.ToVec3d();

        public string Type => "freetime";
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api is ICoreServerAPI sapi) { sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this); }
            RegisterGameTickListener(dt => { if (Api.World.Calendar.FullHourOfDay < 17) Extinguish(); }, 5000);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            if (Api is ICoreServerAPI sapi) { sapi.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this); }
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