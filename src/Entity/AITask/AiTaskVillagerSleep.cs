using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerSleep : AiTaskBase
    {

        BlockEntityBed bed = null;

        bool done;
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

            villagerPathTraverser = entity.GetBehavior<EntityBehaviorAlternatePathtraverser>().villagerWaypointsTraverser;
            done = false;
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
            if (bed == null) { retrieveBed(); }

            if (bed != null)
            {
                done = !villagerPathTraverser.NavigateTo(bed.Pos.ToVec3d(), moveSpeed, 0.5f, goToBed, goToBed, true, 10000);
            }
            else
            {
                done = true;
            }
            if (done) { goToBed(); }
            else { base.StartExecute(); }
        }

        public override bool ContinueExecute(float dt)
        {
            if (lastCheck + 500 < entity.World.ElapsedMilliseconds && bed != null && entity.MountedOn == null)
            {
                lastCheck = entity.World.ElapsedMilliseconds;
                if (entity.ServerPos.SquareDistanceTo(bed.Pos.ToVec3d()) < 2) { goToBed(); }
            }
            return IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World);
        }

        public override void FinishExecute(bool cancelled)
        {
            villagerPathTraverser.Stop();
            base.FinishExecute(cancelled);
            entity.AnimManager.StopAnimation(sleepAnimMeta.Code);
            entity.TryUnmount();
        }

        private void goToBed()
        {
            done = true;
            villagerPathTraverser.Stop();
            if (bed != null && bed.MountedBy == null)
            {
                entity.TryMount(bed);
                if (bed.MountPosition != null && entity.ServerPos.SquareDistanceTo(bed.Pos.ToVec3d()) < 3)
                {
                    entity.ServerPos.Yaw = bed.MountPosition.Yaw;
                }
            }
            entity.AnimManager.StopAnimation(animMeta.Code);
            entity.AnimManager.StartAnimation(sleepAnimMeta);
        }

        private void retrieveBed()
        {
            var blockAccessor = entity.World.BlockAccessor;
            var villager = entity as EntityVillager;
            var village = villager?.Village;
            bed = villager?.Bed != null ? blockAccessor.GetBlockEntity<BlockEntityBed>(villager.Bed) : null;
            if (bed == null && villager != null)
            {
                villager.Bed = village?.FindFreeBed(entity.EntityId);
                bed = villager?.Bed != null ? blockAccessor.GetBlockEntity<BlockEntityBed>(villager.Bed) : null;
            }
        }
    }
}
