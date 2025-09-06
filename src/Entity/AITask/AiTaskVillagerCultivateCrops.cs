using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTraskVillagerCultivateCrops : AiTaskGotoAndInteract
    {
        BlockEntityFarmland nearestFarmland;

        public AiTraskVillagerCultivateCrops(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        }

        protected override Vec3d GetTargetPos()
        {
            nearestFarmland = entity.Api.ModLoader.GetModSystem<POIRegistry>().GetNearestPoi(entity.ServerPos.XYZ, maxDistance, isValidFarmland) as BlockEntityFarmland;
            return nearestFarmland?.Position;
        }

        protected override void ApplyInteractionEffect()
        {
            if (nearestFarmland.HasUnripeCrop())
            {
                nearestFarmland.TryGrowCrop(entity.World.Calendar.TotalHours + nearestFarmland.GetHoursForNextStage() + 1);

                SimpleParticleProperties fertilizer = new SimpleParticleProperties(
                        10, 15,
                        ColorUtil.ToRgba(255, 255, 233, 83),
                        nearestFarmland.Position.AddCopy(-0.4, 0.8, -0.4),
                        nearestFarmland.Position.AddCopy(-0.6, 0.8, -0.6),
                        new Vec3f(-0.25f, 0f, -0.25f),
                        new Vec3f(0.25f, 0f, 0.25f),
                        2f,
                        1f,
                        0.2f,
                        1f,
                        EnumParticleModel.Cube
                    );

                fertilizer.MinPos = targetPos.AddCopy(0.5, 1, 0.5);
                entity.World.SpawnParticles(fertilizer);
            }
        }

        private bool isValidFarmland(IPointOfInterest poi)
        {
            var farmland = poi as BlockEntityFarmland;
            return farmland != null && farmland.HasUnripeCrop() && entity.World.Rand.NextDouble() < 0.2;
        }
    }
}