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
        BlockEntityVillagerBrazier brazier;

        public AiTaskVillagerGotoGatherspot(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig )
        {
            // create random offset from -50 to 50 / 100 to add some variation to when AI tasks can be run
            offset = ((float)entity.World.Rand.Next(taskConfig["minoffset"].AsInt(-50), taskConfig["maxoffset"].AsInt(50))) / 100;
        }

        protected override void ApplyInteractionEffect()
        {
            brazier?.Ignite();
            brazier = null;
        }

        protected override Vec3d GetTargetPos()
        {
            var api = entity.Api;
            var village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
            var brazierPos = village?.FindRandomGatherplace();
            brazier = brazierPos != null ? api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBrazier>(brazierPos) : null;
            return getRandomPosNearby(brazier?.Position);
        }

        public override bool ShouldExecute()
        {
            return base.ShouldExecute() && IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World, offset);
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
