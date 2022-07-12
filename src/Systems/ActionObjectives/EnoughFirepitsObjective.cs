using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsQuest;

namespace VsVillage
{
    public class EnoughFirepitsObjective : ActionObjective
    {
        private int newVillagers;

        private int demand => newVillagers / 6 + newVillagers % 6 > 0 ? 1 : 0;
        public EnoughFirepitsObjective(int newVillagers = 1)
        {
            this.newVillagers = newVillagers;
        }
        public bool isCompletable(IPlayer byPlayer)
        {
            return progress(byPlayer)[0] >= demand;
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var pos = byPlayer.Entity.Pos.XYZInt;
            int fireCount = ActionObjectiveUtil.countBlockEntities(pos, byPlayer.Entity.World.BlockAccessor, blockEntity => blockEntity is BlockEntityFirepit);
            int villagerCount = byPlayer.Entity.World.GetEntitiesAround(new Vec3d(pos.X, pos.Y, pos.Z), 100, 15, entity => entity.Code.Path.StartsWith("humanoid-villager-")).Length;
            int openSlots = (fireCount * 6 - villagerCount);
            return new List<int>(new int[] { openSlots >= newVillagers ? demand : openSlots / 6 });
        }
    }
}