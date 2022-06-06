using System.Collections.Generic;

namespace VsVillage
{
    public class Quest
    {
        public string id { get; set; }
        public int cooldown { get; set; }
        public bool perPlayer { get; set; }
        public List<string> prerequesits { get; set; }
        public List<Objective> gatherObjectives { get; set; }
        public List<Objective> killObjectives { get; set; }
        public List<ItemReward> itemRewards { get; set; }
        public List<string> actionRewardIds { get; set; }
    }

    public class Objective
    {
        public List<string> validCodes { get; set; }
        public int demand { get; set; }
    }

    public class ItemReward
    {
        public string itemCode { get; set; }
        public int amount { get; set; }
    }
}