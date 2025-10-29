using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using static Vintagestory.API.Common.EntityAgent;

namespace VsVillage
{
    public class AiTaskVillagerSocialize : AiTaskGotoAndInteract
    {

        public Entity other { get; set; }

        public AiTaskVillagerSocialize(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
        }

        protected override Vec3d GetTargetPos()
        {
            var friends = entity.World.GetEntitiesAround(entity.ServerPos.XYZ, maxDistance, 2, friend => friend is EntityVillager && friend != entity && friend.Alive || friend is EntityPlayer);
            if (friends.Length > 0)
            {
                other = friends[entity.World.Rand.Next(0, friends.Length)];
                return other.ServerPos.XYZ;
            }
            return null;
        }

        protected override bool InteractionPossible()
        {
            bool closeEnough = entity.ServerPos.SquareDistanceTo(other.ServerPos) < 2 * 2;
            if (closeEnough)
            {
                (entity.Api as ICoreServerAPI).Network.BroadcastEntityPacket(entity.EntityId, (int)EntityServerPacketId.Talk, SerializerUtil.Serialize((int)EnumTalkType.Meet));
            }
            return closeEnough;
        }

        protected override void ApplyInteractionEffect()
        {
            other = null;
        }
    }
}