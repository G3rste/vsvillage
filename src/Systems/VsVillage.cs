using VsQuest;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class VsVillage : ModSystem
    {
        private ICoreClientAPI clientAPI;

        private ICoreServerAPI serverAPI;

        private Harmony harmony = new Harmony("gerste.vsvillage");

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            BedPatch.Patch(harmony);

            api.RegisterEntity("EntityVillager", typeof(EntityVillager));

            api.RegisterEntityBehaviorClass("AlternatePathTraverser", typeof(EntityBehaviorAlternatePathtraverser));
            api.RegisterEntityBehaviorClass("SoldierWorkStation", typeof(EntityBehaviorSoldierWorkStation));

            api.RegisterItemClass("ItemVillagerGear", typeof(ItemVillagerGear));

            api.RegisterBlockEntityBehaviorClass("VillagerBed", typeof(BlockEntityBehaviorVillagerBed));
            api.RegisterBlockEntityBehaviorClass("VillagerPOI", typeof(BlockEntityBehaviorVillagerPOI));

            AiTaskRegistry.Register<AiTaskVillagerMeleeAttack>("villagermeleeattack");
            AiTaskRegistry.Register<AiTaskVillagerSeekEntity>("villagerseekentity");
            AiTaskRegistry.Register<AiTaskVillagerGoto>("villagergoto");
            AiTaskRegistry.Register<AiTaskVillagerSleep>("villagersleep");
            AiTaskRegistry.Register<AiTaskVillagerSocialize>("villagersocialize");
            AiTaskRegistry.Register<AiTaskVillagerGotoPOI>("villagergotopoi");
            AiTaskRegistry.Register<AiTraskVillagerFillTrough>("villagerfilltrough");
            AiTaskRegistry.Register<AiTraskVillagerCultivateCrops>("villagercultivatecrops");
            AiTaskRegistry.Register<AiTraskVillagerFlipWeapon>("villagerflipweapon");

            var questSystem = api.ModLoader.GetModSystem<QuestSystem>();

            questSystem.actionRewardRegistry.Add("spawnsoldier", message => spawnVillager(message, api, "soldier"));
            questSystem.actionRewardRegistry.Add("spawnfarmer", message => spawnVillager(message, api, "farmer"));
            questSystem.actionRewardRegistry.Add("spawnshepherd", message => spawnVillager(message, api, "shepherd"));
            questSystem.actionRewardRegistry.Add("spawnsmith", message => spawnVillager(message, api, "smith"));

            questSystem.actionObjectiveRegistry.Add("add1villagerbed", new MoreBedsThanVillagersObjective(1));
            questSystem.actionObjectiveRegistry.Add("add2villagerbed", new MoreBedsThanVillagersObjective(2));
            questSystem.actionObjectiveRegistry.Add("add3villagerbed", new MoreBedsThanVillagersObjective(3));

        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.clientAPI = api;

            api.Network.RegisterChannel("vsvillagenetwork")
                .RegisterMessageType<TalkUtilMessage>().SetMessageHandler<TalkUtilMessage>(OnTalkMessageClient);
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.serverAPI = api;
            api.Network.RegisterChannel("vsvillagenetwork")
                .RegisterMessageType<TalkUtilMessage>();
        }

        private void OnTalkMessageClient(TalkUtilMessage networkMessage)
        {
            (clientAPI.World.GetEntityById(networkMessage.entityId) as EntityVillager)?.talkUtil.Talk(networkMessage.talkType);
        }

        private void spawnVillager(QuestCompletedMessage message, ICoreAPI api, string profession)
        {
            var sex = api.World.Rand.NextDouble() < 0.5 ? "male" : "female";
            var entity = api.World.ClassRegistry.CreateEntity(api.World.GetEntityType(new AssetLocation("vsvillage", string.Format("humanoid-villager-{0}-{1}", sex, profession))));
            entity.ServerPos = api.World.GetEntityById(message.questGiverId).ServerPos.Copy();
            api.World.SpawnEntity(entity);
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TalkUtilMessage
    {
        public long entityId;

        public EnumTalkType talkType;
    }
}
