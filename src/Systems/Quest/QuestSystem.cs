using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsVillage
{
    public class QuestSystem : ModSystem
    {
        public Dictionary<string, Quest> questRegistry { get; private set; } = new Dictionary<string, Quest>();
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            List<Quest> quests;

            try
            {
                quests = api.LoadModConfig<List<Quest>>("Quests.json");
                if (quests != null)
                {
                    api.Logger.Notification("Quests successfully loaded.");
                }
                else
                {
                    api.Logger.Notification("No Quests specified. Using default quest list");
                    quests = getDefaultQuests();
                }

                api.StoreModConfig<List<Quest>>(quests, "Quests.json");
            }
            catch
            {
                quests = getDefaultQuests();
                api.Logger.Error("Failed to load custom quests. Falling back to default quest list!");
            }

            foreach (var quest in quests)
            {
                questRegistry.Add(quest.id, quest);
            }

        }

        private List<Quest> getDefaultQuests()
        {
            var kill15drifters = new Quest()
            {
                id = "vsvillage:quest-kill15drifters",
                gatherObjectives = new List<Objective>(),
                killObjectives = new List<Objective>(
                    new Objective[]
                    {
                        new Objective()
                        {
                            validCodes = new List<string>(new string[] { "drifter-normal", "drifter-deep", "drifter-tainted", "drifter-corrupt", "drifter-nightmare", "drifter-double-headed" }),
                            demand = 15
                        }
                    }),
                actionRewardIds = new List<string>(),
                itemRewards = new List<ItemReward>(new ItemReward[] { new ItemReward() { itemCode = "game:gear-rusty", amount = 3 } })
            };

            var kill4wolves = new Quest()
            {
                id = "vsvillage:quest-kill4wolves",
                gatherObjectives = new List<Objective>(),
                killObjectives = new List<Objective>(
                    new Objective[]
                    {
                        new Objective()
                        {
                            validCodes = new List<string>(new string[] { "wolf-male", "wolf-female", "wolf-pup" }),
                            demand = 4
                        }
                    }),
                actionRewardIds = new List<string>(),
                itemRewards = new List<ItemReward>(new ItemReward[] { new ItemReward() { itemCode = "game:gear-rusty", amount = 3 } })
            };

            var gather20meat = new Quest()
            {
                id = "vsvillage:quest-gather20meat",
                gatherObjectives = new List<Objective>(
                    new Objective[]
                    {
                        new Objective()
                        {
                            validCodes = new List<string>(new string[] { "bushmeat-raw", "bushmeat-cooked", "bushmeat-cured", "redmeat-raw", "redmeat-cooked", "redmeat-cured", "poultry-raw", "poultry-cooked", "poultry-cured" }),
                            demand = 20
                        }
                    }),
                killObjectives = new List<Objective>(),
                actionRewardIds = new List<string>(),
                itemRewards = new List<ItemReward>(new ItemReward[] { new ItemReward() { itemCode = "game:seeds-flax", amount = 8 } })
            };

            return new List<Quest>(new Quest[] { kill15drifters, kill4wolves, gather20meat });
        }
    }
}