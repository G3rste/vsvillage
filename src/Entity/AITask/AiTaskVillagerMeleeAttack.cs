using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerMeleeAttack : AiTaskMeleeAttack
    {

        public AnimationMetaData baseAnimMeta { get; set; }
        public AnimationMetaData stabAnimMeta { get; set; }
        public AnimationMetaData slashAnimMeta { get; set; }
        public float unarmedDamage { get; set; }
        public float armedDamageMultiplier { get; set; }
        public AiTaskVillagerMeleeAttack(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            baseAnimMeta = animMeta;
            unarmedDamage = damage;
            armedDamageMultiplier = taskConfig["armedDamageMultiplier"].AsFloat(4);

            if (taskConfig["stabanimation"].Exists)
            {
                stabAnimMeta = new AnimationMetaData()
                {
                    Code = taskConfig["stabanimation"].AsString()?.ToLowerInvariant(),
                    Animation = taskConfig["stabanimation"].AsString()?.ToLowerInvariant(),
                    AnimationSpeed = taskConfig["stabanimationSpeed"].AsFloat(1f)
                }.Init();
            }

            if (taskConfig["slashanimation"].Exists)
            {
                slashAnimMeta = new AnimationMetaData()
                {
                    Code = taskConfig["slashanimation"].AsString()?.ToLowerInvariant(),
                    Animation = taskConfig["slashanimation"].AsString()?.ToLowerInvariant(),
                    AnimationSpeed = taskConfig["slashanimationSpeed"].AsFloat(1f)
                }.Init();
            }
        }

        public override bool IsTargetableEntity(Entity e, float range, bool ignoreEntityCode = false)
        {
            if (e == attackedByEntity && e?.Alive == true) { return true; }
            return base.IsTargetableEntity(e, range, ignoreEntityCode);
        }

        public override void StartExecute()
        {
            if (entity.RightHandItemSlot != null && !entity.RightHandItemSlot.Empty)
            {
                damage = Math.Max(entity.RightHandItemSlot.Itemstack.Item.AttackPower * armedDamageMultiplier, unarmedDamage);
                if (entity.RightHandItemSlot.Itemstack.Item.Code.Path.Contains("spear"))
                {
                    animMeta = stabAnimMeta;
                }
                else
                {
                    animMeta = slashAnimMeta;
                }
            }
            else
            {
                damage = unarmedDamage;
                animMeta = baseAnimMeta;
            }
            base.StartExecute();
        }

        public void OnAllyAttacked(Entity byEntity){
            if(targetEntity == null || !targetEntity.Alive){
                targetEntity = byEntity;
            }
        }
    }
}