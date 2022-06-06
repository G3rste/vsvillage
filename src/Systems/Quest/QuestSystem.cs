using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;

namespace VsVillage
{
    public class QuestSystem : ModSystem
    {
        public Dictionary<string, Quest> questRegistry { get; private set; } = new Dictionary<string, Quest>();
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            List<Quest> quests=api.Assets.TryGet(new AssetLocation("vsvillage", "config/quests.json"))?.ToObject<List<Quest>>();
            if (quests == null) throw new FileNotFoundException("config/quests.json is missing.");

            foreach (var quest in quests)
            {
                questRegistry.Add(quest.id, quest);
            }

        }
    }
}