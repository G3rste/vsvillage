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

        float fromTime;
        float toTime;

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
            fromTime = taskConfig["fromTime"].AsFloat(22);
            toTime = taskConfig["toTime"].AsFloat(6);

            villagerPathTraverser = entity.GetBehavior<EntityBehaviorAlternatePathtraverser>().villagerWaypointsTraverser;
            done = false;
        }

        public override bool ShouldExecute()
        {
            if (lastCheck + 10000 < entity.World.ElapsedMilliseconds)
            {
                lastCheck = entity.World.ElapsedMilliseconds;
                return entity.World.Calendar.HourOfDay > fromTime + offset || entity.World.Calendar.HourOfDay < toTime + offset;
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
            return entity.World.Calendar.HourOfDay > fromTime + offset || entity.World.Calendar.HourOfDay < toTime + offset;
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
            if (bed != null)
            {
                entity.TryMount(bed);
                if (bed.MountYaw != null && entity.ServerPos.SquareDistanceTo(bed.Pos.ToVec3d()) < 3)
                {
                    entity.ServerPos.Yaw = (float)bed.MountYaw;
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
            }
            var villagerBed = (entity.Api as ICoreServerAPI)?.ModLoader.GetModSystem<POIRegistry>().GetNearestPoi(entity.ServerPos.XYZ, 50, poi =>
            {
                var behaviorBed = poi as BlockEntityBehaviorVillagerBed;
                return behaviorBed != null && (behaviorBed.owner == null || !behaviorBed.owner.Alive || behaviorBed.owner == entity);
            }) as BlockEntityBehaviorVillagerBed;
            bed = villagerBed?.Blockentity as BlockEntityBed;
            if (bed != null)
            {
                villagerBed.owner = entity as EntityVillager;
                entity.Attributes.SetBlockPos("villagerBed", bed.Pos);
            }
        }
    }
}
