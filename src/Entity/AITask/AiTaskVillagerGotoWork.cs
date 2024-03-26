using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerGotoWork : AiTaskGotoAndInteract
    {

        float offset;
        DayTimeFrame[] duringDayTimeFrames;

        public AiTaskVillagerGotoWork(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
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
        }

        protected override void ApplyInteractionEffect()
        {
            // do nothing
        }

        protected override Vec3d GetTargetPos()
        {
            var blockAccessor = entity.World.BlockAccessor;
            var villager = entity as EntityVillager;
            var village = villager?.Village;
            var workstation = villager?.Workstation != null ? blockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(villager.Workstation) : null;
            if (workstation == null && villager != null)
            {
                var workPos = village?.FindFreeWorkstation(entity.EntityId, villager.Profession);
                if (workPos != null)
                {
                    villager.Workstation = workPos;
                }
                workstation = villager?.Workstation != null ? blockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(villager.Workstation) : null;
            }
            return getRandomPosNearby(workstation?.Position);
        }


        public override bool ShouldExecute()
        {
            return base.ShouldExecute() && IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World);
        }
        private Vec3d getRandomPosNearby(Vec3d middle)
        {
            if (middle == null)
            {
                return null;
            }
            int tries = 0;
            var blockAccessor = entity.World.BlockAccessor;

            while (tries < 5)
            {
                tries++;
                int offsetY = entity.World.Rand.Next(-1, 2);
                int offsetX = entity.World.Rand.Next(-1, 2);

                var candidate = middle.AddCopy(offsetX, 0, offsetY);

                if (blockAccessor.GetBlock(candidate.AsBlockPos.Up()).Id == 0)
                {
                    return candidate;
                }
            }
            return middle;
        }
    }
}
