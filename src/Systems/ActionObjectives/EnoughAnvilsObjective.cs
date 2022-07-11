using System.Collections.Generic;
using Vintagestory.API.Common;
using VsQuest;

namespace VsVillage
{
    public class EnoughAnvils : ActionObjective
    {
        private int anvilDemand;

        public EnoughAnvils(int anvilDemand = 1)
        {
            this.anvilDemand = anvilDemand;
        }
        public bool isCompletable(IPlayer byPlayer)
        {
            return progress(byPlayer)[0] >= anvilDemand;
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var pos = byPlayer.Entity.Pos;
            int anvilCount = 0;
            byPlayer.Entity.World.BlockAccessor.WalkBlocks(pos.AsBlockPos.AddCopy(-100, -15, -100), pos.AsBlockPos.AddCopy(100, 15, 100), (block, x, y, z) =>
            {
                string code = block.Code.Path;
                if (code.StartsWith("anvil-"))
                {
                    anvilCount++;
                }
            });
            int villagerCount = byPlayer.Entity.World.GetEntitiesAround(pos.XYZ, 100, 15, entity => entity.Code.Path.EndsWith("-smith")).Length;
            return new List<int>(new int[] { anvilCount - villagerCount });
        }
    }
}