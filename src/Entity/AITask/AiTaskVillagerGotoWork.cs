using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerGotoWork : AiTaskBase
    {

        IVillagerPointOfInterest workstation = null;
        float moveSpeed = 0.03f;
        long lastCheck;

        float offset;
        DayTimeFrame[] duringDayTimeFrames;
        VillagerWaypointsTraverser villagerPathTraverser;

        public AiTaskVillagerGotoWork(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }
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
        }

        public override bool ShouldExecute()
        {
            if (lastCheck + 10000 < entity.World.ElapsedMilliseconds)
            {
                lastCheck = entity.World.ElapsedMilliseconds;
                if (workstation == null)
                {
                    retrieveWorkstation();
                    return false;
                }
                return entity.ServerPos.SquareDistanceTo(workstation.Position) > 8 * 8 && IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World);
            }
            else
            {
                return false;
            }
        }


        public override void StartExecute()
        {
            if (workstation == null) { retrieveWorkstation(); }

            if (workstation != null)
            {
                villagerPathTraverser.NavigateTo(workstation.Position, moveSpeed, 0.5f, () => { }, () => { }, true, 10000);
            }
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            return entity.ServerPos.SquareDistanceTo(workstation.Position) > 5 * 5;
        }

        public override void FinishExecute(bool cancelled)
        {
            villagerPathTraverser.Stop();
            base.FinishExecute(cancelled);
        }

        private void retrieveWorkstation()
        {
            var registry = (entity.Api as ICoreServerAPI)?.ModLoader.GetModSystem<POIRegistry>();
            if (entity.Attributes.HasAttribute("villagerWork"))
            {
                workstation = registry.GetNearestPoi(entity.Attributes.GetBlockPos("villagerWork").ToVec3d(), 2f, isValidWorkStation) as IVillagerPointOfInterest;
                if (workstation != null) { return; }
            }
            else
            {
                workstation = registry.GetNearestPoi(entity.ServerPos.XYZ, 50f, isValidWorkStation) as IVillagerPointOfInterest;
                if (workstation != null)
                {
                    entity.Attributes.SetBlockPos("villagerWork", new BlockPos(workstation.Position.XInt, workstation.Position.YInt, workstation.Position.ZInt));
                    return;
                }
            }
            entity.Attributes.RemoveAttribute("villagerWork");
        }

        private bool isValidWorkStation(IPointOfInterest poi)
        {
            var workstation = poi as IVillagerPointOfInterest;
            if (workstation != null)
            {
                if (workstation.workerIds.Contains(entity.EntityId))
                {
                    entity.Attributes.SetBlockPos("villagerWork", new BlockPos(workstation.Position.XInt, workstation.Position.YInt, workstation.Position.ZInt));
                    return true;
                }

                return workstation.tryAddWorker(entity as EntityVillager);
            }
            return false;
        }
    }
}
