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

        private List<Objective> createSimpleObjective(string[] objective, int demand)
        {
            return new List<Objective>(new Objective[] { new Objective() { validCodes = new List<string>(objective), demand = demand } });
        }

        private List<ItemReward> createSimpleReward(string itemCode, int amount)
        {
            return new List<ItemReward>(new ItemReward[] { new ItemReward() { itemCode = itemCode, amount = amount } });
        }

        private List<Quest> getDefaultQuests()
        {
            var kill4Wolves = createSimpleObjective(new string[] { "wolf-male", "wolf-female", "wolf-pup" }, 4);
            var kill15Drifters = createSimpleObjective(new string[] { "drifter-normal", "drifter-deep", "drifter-tainted", "drifter-corrupt", "drifter-nightmare", "drifter-double-headed" }, 15);
            var kill3NightmareDrifters = createSimpleObjective(new string[] { "drifter-nightmare", "drifter-double-headed" }, 3);
            var gather20Meat = createSimpleObjective(new string[] { "bushmeat-raw", "bushmeat-cooked", "bushmeat-cured", "redmeat-raw", "redmeat-cooked", "redmeat-cured", "poultry-raw", "poultry-cooked", "poultry-cured" }, 20);
            var gather20CharCoal = createSimpleObjective(new string[] { "charcoal" }, 20);

            var for3Gears = createSimpleReward("game:gear-rusty", 3);
            var for5Gears = createSimpleReward("game:gear-rusty", 5);

            var for8FlaxSeeds = createSimpleReward("game:seeds-flax", 8);
            var for8ParsnipSeeds = createSimpleReward("game:seeds-parsnip", 8);
            var for8TurnipSeeds = createSimpleReward("game:seeds-turnip", 8);

            var kill15DriftersFor3Gears = new Quest()
            {
                id = "vsvillage:quest-kill15DriftersFor3Gears",
                gatherObjectives = new List<Objective>(),
                killObjectives = kill15Drifters,
                actionRewardIds = new List<string>(),
                itemRewards = for3Gears
            };

            var kill3NightmareDriftersFor5Gears = new Quest()
            {
                id = "vsvillage:quest-kill3NightmareDriftersFor5Gears",
                gatherObjectives = new List<Objective>(),
                killObjectives = kill3NightmareDrifters,
                actionRewardIds = new List<string>(),
                itemRewards = for5Gears
            };

            var kill4WolvesFor3Gears = new Quest()
            {
                id = "vsvillage:quest-kill4WolvesFor3Gears",
                gatherObjectives = new List<Objective>(),
                killObjectives = kill4Wolves,
                actionRewardIds = new List<string>(),
                itemRewards = for3Gears
            };

            var gather20MeatFor8FlaxSeeds = new Quest()
            {
                id = "vsvillage:quest-gather20MeatFor8FlaxSeeds",
                gatherObjectives = gather20Meat,
                killObjectives = new List<Objective>(),
                actionRewardIds = new List<string>(),
                itemRewards = for8FlaxSeeds
            };

            var gather20MeatFor8ParsnipSeeds = new Quest()
            {
                id = "vsvillage:quest-gather20MeatFor8ParsnipSeeds",
                gatherObjectives = gather20Meat,
                killObjectives = new List<Objective>(),
                actionRewardIds = new List<string>(),
                itemRewards = for8ParsnipSeeds
            };

            var gather20MeatFor8TurnipSeeds = new Quest()
            {
                id = "vsvillage:quest-gather20MeatFor8TurnipSeeds",
                gatherObjectives = gather20Meat,
                killObjectives = new List<Objective>(),
                actionRewardIds = new List<string>(),
                itemRewards = for8TurnipSeeds
            };

            return new List<Quest>(new Quest[] { kill15DriftersFor3Gears, kill3NightmareDriftersFor5Gears, kill4WolvesFor3Gears, gather20MeatFor8FlaxSeeds, gather20MeatFor8ParsnipSeeds, gather20MeatFor8TurnipSeeds });
        }
    }
}