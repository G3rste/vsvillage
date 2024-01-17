using System.Runtime.CompilerServices;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityBehaviorVillagerBed : BlockEntityBehavior
    {

        public string VillageId { get; set; }
        public Vec3d Position => Blockentity.Pos.ToVec3d();

        public string Type => "villagerBed";

        public BlockEntityBehaviorVillagerBed(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            var sapi = api as ICoreServerAPI;
            if (sapi != null)
            {
                sapi.World.RegisterCallback(dt => (Blockentity as BlockEntityBed).MountedBy?.TryUnmount(), 500);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            var village = Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(Pos);
            village?.Beds.Add(new(){Pos = Pos});
            VillageId = village?.Id;
            Blockentity.MarkDirty();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId)?.Beds.RemoveAll(bed => bed.Pos.Equals(Pos));
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