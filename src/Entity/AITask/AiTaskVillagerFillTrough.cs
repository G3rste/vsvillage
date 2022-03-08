using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTraskVillagerFillTrough : AiTaskBase
    {
        private float maxDistance { get; set; }

        private float moveSpeed;
        private VillagerWaypointsTraverser villagerPathTraverser;
        private long lastCheck;

        BlockEntityTrough nearestTrough;
        private bool stuck;

        public AiTraskVillagerFillTrough(EntityAgent entity) : base(entity)
        {
        }


        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            maxDistance = taskConfig["maxdistance"].AsFloat(5);
            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);


            villagerPathTraverser = entity.GetBehavior<EntityBehaviorAlternatePathtraverser>().villagerWaypointsTraverser;
        }

        public override bool ShouldExecute()
        {
            var elapsedMs = entity.World.ElapsedMilliseconds;
            if (cooldownUntilMs + lastCheck < elapsedMs)
            {
                lastCheck = elapsedMs;
                if (nearestTrough == null || entity.ServerPos.SquareDistanceTo(nearestTrough.Position) > maxDistance * maxDistance * 4)
                {
                    nearestTrough = entity.Api.ModLoader.GetModSystem<POIRegistry>().GetNearestPoi(entity.ServerPos.XYZ, maxDistance, poi => poi is BlockEntityTrough) as BlockEntityTrough;
                }
                if (nearestTrough != null) { return true; }
            }
            return false;
        }

        public override void StartExecute()
        {
            if (nearestTrough != null)
            {
                stuck = !villagerPathTraverser.NavigateTo(nearestTrough.Position, moveSpeed, 0.5f, () => { }, () => stuck = true, true, 10000);
            }
            else
            {
                stuck = true;
            }
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            return !stuck && entity.ServerPos.SquareDistanceTo(nearestTrough.Position) > 1.5f * 1.5f;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            var interactAnim = new AnimationMetaData
            {
                Code = "Interact",
                Animation = "interact"
            }.Init();
            if (nearestTrough.Inventory[0].Empty)
            {
                var slot = new DummySlot(new ItemStack(entity.World.GetItem(new AssetLocation("grain-flax")), 16));
                var contentConfig = ItemSlotTrough.getContentConfig(entity.Api.World, nearestTrough.contentConfigs, slot);
                if (contentConfig != null)
                {
                    slot.TryPutInto(entity.World, nearestTrough.Inventory[0], contentConfig.QuantityPerFillLevel);
                    entity.AnimManager.StartAnimation(interactAnim);
                }
            }
            else
            {
                var slot = new DummySlot(new ItemStack(nearestTrough.Inventory[0].Itemstack.Item, 16));
                var contentConfig = ItemSlotTrough.getContentConfig(entity.Api.World, nearestTrough.contentConfigs, slot);
                if (contentConfig != null)
                {
                    slot.TryPutInto(entity.World, nearestTrough.Inventory[0], contentConfig.QuantityPerFillLevel);
                    entity.AnimManager.StartAnimation(interactAnim);
                }
            }
        }
    }
}