using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsQuest;

namespace VsVillage
{
    public class EnoughTroughsObjective : ActionObjective
    {
        private int newShepherds;

        public EnoughTroughsObjective(int newShepherds = 1)
        {
            this.newShepherds = newShepherds;
        }
        public bool isCompletable(IPlayer byPlayer)
        {
            return progress(byPlayer)[0] >= newShepherds;
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var pos = byPlayer.Entity.Pos.XYZInt;
            int troughCount = ActionObjectiveUtil.countBlockEntities(pos, byPlayer.Entity.World.BlockAccessor, blockEntity => blockEntity is BlockEntityTrough);
            int villagerCount = byPlayer.Entity.World.GetEntitiesAround(new Vec3d(pos.X, pos.Y, pos.Z), 100, 15, entity => entity.Code.Path.EndsWith("-shepherd")).Length;
            return new List<int>(new int[] { troughCount - villagerCount });
        }

        
    }
}