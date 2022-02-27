using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerSeekEntity : AiTaskSeekEntity
    {

        protected long lastCheckTotalMs { get; set; }
        protected long lastCheckCooldown { get; set; } = 500;
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
                    (entity as EntityVillager)?.UndrawWeapon();
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
        }

        public override bool IsTargetableEntity(Entity e, float range, bool ignoreEntityCode = false)
        {
            if (e == attackedByEntity && e?.Alive == true) { return true; }
            return base.IsTargetableEntity(e, range, ignoreEntityCode);
        }
    }
}