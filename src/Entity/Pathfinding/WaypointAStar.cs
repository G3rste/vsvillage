using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsVillage
{
    public class WaypointAStar : VillagerAStarNew
    {
        public WaypointAStar(ICachingBlockAccessor blockAccessor) : base(blockAccessor)
        {
            traversableCodes = ["door", "gate", "multiblock"];
            climbableCodes = [];
            steppableCodes = ["stair", "path", "packed", "plank"];
        }

        protected override bool canStep(Block belowBlock)
        {
            return steppableCodes.Exists(code => belowBlock.Code.Path.Contains(code));
        }
    }
}