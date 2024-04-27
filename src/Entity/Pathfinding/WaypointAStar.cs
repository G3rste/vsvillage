using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsVillage
{
    public class WaypointAStar : VillagerAStar
    {
        public WaypointAStar(ICoreServerAPI api) : base(api)
        {
            traversableCodes = new List<string>() { "door", "gate", "multiblock" };
            climbableCodes = new List<string>();
            steppableCodes = new List<string>() { "stair", "path", "packed", "plank" };
        }

        protected override bool canStep(Block belowBlock)
        {
            return steppableCodes.Exists(code => belowBlock.Code.Path.Contains(code));
        }
    }
}