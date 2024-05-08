using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerSleep : AiTaskBase
    {

        BlockEntityVillagerBed bed = null;

        bool bedReached;
        float moveSpeed = 0.03f;
        long lastCheck;

        float offset;
        DayTimeFrame[] duringDayTimeFrames;
        VillagerWaypointsTraverser villagerPathTraverser;
        AnimationMetaData sleepAnimMeta;

        public AiTaskVillagerSleep(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

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
            duringDayTimeFrames = taskConfig["duringDayTimeFrames"].AsObject<DayTimeFrame[]>(null);
            if (duringDayTimeFrames != null)
            {
                foreach (var frame in duringDayTimeFrames)
                {
                    frame.FromHour += offset;
                    frame.ToHour += offset;
                }
            }

            villagerPathTraverser = entity.GetBehavior<EntityBehaviorVillager>().villagerWaypointsTraverser;
            bedReached = false;
        }

        public override bool ShouldExecute()
        {
            if (lastCheck + 10000 < entity.World.ElapsedMilliseconds)
            {
                lastCheck = entity.World.ElapsedMilliseconds;
                if (bed == null)
                {
                    retrieveBed();
                }
                return IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World);
            }
            else
            {
                return false;
            }
        }


        public override void StartExecute()
        {
            base.StartExecute();
            if (bed == null) { retrieveBed(); }
            bedReached = false;
            if (bed != null)
            {
                villagerPathTraverser.NavigateTo(bed.Pos.ToVec3d(), moveSpeed, 0.5f, tryGoingToBed, tryGoingToBed, true, 10000);
                tryGoingToBed();
            }
        }

        public override bool ContinueExecute(float dt)
        {
            if (lastCheck + 500 < entity.World.ElapsedMilliseconds && bed != null)
            {
                lastCheck = entity.World.ElapsedMilliseconds;
                if (entity.ServerPos.SquareDistanceTo(bed.Pos.ToVec3d()) < 2) { tryGoingToBed(); }
            }
            return IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World) && (bedReached || villagerPathTraverser.Active);
        }

        public override void FinishExecute(bool cancelled)
        {
            villagerPathTraverser.Stop();
            base.FinishExecute(cancelled);
            entity.AnimManager.StopAnimation(sleepAnimMeta.Code);
        }

        private void tryGoingToBed()
        {
            if (bed != null && entity.ServerPos.SquareDistanceTo(bed.Pos.ToVec3d()) < 3)
            {
                entity.ServerPos.SetPos(bed.Pos.ToVec3d().Add(0.5, 0, 0.5));
                entity.ServerPos.Yaw = bed.Yaw;
                entity.AnimManager.StopAnimation(animMeta.Code);
                entity.AnimManager.StartAnimation(sleepAnimMeta);
                bedReached = true;
                villagerPathTraverser.Stop();
            }
        }

        private void retrieveBed()
        {
            var blockAccessor = entity.World.BlockAccessor;
            var villager = entity.GetBehavior<EntityBehaviorVillager>();
            var village = villager?.Village;
            bed = villager?.Bed != null ? blockAccessor.GetBlockEntity<BlockEntityVillagerBed>(villager.Bed) : null;
            if (bed == null && villager != null)
            {
                var bedPos = village?.FindFreeBed(entity.EntityId);
                if (bedPos != null)
                {
                    villager.Bed = bedPos;
                }
                bed = villager?.Bed != null ? blockAccessor.GetBlockEntity<BlockEntityVillagerBed>(villager.Bed) : null;
            }
        }
    }
}
