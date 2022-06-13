using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsVillage
{
    public class EntityBehaviorQuestGiver : EntityBehavior
    {
        private string[] quests;

        public EntityBehaviorQuestGiver(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            quests = attributes["quests"].AsArray<string>();
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
            if (entity.Api is ICoreServerAPI sapi && byEntity is EntityPlayer player && mode == EnumInteractMode.Interact)
            {
                var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                var activeQuests = questSystem.getPlayerQuests(player.PlayerUID, sapi).FindAll(quest => quest.questGiverId == entity.EntityId);
                var availableQuestIds = new List<string>();
                foreach (var questId in quests)
                {
                    var quest = questSystem.questRegistry[questId];
                    var key = quest.perPlayer ? String.Format("lastaccepted-{0}-{1}", questId, entity.EntityId) : String.Format("lastaccepted-{0}", questId);
                    if (entity.WatchedAttributes.GetDouble(key) + quest.cooldown < sapi.World.Calendar.TotalDays
                            && activeQuests.Find(activeQuest => activeQuest.questId == questId) == null)
                    {
                        availableQuestIds.Add(questId);
                    }
                }
                var message = new QuestInfoMessage()
                {
                    questGiverId = entity.EntityId,
                    availableQestIds = availableQuestIds,
                    activeQuests = activeQuests
                };

                sapi.Network.GetChannel("vsquest").SendPacket<QuestInfoMessage>(message, player.Player as IServerPlayer);
            }
        }

        public override string PropertyName() => "questgiver";
    }
}