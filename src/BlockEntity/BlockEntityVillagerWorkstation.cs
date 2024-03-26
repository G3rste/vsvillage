using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public class BlockEntityVillagerWorkstation : BlockEntity
    {

        public string VillageId { get; set; }
        public string VillageName { get; set; }

        public Vec3d Position => Pos.ToVec3d();

        public string Type => Block.Variant["profession"];
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            var village = Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(Pos);
            village?.Workstations.Add(new() { OwnerId = -1, Pos = Pos, Profession = Type });
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
                village?.Workstations.Add(new() { OwnerId = -1, Pos = Pos, Profession = Type });
            }
            else
            {
                //load the village if not loaded
                Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId);
            }
        }

        public void RemoveVillage()
        {
            VillageId = null;
            VillageName = null;
            MarkDirty();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId)?.Workstations.RemoveAll(workstation => workstation.Pos.Equals(Pos));
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