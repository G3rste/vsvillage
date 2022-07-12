using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsQuest;

namespace VsVillage
{
    public class EnoughQuernsObjective : ActionObjective
    {
        private int newFarmers;

        public EnoughQuernsObjective(int newFarmers = 1)
        {
            this.newFarmers = newFarmers;
        }
        public bool isCompletable(IPlayer byPlayer)
        {
            return progress(byPlayer)[0] >= newFarmers;
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var pos = byPlayer.Entity.Pos.XYZInt;
            int quernCount = ActionObjectiveUtil.countBlockEntities(pos, byPlayer.Entity.World.BlockAccessor, blockEntity => blockEntity is BlockEntityQuern);
            int villagerCount = byPlayer.Entity.World.GetEntitiesAround(new Vec3d(pos.X, pos.Y, pos.Z), 100, 15, entity => entity.Code.Path.EndsWith("-farmer")).Length;
            int openSlots = (quernCount * 2 - villagerCount);
            return new List<int>(new int[] { openSlots });
        }
    }
}