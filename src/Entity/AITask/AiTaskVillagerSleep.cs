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
                if(bed == null || bed.GetBehavior<BlockEntityBehaviorVillagerBed>()?.ownerId != entity.EntityId){
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
            if (bed == null || bed.GetBehavior<BlockEntityBehaviorVillagerBed>()?.owner != entity) { retrieveBed(); }

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
                    entity.TeleportTo(bed.Pos);
                    entity.ServerPos.Yaw = bed.MountPosition.Yaw;
                }
            }
            entity.AnimManager.StopAnimation(animMeta.Code);
            entity.AnimManager.StartAnimation(sleepAnimMeta);
        }

        private void retrieveBed()
        {
            if (entity.Attributes.HasAttribute("villagerBed"))
            {
                bed = entity.World.BlockAccessor.GetBlockEntity(entity.Attributes.GetBlockPos("villagerBed")) as BlockEntityBed;
                if (bed != null && bed.GetBehavior<BlockEntityBehaviorVillagerBed>()?.owner?.EntityId == entity.EntityId) { return; }
            }
            var villagerBed = (entity.Api as ICoreServerAPI)?.ModLoader.GetModSystem<POIRegistry>().GetNearestPoi(entity.ServerPos.XYZ, 75, poi =>
            {
                var behaviorBed = poi as BlockEntityBehaviorVillagerBed;

                // sometimes the worldgen overwrites some bed blocks without deleting the bed entity properly
                // we shall avoid those bed entities
                bool isNotRiggedByWorldGen = entity.World.BlockAccessor.GetBlock(poi.Position.AsBlockPos).Code.Path.StartsWith("bed-");

                return behaviorBed != null && isNotRiggedByWorldGen && (behaviorBed.owner == null || !behaviorBed.owner.Alive || behaviorBed.owner == entity);
            }) as BlockEntityBehaviorVillagerBed;
            if (villagerBed?.setOwnerIfFree(entity.EntityId) == true)
            {
                entity.Attributes.SetBlockPos("villagerBed", villagerBed.Position.AsBlockPos);
                bed = villagerBed.Blockentity as BlockEntityBed;
            }
        }
    }
}
