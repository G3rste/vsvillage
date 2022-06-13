using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsVillage
{
    public class QuestSystem : ModSystem
    {
        public Dictionary<string, Quest> questRegistry { get; private set; } = new Dictionary<string, Quest>();
        private ConcurrentDictionary<string, List<ActiveQuest>> playerQuests = new ConcurrentDictionary<string, List<ActiveQuest>>();
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));

            List<Quest> quests = api.Assets.TryGet(new AssetLocation("vsvillage", "config/quests.json"))?.ToObject<List<Quest>>();
            if (quests == null) throw new FileNotFoundException("config/quests.json is missing.");

            foreach (var quest in quests)
            {
                questRegistry.Add(quest.id, quest);
            }

        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);

            capi.Network.RegisterChannel("vsquest")
                .RegisterMessageType<QuestAcceptedMessage>()
                .RegisterMessageType<QuestCompletedMessage>()
                .RegisterMessageType<QuestInfoMessage>().SetMessageHandler<QuestInfoMessage>(message => OnQuestInfoMessage(message, capi));
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            sapi.Network.RegisterChannel("vsquest")
                .RegisterMessageType<QuestAcceptedMessage>().SetMessageHandler<QuestAcceptedMessage>((player, message) => OnQuestAccepted(player, message, sapi))
                .RegisterMessageType<QuestCompletedMessage>().SetMessageHandler<QuestCompletedMessage>((player, message) => OnQuestCompleted(player, message, sapi))
                .RegisterMessageType<QuestInfoMessage>();

            sapi.Event.GameWorldSave += () => OnSave(sapi);
            sapi.Event.PlayerDisconnect += player => OnDisconnect(player, sapi);
            sapi.Event.OnEntityDeath += (entity, dmgSource) => OnEntityDeath(entity, dmgSource, sapi);
        }

        public List<ActiveQuest> getPlayerQuests(string playerUID, ICoreServerAPI sapi)
        {
            return playerQuests.GetOrAdd(playerUID, (val) => loadPlayerQuests(sapi, val));
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource, ICoreServerAPI sapi)
        {
            if (damageSource?.SourceEntity is EntityPlayer player)
            {
                getPlayerQuests(player.PlayerUID, sapi).ForEach(quest => quest.OnEntityKilled(entity.Code.Path));
            }
        }

        private void OnDisconnect(IServerPlayer byPlayer, ICoreServerAPI sapi)
        {
            if (playerQuests.TryGetValue(byPlayer.PlayerUID, out var activeQuests))
            {
                savePlayerQuests(sapi, byPlayer.PlayerUID, activeQuests);
                playerQuests.Remove(byPlayer.PlayerUID);
            }
        }

        private void OnSave(ICoreServerAPI sapi)
        {
            foreach (var player in playerQuests)
            {
                savePlayerQuests(sapi, player.Key, player.Value);
            }
        }

        private void savePlayerQuests(ICoreServerAPI sapi, string playerUID, List<ActiveQuest> activeQuests)
        {
            sapi.WorldManager.SaveGame.StoreData<List<ActiveQuest>>(String.Format("quests-{0}", playerUID), activeQuests);
        }
        private List<ActiveQuest> loadPlayerQuests(ICoreServerAPI sapi, string playerUID)
        {
            return sapi.WorldManager.SaveGame.GetData<List<ActiveQuest>>(String.Format("quests-{0}", playerUID), new List<ActiveQuest>());
        }

        private void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            var quest = questRegistry[message.questId];
            var killTrackers = new List<EntityKillTracker>();
            foreach (var killObjective in quest.killObjectives)
            {
                var tracker = new EntityKillTracker()
                {
                    kills = 0,
                    relevantEntityCodes = new HashSet<string>(killObjective.validCodes)
                };
                killTrackers.Add(tracker);
            }
            var activeQuest = new ActiveQuest()
            {
                questGiverId = message.questGiverId,
                questId = message.questId,
                killTrackers = killTrackers
            };
            getPlayerQuests(fromPlayer.PlayerUID, sapi).Add(activeQuest);
            var questgiver = sapi.World.GetEntityById(message.questGiverId);
            var key = quest.perPlayer ? String.Format("lastaccepted-{0}-{1}", quest.id, questgiver.EntityId) : String.Format("lastaccepted-{0}", quest.id);
            questgiver.WatchedAttributes.SetDouble(key, sapi.World.Calendar.TotalDays);
            questgiver.WatchedAttributes.MarkPathDirty(key);
        }

        private void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID, sapi);
            var activeQuest = playerQuests.Find(item => item.questId == message.questId && item.questGiverId == message.questGiverId);
            activeQuest.completeQuest(fromPlayer);
            playerQuests.Remove(activeQuest);
            var quest = questRegistry[message.questId];
            foreach (var reward in quest.itemRewards)
            {
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(reward.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(reward.itemCode));
                }
                var stack = new ItemStack(item, reward.amount);
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    var questgiver = sapi.World.GetEntityById(message.questGiverId);
                    sapi.World.SpawnItemEntity(stack, questgiver.ServerPos.XYZ);
                }
            }
        }

        private void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            new QuestSelectGui(capi, message.questGiverId, message.availableQestIds, message.activeQuests).TryOpen();
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestAcceptedMessage
    {
        public string questId { get; set; }

        public long questGiverId { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestCompletedMessage
    {
        public string questId { get; set; }

        public long questGiverId { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestInfoMessage
    {
        public long questGiverId { get; set; }
        public List<string> availableQestIds { get; set; }
        public List<ActiveQuest> activeQuests { get; set; }
    }
}