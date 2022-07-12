using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsQuest;

namespace VsVillage
{
    public class EnoughAnvilsObjective : ActionObjective
    {
        private int anvilDemand;

        public EnoughAnvilsObjective(int anvilDemand = 1)
        {
            this.anvilDemand = anvilDemand;
        }
        public bool isCompletable(IPlayer byPlayer)
        {
            return progress(byPlayer)[0] >= anvilDemand;
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var pos = byPlayer.Entity.Pos.XYZInt;
            int anvilCount = ActionObjectiveUtil.countBlockEntities(pos, byPlayer.Entity.World.BlockAccessor, blockEntity => blockEntity is BlockEntityAnvil);
            int villagerCount = byPlayer.Entity.World.GetEntitiesAround(new Vec3d(pos.X, pos.Y, pos.Z), 100, 15, entity => entity.Code.Path.EndsWith("-smith")).Length;
            return new List<int>(new int[] { anvilCount - villagerCount });
        }

        
    }
}