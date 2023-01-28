using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerRangedAttack : AiTaskBaseTargetable
    {
        int durationMs;
        int releaseAtMs;
        long lastSearchTotalMs;

        float minVertDist = 2f;
        float minDist = 3f;
        float maxDist = 15f;

        protected int searchWaitMs = 7000;

        EntityPartitioning partitionUtil;

        float startTimeStamp = 0;
        bool didThrow;
        bool didRenderswitch;

        float minTurnAnglePerSec;
        float maxTurnAnglePerSec;
        float curTurnRadPerSec;

        protected EntityProperties projectileType;
        protected AssetLocation shootingSound = null;
        protected AssetLocation drawingsound = null;

        AnimationMetaData animationRelease = null;

        bool animStarted = false;
        float damage;
        public AiTaskVillagerRangedAttack(EntityAgent entity) : base(entity)
        {
        }
        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            durationMs = taskConfig["durationMs"].AsInt(1500);
            releaseAtMs = taskConfig["releaseAtMs"].AsInt(1000);
            minDist = taskConfig["minDist"].AsFloat(3f);
            minVertDist = taskConfig["minVertDist"].AsFloat(2f);
            maxDist = taskConfig["maxDist"].AsFloat(15f);

            projectileType = entity.World.GetEntityType(new AssetLocation(taskConfig["projectile"].AsString()));
            if (taskConfig["drawingsound"].Exists)
            {
                drawingsound = new AssetLocation(taskConfig["drawingsound"].AsString());
            }
            if (taskConfig["shootingsound"].Exists)
            {
                shootingSound = new AssetLocation(taskConfig["shootingsound"].AsString());
            }
            if (taskConfig["animationRelase"].Exists)
            {
                animationRelease = new AnimationMetaData()
                {
                    Animation = taskConfig["animationRelase"].AsString(),
                    Code = taskConfig["animationRelase"].AsString()
                }.Init();
            }
            damage = taskConfig["damage"].AsFloat(3f);

        }


        public override bool ShouldExecute()
        {
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;
            if (lastSearchTotalMs + searchWaitMs < entity.World.ElapsedMilliseconds && targetEntity?.Alive != true
                || lastSearchTotalMs + searchWaitMs * 5 < entity.World.ElapsedMilliseconds)
            {
                float range = maxDist;
                lastSearchTotalMs = entity.World.ElapsedMilliseconds;

                targetEntity = partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, range, (e) => IsTargetableEntity(e, range * 4) && hasDirectContact(e, range * 4, range / 2f));
            }
            return targetEntity?.Alive == true;
        }

        public override void StartExecute()
        {
            if (entity is EntityVillager villager)
            {
                villager?.DrawWeapon();
                villager.RightHandItemSlot?.Itemstack?.Attributes?.SetInt("renderVariant", 1);
                villager.RightHandItemSlot.MarkDirty();
            }
            startTimeStamp = 0;
            didThrow = false;
            didRenderswitch = false;
            animStarted = false;

            if (entity?.Properties.Server?.Attributes != null)
            {
                ITreeAttribute pathfinder = entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder");
                if (pathfinder != null)
                {
                    minTurnAnglePerSec = pathfinder.GetFloat("minTurnAnglePerSec", 250);
                    maxTurnAnglePerSec = pathfinder.GetFloat("maxTurnAnglePerSec", 450);
                }
            }
            else
            {
                minTurnAnglePerSec = 250;
                maxTurnAnglePerSec = 450;
            }

            curTurnRadPerSec = minTurnAnglePerSec + (float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec);
            curTurnRadPerSec *= GameMath.DEG2RAD * 50 * 0.02f;
        }



        public override bool ContinueExecute(float dt)
        {
            Vec3f targetVec = targetEntity.ServerPos.XYZFloat.Sub(entity.ServerPos.XYZFloat);

            targetVec.Set(
                (float)(targetEntity.ServerPos.X - entity.ServerPos.X),
                (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y),
                (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z)
            );

            float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

            if (Math.Abs(yawDist) > 0.02) return true;

            if (animMeta != null && !animStarted)
            {
                animStarted = true;
                animMeta.EaseInSpeed = 1f;
                animMeta.EaseOutSpeed = 1f;
                entity.AnimManager.StartAnimation(animMeta);
                if (drawingsound != null)
                {
                    entity.World.PlaySoundAt(drawingsound, entity, null, false);
                }
            }

            startTimeStamp += dt;

            if (entity is EntityVillager && !didRenderswitch && startTimeStamp > releaseAtMs / 2000f)
            {
                entity.RightHandItemSlot?.Itemstack?.Attributes?.SetInt("renderVariant", 3);
                entity.RightHandItemSlot.MarkDirty();
                didRenderswitch = true;
            }

            if (startTimeStamp > releaseAtMs / 1000f && !didThrow && !entityInTheWay())
            {
                didThrow = true;

                EntityProjectile projectile = (EntityProjectile)entity.World.ClassRegistry.CreateEntity(projectileType);
                projectile.FiredBy = entity;
                projectile.Damage = damage;
                projectile.ProjectileStack = new ItemStack();
                projectile.DropOnImpactChance = 0;
                projectile.World = entity.World;

                Vec3d pos = entity.ServerPos.AheadCopy(0.5).XYZ.AddCopy(0, entity.LocalEyePos.Y, 0);
                Vec3d aheadPos = targetEntity.ServerPos.XYZ.AddCopy(0, targetEntity.LocalEyePos.Y, 0);

                double distf = Math.Pow(pos.SquareDistanceTo(aheadPos), 0.1);
                Vec3d velocity = (aheadPos - pos + new Vec3d(0, Math.Sqrt(distf / 4), 0)).Normalize() * GameMath.Clamp(distf - 1f, 0.1f, 1f);

                projectile.ServerPos.SetPos(
                    entity.ServerPos.AheadCopy(0.5).XYZ.Add(0, entity.LocalEyePos.Y, 0)
                );

                projectile.ServerPos.Motion.Set(velocity);
                projectile.Pos.SetFrom(projectile.ServerPos);
                projectile.SetRotation();


                entity.World.SpawnEntity(projectile);

                if (shootingSound != null)
                {
                    entity.World.PlaySoundAt(shootingSound, entity, null, false);
                }
                if (animationRelease != null)
                {
                    animationRelease.EaseInSpeed = 1f;
                    animationRelease.EaseOutSpeed = 1f;
                    entity.AnimManager.StartAnimation(animationRelease);
                }
            }

            return startTimeStamp < durationMs / 1000f;
        }

        private bool entityInTheWay()
        {
            var entitySel = new EntitySelection();
            var blockSel = new BlockSelection();
            entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos), targetEntity.ServerPos.XYZ.AddCopy(targetEntity.LocalEyePos), ref blockSel, ref entitySel);
            return entitySel?.Entity != targetEntity;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            if (entity is EntityVillager)
            {
                entity.RightHandItemSlot?.Itemstack?.Attributes?.SetInt("renderVariant", 0);
                entity.RightHandItemSlot.MarkDirty();
            }
        }



        public override bool IsTargetableEntity(Entity e, float range, bool ignoreEntityCode = false)
        {
            if (e == attackedByEntity && e?.Alive == true) { return true; }
            return base.IsTargetableEntity(e, range, ignoreEntityCode);
        }

        public void OnAllyAttacked(Entity byEntity)
        {
            if (targetEntity == null || !targetEntity.Alive)
            {
                targetEntity = byEntity;
            }
        }
    }
}