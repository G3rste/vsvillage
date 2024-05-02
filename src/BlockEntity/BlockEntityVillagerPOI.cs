using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public abstract class BlockEntityVillagerPOI : BlockEntity
    {
        public string VillageId { get; set; }
        public string VillageName { get; set; }

        public Vec3d Position => Pos.ToVec3d();

        public abstract void AddToVillage(Village village);
        public abstract void RemoveFromVillage(Village village);
        public abstract bool BelongsToVillage(Village village);

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Client) return;

            var village = string.IsNullOrEmpty(VillageId)
                ? api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(Pos)
                : api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId);
            if (village != null && !BelongsToVillage(village))
            {
                VillageId = village.Id;
                VillageName = village.Name;
                AddToVillage(village);
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
            RemoveFromVillage(Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId));
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
                dsc.AppendLine().Append(Lang.Get("vsvillage:resides-in", Lang.Get(VillageName)));
            }
        }
    }
}