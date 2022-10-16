using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityVillagerWorkstation : BlockEntity, IPointOfInterest
    {

        protected long? ownerId { get; set; }
        public EntityVillager owner { get => ownerId == null ? null : Api.World.GetEntityById((long)ownerId) as EntityVillager; }

        public Vec3d Position => Pos.ToVec3d();

        public string Type => Block.Variant["profession"];
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api is ICoreServerAPI sapi) { sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this); }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            if (Api is ICoreServerAPI sapi) { sapi.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this); }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            if (tree.HasAttribute("ownerId")) { ownerId = tree.GetLong("ownerId"); }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (ownerId != null) { tree.SetLong("ownerId", (long)ownerId); }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool setOwnerIfFree(long newOwner){
            if(ownerId == null || ownerId == newOwner || Api.World.GetEntityById((long)ownerId)?.Alive != true){
                ownerId = newOwner;
                return true;
            }else{
                return false;
            }
        }
    }
}