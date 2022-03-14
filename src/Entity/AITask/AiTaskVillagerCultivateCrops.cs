using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTraskVillagerCultivateCrops : AiTaskBase
    {
        private float maxDistance { get; set; }

        private float moveSpeed;
        private VillagerWaypointsTraverser villagerPathTraverser;
        private long lastCheck;

        BlockEntityFarmland nearestFarmland;
        private bool stuck;

        public AiTraskVillagerCultivateCrops(EntityAgent entity) : base(entity)
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
                if (nearestFarmland == null || entity.ServerPos.SquareDistanceTo(nearestFarmland.Position) > maxDistance * maxDistance * 4)
                {
                    nearestFarmland = entity.Api.ModLoader.GetModSystem<POIRegistry>().GetNearestPoi(entity.ServerPos.XYZ, maxDistance, isValidFarmland) as BlockEntityFarmland;
                }
                if (nearestFarmland != null) { return true; }
            }
            return false;
        }

        public override void StartExecute()
        {
            if (nearestFarmland != null)
            {
                stuck = !villagerPathTraverser.NavigateTo(nearestFarmland.Position, moveSpeed, 0.5f, () => { }, () => stuck = true, true, 10000);
            }
            else
            {
                stuck = true;
            }
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            return !stuck && entity.ServerPos.SquareDistanceTo(nearestFarmland.Position) > 1.5f * 1.5f;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            var interactAnim = new AnimationMetaData
            {
                Code = "Interact",
                Animation = "interact"
            }.Init();
            entity.AnimManager.StartAnimation(interactAnim);
            if (nearestFarmland.CanPlant())
            {
                nearestFarmland.TryPlant(entity.World.GetBlock(new AssetLocation("crop-flax-1")));
            }
            else if (nearestFarmland.HasRipeCrop())
            {
                var crop = entity.World.BlockAccessor.GetBlock(nearestFarmland.UpPos) as BlockCrop;
                entity.Api.Logger.Debug("Harvested: {0}", crop.Code.Path);
                entity.World.BlockAccessor.BreakBlock(nearestFarmland.UpPos, null, 0);
                nearestFarmland.TryPlant(entity.World.GetBlock(crop.CodeWithParts("1")));
            }
            nearestFarmland = null;
        }

        private bool isValidFarmland(IPointOfInterest poi)
        {
            var farmland = poi as BlockEntityFarmland;
            return farmland != null && (farmland.HasRipeCrop() || farmland.CanPlant());
        }
    }
}