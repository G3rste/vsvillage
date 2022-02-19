using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityBehaviorVillagerBed : BlockEntityBehavior, IPointOfInterest
    {

        protected long? ownerId { get; set; }
        public EntityVillager owner { get => ownerId == null ? null : Api.World.GetEntityById((long)ownerId) as EntityVillager; set => ownerId = value.EntityId; }

        public Vec3d Position => Blockentity.Pos.ToVec3d();

        public string Type => "villagerBed";

        public BlockEntityBehaviorVillagerBed(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            var sapi = api as ICoreServerAPI;
            if (sapi != null) { sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this); }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            var sapi = Api as ICoreServerAPI;
            if (sapi != null) { sapi.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this); }
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
    }
}