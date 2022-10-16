using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerGotoGatherspot : AiTaskGotoAndInteract
    {

        float offset;
        DayTimeFrame[] duringDayTimeFrames;

        public AiTaskVillagerGotoGatherspot(EntityAgent entity) : base(entity)
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
            var registry = (entity.Api as ICoreServerAPI)?.ModLoader.GetModSystem<POIRegistry>();
            return getRandomPosNearby(registry.GetNearestPoi(entity.ServerPos.XYZ, maxDistance, poi => poi is BlockEntityVillagerBrazier)?.Position);
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
                int offsetY = entity.World.Rand.Next(-3, 4);
                int offsetX = entity.World.Rand.Next(-3, 4);

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
