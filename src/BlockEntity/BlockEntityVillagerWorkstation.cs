using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityVillagerWorkstation : BlockEntity
    {

        public string villageId { get; set; }

        public Vec3d Position => Pos.ToVec3d();

        public string Type => Block.Variant["profession"];
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            Api.ModLoader.GetModSystem<VillageManager>().GetVillage(Pos)?.Workstations.Add(new(){Pos = Pos});
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            Api.ModLoader.GetModSystem<VillageManager>().GetVillage(villageId)?.Workstations.RemoveAll(workstation => workstation.Pos.Equals(Pos));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            villageId = tree.GetString("villageId");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("villageId", villageId);
        }
    }
}