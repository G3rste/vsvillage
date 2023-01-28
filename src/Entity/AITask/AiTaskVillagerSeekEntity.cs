using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerSeekEntity : AiTaskSeekEntity
    {

        protected long lastCheckTotalMs { get; set; }
        protected long lastCheckCooldown { get; set; } = 500;
        protected long lastCallForHelp { get; set; }
        public AiTaskVillagerSeekEntity(EntityAgent entity) : base(entity)
        {
        }

        public override bool ShouldExecute()
        {
            if (lastCheckTotalMs + lastCheckCooldown > entity.World.ElapsedMilliseconds) { return false; }
            lastCheckTotalMs = entity.World.ElapsedMilliseconds;

            if (targetEntity != null && targetEntity.Alive && targetEntity.ServerPos.SquareDistanceTo(entity.ServerPos.XYZ) < seekingRange * seekingRange * 2)
            {
                targetPos = targetEntity.ServerPos.XYZ;
                return true;
            }
            else { targetEntity = null; }

            if (attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.ServerPos.SquareDistanceTo(entity.ServerPos.XYZ) < seekingRange * seekingRange * 2)
            {
                targetEntity = attackedByEntity;
                targetPos = targetEntity.ServerPos.XYZ;
                return true;
            }
            else { attackedByEntity = null; }

            if (lastSearchTotalMs + searchWaitMs < entity.World.ElapsedMilliseconds)
            {
                lastSearchTotalMs = entity.World.ElapsedMilliseconds;
                targetEntity = partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, seekingRange, potentialTarget => IsTargetableEntity(potentialTarget, seekingRange));

                if (targetEntity != null && targetEntity.Alive && targetEntity.ServerPos.SquareDistanceTo(entity.ServerPos.XYZ) < seekingRange * seekingRange * 2)
                {
                    targetPos = targetEntity.ServerPos.XYZ;
                    return true;
                }
                else
                {
                    targetEntity = null;
                }
            }

            return false;
        }

        public override void StartExecute()
        {
            (entity as EntityVillager)?.DrawWeapon();
            base.StartExecute();
        }

        public override void OnEntityHurt(DamageSource source, float damage)
        {
            base.OnEntityHurt(source, damage);
            (entity as EntityVillager)?.DrawWeapon();
            if (source.Type != EnumDamageType.Heal && lastCallForHelp + 5000 < entity.World.ElapsedMilliseconds)
            {
                lastCallForHelp = entity.World.ElapsedMilliseconds;
                foreach (var villager in entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 15, 4, entity => (entity as EntityVillager)?.profession == "soldier"))
                {
                    var taskManager = villager.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                    taskManager.GetTask<AiTaskVillagerSeekEntity>().OnAllyAttacked(source.SourceEntity);
                    taskManager.GetTask<AiTaskVillagerMeleeAttack>().OnAllyAttacked(source.SourceEntity);
                    taskManager.GetTask<AiTaskVillagerRangedAttack>().OnAllyAttacked(source.SourceEntity);
                }
            }
        }

        public void OnAllyAttacked(Entity byEntity){
            if(targetEntity == null || !targetEntity.Alive){
                targetEntity = byEntity;
            }
        }

        public override bool IsTargetableEntity(Entity e, float range, bool ignoreEntityCode = false)
        {
            if (e == attackedByEntity && e?.Alive == true) { return true; }
            return base.IsTargetableEntity(e, range, ignoreEntityCode);
        }
    }
}