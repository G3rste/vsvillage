using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityVillagerWorkstation : BlockEntity, IPointOfInterest
    {

        public long? ownerId { get; protected set; }
        public Entity owner { get => ownerId == null ? null : Api.World.GetEntityById((long)ownerId); }

        public Vec3d Position => Pos.ToVec3d();

        public string Type => Block.Variant["profession"];
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api is ICoreServerAPI sapi)
            {
                sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                sapi.World.RegisterGameTickListener(repopulate, 300000, 300000);
            }
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
        public bool setOwnerIfFree(long newOwner)
        {
            if (ownerId == null || ownerId == newOwner || Api.World.GetEntityById((long)ownerId)?.Alive != true)
            {
                ownerId = newOwner;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void repopulate(float dt)
        {
            if (ownerId == null || Api.World.GetEntityById((long)ownerId) == null)
            {
                var registry = Api.ModLoader.GetModSystem<POIRegistry>();
                var spawn = registry.GetNearestPoi(Position, 75, poi =>
                {
                    if (poi is BlockEntityBehaviorVillagerBed bed && (bed.ownerId == null || bed.owner?.Alive == false))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }) as BlockEntityBehaviorVillagerBed;
                if (spawn != null)
                {
                    var worker = spawnWorker(spawn.Blockentity.Pos);
                    spawn.setOwnerIfFree(worker.EntityId);
                    setOwnerIfFree(worker.EntityId);
                }
            }
        }

        private Entity spawnWorker(BlockPos location)
        {
            string workerCode = string.Format("vsvillage:humanoid-villager-{0}-{1}", new string[] { "male", "female" }[Api.World.Rand.Next(2)], Type);
            var entityType = Api.World.GetEntityType(new AssetLocation(workerCode));
            var entity = Api.World.ClassRegistry.CreateEntity(entityType);
            var blockAccessor = Api.World.BlockAccessor;
            BlockPos freeLocation = null;
            for (int i = -2; i < 2; i++)
            {
                for (int k = -2; k < 2; k++)
                {
                    BlockPos candidate = location.AddCopy(i, 0, k);
                    if (blockAccessor.GetBlock(candidate).CollisionBoxes == null
                        && blockAccessor.GetBlock(candidate.UpCopy()).CollisionBoxes == null
                        && blockAccessor.GetBlock(candidate.DownCopy()).SideSolid[BlockFacing.UP.Index])
                    {
                        freeLocation = candidate;
                    }
                }
            }
            entity.ServerPos.SetPos(freeLocation != null ? freeLocation.ToVec3d().Add(0.5, 0, 0.5) : location.ToVec3d().Add(0.5, 0, 0.5));
            Api.World.SpawnEntity(entity);
            return entity;
        }
    }
}