using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerSocialize : AiTaskBase
    {

        public Entity other { get; set; }

        public AiTaskGotoEntity gotoTask;
        public AiTaskLookAtEntity lookAtTask;

        long lastCheck;

        float moveSpeed;

        bool lookAtTaskStarted;

        public AiTaskVillagerSocialize(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);

        }
        public override bool ShouldExecute()
        {
            if (cooldownUntilMs + lastCheck > entity.World.ElapsedMilliseconds && other == null) { return false; }
            lastCheck = entity.World.ElapsedMilliseconds;
            if (other == null)
            {
                var friends = entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 8, 2, friend => friend is EntityVillager || friend is EntityPlayer);
                if (friends.Length > 0) { other = friends[0]; }
            }
            return other != null;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            gotoTask = new AiTaskGotoEntity(entity, other);
            lookAtTask = new AiTaskLookAtEntity(entity, other);
            lookAtTaskStarted = false;

            gotoTask.StartExecute();
            var socialtask = other.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.GetTask<AiTaskVillagerSocialize>();
            if (socialtask != null) { socialtask.other = entity; }
        }

        public override bool ContinueExecute(float dt)
        {
            if (gotoTask.TargetReached())
            {
                if (lookAtTaskStarted)
                {
                    return lookAtTask.ContinueExecute(dt);
                }
                else
                {
                    lookAtTask.StartExecute();
                    lookAtTaskStarted = true;
                }
            }
            else
            {
                gotoTask.ContinueExecute(dt);
            }
            return true;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            gotoTask.FinishExecute(cancelled);
            lookAtTask.FinishExecute(cancelled);
            entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = "welcome", Code = "welcome", Weight = 10, EaseOutSpeed = 10000, EaseInSpeed = 10000 });

            var message = new TalkUtilMessage();
            message.entityId = entity.EntityId;
            message.talkType = EnumTalkType.Meet;

            IServerPlayer[] relevantPlayers = new List<Entity>(entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 30, 10, player => player is EntityPlayer))
                .ConvertAll<IServerPlayer>(player => (player as EntityPlayer).Player as IServerPlayer).ToArray();

            if (relevantPlayers.Length > 0)
            {
                (entity.Api as ICoreServerAPI).Network.GetChannel("vsvillagenetwork").SendPacket<TalkUtilMessage>(message, relevantPlayers);
            }

            other = null;
        }
    }
}