using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
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
            var villager = entity.GetBehavior<EntityBehaviorVillager>();
            var village = villager?.Village;
            if (village == null) return null;
            var workPos = villager.Workstation;
            if (workPos == null)
            {
                workPos = village.FindFreeWorkstation(entity.EntityId, villager.Profession);
                villager.Workstation = workPos;
            }
            else
            {
                village.Workstations.TryGetValue(workPos, out var workstation);
                if (workstation == null || workstation.OwnerId != entity.EntityId)
                {
                    workPos = null; 
                    villager.Workstation = null;
                }
            }
            return getRandomPosNearby(workPos?.ToVec3d());
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
