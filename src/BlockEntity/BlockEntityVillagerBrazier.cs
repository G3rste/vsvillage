using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityVillagerBrazier : BlockEntity
    {

        public string VillageId { get; set; }

        public Vec3d Position => Pos.ToVec3d();

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            var village = Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(Pos);
            village?.Gatherplaces.Add(Pos);
            VillageId = village?.Id;
            MarkDirty();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId)?.Gatherplaces.Remove(Pos);
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

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            VillageId = tree.GetString("villageId");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("villageId", VillageId);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine().Append("Resides in: " + VillageId);
        }
    }
}