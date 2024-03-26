using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public class BlockEntityVillagerBrazier : BlockEntity
    {

        public string VillageId { get; set; }
        public string VillageName { get; set; }

        public Vec3d Position => Pos.ToVec3d();

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            var village = Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(Pos);
            village?.Gatherplaces.Add(Pos);
            VillageId = village?.Id;
            VillageName = village?.Name;
            MarkDirty();
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (string.IsNullOrEmpty(VillageId))
            {
                var village = Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(Pos);
                VillageId = village?.Id;
                VillageName = village?.Name;
                village?.Gatherplaces.Add(Pos);
            }
            else
            {
                //load the village if not loaded
                Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId);
            }
        }

        public void RemoveVillage(){
            VillageId = null;
            VillageName = null;
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