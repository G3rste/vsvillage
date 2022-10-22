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

        BlockEntityVillagerWorkstation workstation = null;

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
            var registry = (entity.Api as ICoreServerAPI)?.ModLoader.GetModSystem<POIRegistry>();
            if (entity.Attributes.HasAttribute("workstation"))
            {
                workstation = entity.World.BlockAccessor.GetBlockEntity(entity.Attributes.GetBlockPos("workstation")) as BlockEntityVillagerWorkstation;
                if (workstation?.ownerId == entity.EntityId) { return getRandomPosNearby(workstation.Position); }
            }
            workstation = registry.GetNearestPoi(entity.ServerPos.XYZ, maxDistance, poi =>
            {
                var candidate = poi as BlockEntityVillagerWorkstation;
                return candidate != null && candidate.Type == entity.Properties.Attributes["profession"].AsString() && (candidate.ownerId == null || !candidate.owner.Alive || candidate.ownerId == entity.EntityId);
            }) as BlockEntityVillagerWorkstation;
            if (workstation?.setOwnerIfFree(entity.EntityId) == true)
            {
                entity.Attributes.SetBlockPos("workstation", workstation.Pos);
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
