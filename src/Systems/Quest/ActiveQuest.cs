using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsVillage
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuest
    {
        public long questGiverId { get; set; }
        public string questId { get; set; }
        public List<EntityKillTracker> killTrackers { get; set; }
        public void OnEntityKilled(string entityCode)
        {
            foreach (var tracker in killTrackers)
            {
                if (tracker.relevantEntityCodes.Contains(entityCode))
                {
                    tracker.kills++;
                }
            }
        }

        public bool isCompletable(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.questRegistry[questId];
            bool completable = true;
            for (int i = 0; i < quest.killObjectives.Count; i++)
            {
                completable &= quest.killObjectives[i].demand <= killTrackers[i].kills;
            }
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                int itemsFound = itemsGathered(byPlayer, gatherObjective);
                completable &= itemsFound >= gatherObjective.demand;
            }
            return completable;
        }

        public void completeQuest(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.questRegistry[questId];
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                handOverItems(byPlayer, gatherObjective);
            }
        }

        public List<int> killProgress()
        {
            if (killTrackers != null)
            {
                return killTrackers.ConvertAll<int>(tracker => tracker.kills);
            }
            else
            {
                return new List<int>();
            }
        }

        public List<int> gatherProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.questRegistry[questId];
            return quest.gatherObjectives.ConvertAll<int>(gatherObjective => itemsGathered(byPlayer, gatherObjective));
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var progress = gatherProgress(byPlayer);
            progress.AddRange(killProgress());
            return progress;
        }

        public int itemsGathered(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjective.validCodes.Contains(slot?.Itemstack?.Collectible?.Code?.Path))
                    {
                        itemsFound += slot.Itemstack.StackSize;
                    }
                }
            }
            return itemsFound;
        }

        public void handOverItems(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjective.validCodes.Contains(slot?.Itemstack?.Collectible?.Code?.Path))
                    {
                        var stack = slot.TakeOut(Math.Min(slot.Itemstack.StackSize, gatherObjective.demand - itemsFound));
                        slot.MarkDirty();
                        itemsFound += stack.StackSize;
                    }
                    if (itemsFound > gatherObjective.demand) { return; }
                }
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class EntityKillTracker
    {
        public HashSet<string> relevantEntityCodes { get; set; }
        public int kills { get; set; }
    }
}