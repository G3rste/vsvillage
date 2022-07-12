using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsQuest;

namespace VsVillage
{
    public class EnoughStrawdummiesObjective : ActionObjective
    {
        private int newSoldiers;

        public EnoughStrawdummiesObjective(int newSoldiers = 1)
        {
            this.newSoldiers = newSoldiers;
        }
        public bool isCompletable(IPlayer byPlayer)
        {
            return progress(byPlayer)[0] >= newSoldiers;
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var pos = byPlayer.Entity.Pos.XYZInt;
            int dummyCount = byPlayer.Entity.World.GetEntitiesAround(new Vec3d(pos.X, pos.Y, pos.Z), 100, 15, entity => entity.Code.Path == "strawdummy").Length;
            int villagerCount = byPlayer.Entity.World.GetEntitiesAround(new Vec3d(pos.X, pos.Y, pos.Z), 100, 15, entity => entity.Code.Path.EndsWith("-soldier")).Length;
            int openSlots = (dummyCount * 3 - villagerCount);
            return new List<int>(new int[] { openSlots });
        }
    }
}