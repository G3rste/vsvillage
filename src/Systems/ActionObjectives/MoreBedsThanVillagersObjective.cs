using System.Collections.Generic;
using Vintagestory.API.Common;
using VsQuest;

namespace VsVillage
{
    public class MoreBedsThanVillagersObjective : ActionObjective
    {
        private int bedDemand;

        public MoreBedsThanVillagersObjective(int bedDemand = 1)
        {
            this.bedDemand = bedDemand;
        }
        public bool isCompletable(IPlayer byPlayer)
        {
            return progress(byPlayer)[0] >= bedDemand;
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var pos = byPlayer.Entity.Pos;
            int bedCount = 0;
            byPlayer.Entity.World.BlockAccessor.WalkBlocks(pos.AsBlockPos.AddCopy(-100, -15, -100), pos.AsBlockPos.AddCopy(100, 15, 100), (block, x, y, z) =>
            {
                string code = block.Code.Path;
                if (code.StartsWith("bed-") && code.Contains("-head-"))
                {
                    bedCount++;
                }
            });
            int villagerCount = byPlayer.Entity.World.GetEntitiesAround(pos.XYZ, 100, 15, entity => entity.Code.Path.StartsWith("humanoid-villager-")).Length;
            return new List<int>(new int[] { bedCount - villagerCount });
        }
    }
}