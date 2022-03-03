using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerGotoPOI : AiTaskBase
    {

        IVillagerPointOfInterest poi = null;
        float moveSpeed = 0.03f;
        long lastCheck;

        bool stuck = false;

        float offset;
        DayTimeFrame[] duringDayTimeFrames;
        VillagerWaypointsTraverser villagerPathTraverser;

        float maxDistance;
        float minDistance;

        VillagerPointOfInterestOccasion occasion;

        string poiKey;

        public AiTaskVillagerGotoPOI(EntityAgent entity) : base(entity)
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
            maxDistance = taskConfig["maxdistance"].AsFloat(5);
            minDistance = taskConfig["mindistance"].AsFloat(2);
            Enum.TryParse<VillagerPointOfInterestOccasion>(taskConfig["occasion"].AsString().ToUpper(), out occasion);
            poiKey = "villager" + Enum.GetName(typeof(VillagerPointOfInterestOccasion), occasion);

            villagerPathTraverser = entity.GetBehavior<EntityBehaviorAlternatePathtraverser>().villagerWaypointsTraverser;
        }

        public override bool ShouldExecute()
        {
            if (lastCheck + 10000 < entity.World.ElapsedMilliseconds)
            {
                lastCheck = entity.World.ElapsedMilliseconds;
                if (poi == null)
                {
                    retrievePOI();
                    return false;
                }
                return entity.ServerPos.SquareDistanceTo(poi.Position) > maxDistance * maxDistance && IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World);
            }
            else
            {
                return false;
            }
        }


        public override void StartExecute()
        {
            if (poi == null) { retrievePOI(); }

            if (poi != null)
            {
                stuck = !villagerPathTraverser.NavigateTo(poi.Position, moveSpeed, 0.5f, () => { }, () => stuck = true, true, 10000);
            }
            else
            {
                stuck = true;
            }
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            return !stuck && entity.ServerPos.SquareDistanceTo(poi.Position) > minDistance * minDistance;
        }

        public override void FinishExecute(bool cancelled)
        {
            villagerPathTraverser.Stop();
            base.FinishExecute(cancelled);
        }

        private void retrievePOI()
        {
            var registry = (entity.Api as ICoreServerAPI)?.ModLoader.GetModSystem<POIRegistry>();
            if (entity.Attributes.HasAttribute(poiKey))
            {
                poi = registry.GetNearestPoi(entity.Attributes.GetBlockPos(poiKey).ToVec3d(), 2f, isValidPOI) as IVillagerPointOfInterest;
                if (poi != null) { return; }
            }
            poi = registry.GetNearestPoi(entity.ServerPos.XYZ, 75f, isValidPOI) as IVillagerPointOfInterest;
            if (poi != null)
            {
                entity.Attributes.SetBlockPos(poiKey, new BlockPos(poi.Position.XInt, poi.Position.YInt, poi.Position.ZInt));
                return;
            }
            entity.Attributes.RemoveAttribute(poiKey);
        }

        private bool isValidPOI(IPointOfInterest pointOfInterest)
        {
            var villagerPOI = pointOfInterest as IVillagerPointOfInterest;
            if (villagerPOI != null && villagerPOI.occasion == occasion)
            {
                if (villagerPOI.villagerIds.Contains(entity.EntityId))
                {
                    entity.Attributes.SetBlockPos(poiKey, new BlockPos(villagerPOI.Position.XInt, villagerPOI.Position.YInt, villagerPOI.Position.ZInt));
                    return true;
                }

                return villagerPOI.tryAddVillager(entity as EntityVillager);
            }
            return false;
        }
    }
}
