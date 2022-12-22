using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public class ItemVillagerHorn : Item
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return "eat";
        }
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            handling = EnumHandHandling.PreventDefault;
            if (byEntity.Api?.Side == EnumAppSide.Server)
            {
                byEntity.World?.PlaySoundAt(new AssetLocation("vsvillage:sounds/horn.ogg"), byEntity.ServerPos.X, byEntity.ServerPos.Y, byEntity.ServerPos.Z);
                spawnVillager(byEntity, blockSel);

                if (!(byEntity is EntityPlayer) || (byEntity as EntityPlayer).Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }
            }
        }

        private void spawnVillager(EntityAgent byEntity, BlockSelection blockSel)
        {
            string villagerId;
            var sapi = byEntity.Api;
            if (sapi.ModLoader.IsModEnabled("vsquest"))
            {
                villagerId = new string[] { "vsvillage:humanoid-villager-male-mayor", "vsvillage:humanoid-villager-female-mayor" }[sapi.World.Rand.Next(2)];
            }
            else
            {
                string sex = new string[] { "male", "female" }[sapi.World.Rand.Next(2)];
                string profession = new string[] { "soldier", "smith", "farmer", "shepherd", "herbalist", "mayor", "trader" }[sapi.World.Rand.Next(2)];
                villagerId = "vsvillage:humanoid-villager-" + sex + "-" + profession;
            }
            AssetLocation location = new AssetLocation(villagerId);
            EntityProperties type = byEntity.World.GetEntityType(location);
            if (type == null)
            {
                byEntity.World.Logger.Error("ItemCreature: No such entity - {0}", location);
                return;
            }

            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.ServerPos.Yaw = (float)byEntity.World.Rand.NextDouble() * 2 * GameMath.PI;

                entity.Pos.SetFrom(entity.ServerPos);
                entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

                entity.Attributes.SetString("origin", "summoned");

                byEntity.World.SpawnEntity(entity);
            }

            SimpleParticleProperties smoke = new SimpleParticleProperties(
                    50, 100,
                    ColorUtil.ToRgba(75, 169, 169, 169),
                    new Vec3d(),
                    new Vec3d(2, 1, 2),
                    new Vec3f(-0.25f, 0f, -0.25f),
                    new Vec3f(0.25f, 0f, 0.25f),
                    3f,
                    -0.075f,
                    0.5f,
                    3f,
                    EnumParticleModel.Quad
                );

            smoke.MinPos = blockSel.Position.ToVec3d();
            byEntity.World.SpawnParticles(smoke);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return secondsUsed < 3;
        }
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {

            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "vsvillage:interact-villager-horn",
                    MouseButton = EnumMouseButton.Right,
                }
            };
        }
    }
}