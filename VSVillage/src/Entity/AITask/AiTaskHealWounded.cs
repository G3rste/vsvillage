using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskHealWounded : AiTaskGotoAndInteract
    {

        public Entity woundedEntity;
        public AiTaskHealWounded(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        }

        protected override Vec3d GetTargetPos()
        {
            var villagers = entity.World.GetEntitiesAround(entity.ServerPos.XYZ, maxDistance, 5, entity => entity is EntityVillager || entity is EntityTrader || entity is EntityPlayer);
            if (entity.GetBehavior<EntityBehaviorVillager>()?.Village != null)
            {
                villagers = villagers.Concat(entity.GetBehavior<EntityBehaviorVillager>().Village.Villagers.ConvertAll(villager => villager.entity).ToArray()).ToArray();
            }
            int maxHpLossIndex = 0;
            float maxHpLoss = 0;
            for (int i = 0; i < villagers.Length; i++)
            {
                var health = villagers[i].GetBehavior<EntityBehaviorHealth>();
                if (health != null && maxHpLoss < health.MaxHealth - health.Health)
                {
                    maxHpLoss = health.MaxHealth - health.Health;
                    maxHpLossIndex = i;
                }
                // prefer dead targets
                if (health != null && health.Health <= 0)
                {
                    maxHpLoss = int.MaxValue;
                    maxHpLossIndex = i;
                }
            }
            if (maxHpLoss > 0.5f)
            {
                woundedEntity = villagers[maxHpLossIndex];
            }
            return woundedEntity?.ServerPos?.XYZ;
        }

        protected override bool InteractionPossible()
        {
            return entity.ServerPos.SquareDistanceTo(woundedEntity.ServerPos) < 2 * 2;
        }

        protected override void ApplyInteractionEffect()
        {
            if (woundedEntity.Alive)
            {
                woundedEntity.ReceiveDamage(new DamageSource()
                {
                    DamageTier = 0,
                    HitPosition = woundedEntity.ServerPos.XYZ,
                    Source = EnumDamageSource.Internal,
                    SourceEntity = null, // otherwise the basegame wants to retaliate attacks ^^
                    Type = EnumDamageType.Heal
                }, 100);
            }
            else { woundedEntity.Revive(); }

            SimpleParticleProperties smoke = new SimpleParticleProperties(
                    10, 15,
                    ColorUtil.ToRgba(75, 146, 175, 122),
                    new Vec3d(),
                    new Vec3d(2, 1, 2),
                    new Vec3f(-0.25f, 0f, -0.25f),
                    new Vec3f(0.25f, 0f, 0.25f),
                    0.6f,
                    -0.075f,
                    0.5f,
                    3f,
                    EnumParticleModel.Quad
                )
            {
                MinPos = woundedEntity.ServerPos.XYZ.AddCopy(-1.5, -0.5, -1.5)
            };
            entity.World.SpawnParticles(smoke);
            woundedEntity = null;
        }
    }
}