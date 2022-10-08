using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerSocialize : AiTaskGotoAndInteract
    {

        public Entity other { get; set; }

        public AiTaskGotoEntity gotoTask;
        public AiTaskLookAtEntity lookAtTask;

        public bool lookAtTaskStarted;

        public AiTaskVillagerSocialize(EntityAgent entity) : base(entity)
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
                var message = new TalkUtilMessage();
                message.entityId = entity.EntityId;
                message.talkType = EnumTalkType.Meet;

                IServerPlayer[] relevantPlayers = new List<Entity>(entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 30, 10, player => player is EntityPlayer))
                    .ConvertAll<IServerPlayer>(player => (player as EntityPlayer).Player as IServerPlayer).ToArray();

                if (relevantPlayers.Length > 0)
                {
                    (entity.Api as ICoreServerAPI).Network.GetChannel("vsvillagenetwork").SendPacket<TalkUtilMessage>(message, relevantPlayers);
                }
            }
            return closeEnough;
        }

        protected override void ApplyInteractionEffect()
        {
            other = null;
        }
    }
}