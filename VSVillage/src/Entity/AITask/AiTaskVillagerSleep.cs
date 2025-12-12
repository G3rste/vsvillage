using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerSleep : AiTaskBase
    {

        BlockEntityVillagerBed bedEntity = null;

        bool bedReached;
        float moveSpeed = 0.03f;
        long lastCheck;

        float offset;
        AnimationMetaData sleepAnimMeta;

        public AiTaskVillagerSleep(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }
            sleepAnimMeta = new AnimationMetaData()
            {
                Code = taskConfig["sleepAnimation"].AsString("Sleep").ToLowerInvariant(),
                Animation = taskConfig["sleepAnimation"].AsString("Sleep").ToLowerInvariant(),
                AnimationSpeed = taskConfig["sleepAnimationSpeed"].AsFloat(1f)
            }.Init();
            offset = ((float)entity.World.Rand.Next(taskConfig["minoffset"].AsInt(-50), taskConfig["maxoffset"].AsInt(50))) / 100;

            bedReached = false;
        }

        public override bool ShouldExecute()
        {
            if (lastCheck + 10000 < entity.World.ElapsedMilliseconds)
            {
                lastCheck = entity.World.ElapsedMilliseconds;
                retrieveBed();
                return bedEntity != null && IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World, offset);
            }
            else
            {
                return false;
            }
        }


        public override void StartExecute()
        {
            base.StartExecute();
            bedReached = false;
            if (bedEntity != null)
            {
                pathTraverser.NavigateTo(bedEntity.Pos.ToVec3d(), moveSpeed, 0.5f, tryGoingToBed, tryGoingToBed, null, true, 999, 0, null);
                tryGoingToBed();
            }
        }
        public override bool ContinueExecute(float dt)
        {
            if (lastCheck + 500 < entity.World.ElapsedMilliseconds && !bedReached)
            {
                lastCheck = entity.World.ElapsedMilliseconds;
                tryGoingToBed();
            }
            return IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World) && (bedReached || pathTraverser.Active);
        }

        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            base.FinishExecute(cancelled);
            entity.AnimManager.StopAnimation(sleepAnimMeta.Code);
        }

        private void tryGoingToBed()
        {
            if (bedEntity != null && entity.ServerPos.SquareDistanceTo(bedEntity.Pos.ToVec3d()) < 3)
            {
                entity.ServerPos.SetPos(bedEntity.Pos.ToVec3d().Add(0.5, 0, 0.5));
                entity.ServerPos.Yaw = bedEntity.Yaw;
                entity.AnimManager.StopAnimation(animMeta.Code);
                entity.AnimManager.StartAnimation(sleepAnimMeta);
                bedReached = true;
                pathTraverser.Stop();
            }
        }

        private void retrieveBed()
        {
            bedEntity = null;
            var blockAccessor = entity.World.BlockAccessor;
            var villager = entity.GetBehavior<EntityBehaviorVillager>();
            var village = villager?.Village;
            if (village == null) return;
            var bedPos = villager.Bed;
            if (bedPos == null)
            {
                bedPos = village.FindFreeBed(entity.EntityId);
                villager.Bed = bedPos;
            }
            else
            {
                village.Beds.TryGetValue(bedPos, out var bed);
                if (bed == null || bed.OwnerId != entity.EntityId)
                {
                    bedPos = null;
                    villager.Bed = null;
                }
            }

            if (bedPos != null)
            {
                bedEntity = blockAccessor.GetBlockEntity<BlockEntityVillagerBed>(bedPos);
            }
        }
    }
}
