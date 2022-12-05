using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTraskVillagerFillTrough : AiTaskGotoAndInteract
    {
        BlockEntityTrough nearestTrough;

        public AiTraskVillagerFillTrough(EntityAgent entity) : base(entity)
        {
        }

        protected override Vec3d GetTargetPos()
        {
            nearestTrough = entity.Api.ModLoader.GetModSystem<POIRegistry>().GetNearestPoi(entity.ServerPos.XYZ, maxDistance, poi => poi is BlockEntityTrough) as BlockEntityTrough;
            return nearestTrough?.Position;
        }

        protected override void ApplyInteractionEffect()
        {
            Item troughContent = nearestTrough.Inventory[0].Empty ? entity.World.GetItem(new AssetLocation("grain-flax")) : nearestTrough.Inventory[0].Itemstack.Item;
            ItemSlot slot = new DummySlot(new ItemStack(troughContent, 16));
            var contentConfig = ItemSlotTrough.getContentConfig(entity.Api.World, nearestTrough.contentConfigs, slot);
            if (contentConfig != null && troughContent != null)
            {
                slot.TryPutInto(entity.World, nearestTrough.Inventory[0], contentConfig.QuantityPerFillLevel);
                nearestTrough.Inventory[0].MarkDirty();

                SimpleParticleProperties grain = new SimpleParticleProperties(
                        10, 15,
                        ColorUtil.ToRgba(255, 255, 233, 83),
                        nearestTrough.Position.AddCopy(-0.4, 0.8, -0.4),
                        nearestTrough.Position.AddCopy(-0.6, 0.8, -0.6),
                        new Vec3f(-0.25f, 0f, -0.25f),
                        new Vec3f(0.25f, 0f, 0.25f),
                        2f,
                        1f,
                        0.2f,
                        1f,
                        EnumParticleModel.Cube
                    );

                grain.MinPos = targetPos.AddCopy(0.5, 1, 0.5);
                entity.World.SpawnParticles(grain);
            }
        }
    }
}