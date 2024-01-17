using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityVillagerBrazier : BlockEntity
    {

        public string villageId { get; set; }

        public Vec3d Position => Pos.ToVec3d();

        public string Type => "freetime";
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            Api.ModLoader.GetModSystem<VillageManager>().GetVillage(Pos)?.Gatherplaces.Add(Pos);

        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            Api.ModLoader.GetModSystem<VillageManager>().GetVillage(villageId)?.Gatherplaces.Remove(Pos);
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