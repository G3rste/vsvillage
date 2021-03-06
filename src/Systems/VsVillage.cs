using VsQuest;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System;

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

            api.RegisterBlockEntityClass("Lectern", typeof(BlockEntityLectern));

            AiTaskRegistry.Register<AiTaskVillagerMeleeAttack>("villagermeleeattack");
            AiTaskRegistry.Register<AiTaskVillagerSeekEntity>("villagerseekentity");
            AiTaskRegistry.Register<AiTaskVillagerGoto>("villagergoto");
            AiTaskRegistry.Register<AiTaskVillagerSleep>("villagersleep");
            AiTaskRegistry.Register<AiTaskVillagerSocialize>("villagersocialize");
            AiTaskRegistry.Register<AiTaskVillagerGotoPOI>("villagergotopoi");
            AiTaskRegistry.Register<AiTraskVillagerFillTrough>("villagerfilltrough");
            AiTaskRegistry.Register<AiTraskVillagerCultivateCrops>("villagercultivatecrops");
            AiTaskRegistry.Register<AiTraskVillagerFlipWeapon>("villagerflipweapon");
            AiTaskRegistry.Register<AiTaskStayCloseToEmployer>("villagerstayclose");

            var questSystem = api.ModLoader.GetModSystem<QuestSystem>();

            questSystem.actionRewardRegistry.Add("spawnsoldier", (message, byPlayer) => spawnVillager(message, api, "soldier"));
            questSystem.actionRewardRegistry.Add("spawnfarmer", (message, byPlayer) => spawnVillager(message, api, "farmer"));
            questSystem.actionRewardRegistry.Add("spawnshepherd", (message, byPlayer) => spawnVillager(message, api, "shepherd"));
            questSystem.actionRewardRegistry.Add("spawnsmith", (message, byPlayer) => spawnVillager(message, api, "smith"));
            questSystem.actionRewardRegistry.Add("recruitvillager", recruitVillager(api));

            questSystem.actionObjectiveRegistry.Add("add1villagerbed", new EnoughBedsObjective(1));
            questSystem.actionObjectiveRegistry.Add("add2villagerbed", new EnoughBedsObjective(2));
            questSystem.actionObjectiveRegistry.Add("add3villagerbed", new EnoughBedsObjective(3));
            questSystem.actionObjectiveRegistry.Add("add1villagerfreetime", new EnoughFirepitsObjective(1));
            questSystem.actionObjectiveRegistry.Add("add2villagerfreetime", new EnoughFirepitsObjective(2));
            questSystem.actionObjectiveRegistry.Add("add3villagerfreetime", new EnoughFirepitsObjective(3));
            questSystem.actionObjectiveRegistry.Add("add1smithwork", new EnoughAnvilsObjective(1));
            questSystem.actionObjectiveRegistry.Add("add1soldierwork", new EnoughStrawdummiesObjective(1));
            questSystem.actionObjectiveRegistry.Add("add1shepherdwork", new EnoughTroughsObjective(1));
            questSystem.actionObjectiveRegistry.Add("add1farmerwork", new EnoughQuernsObjective(1));
            questSystem.actionObjectiveRegistry.Add("add2farmerwork", new EnoughQuernsObjective(2));

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

        private static void spawnVillager(QuestCompletedMessage message, ICoreAPI api, string profession)
        {
            var sex = api.World.Rand.NextDouble() < 0.5 ? "male" : "female";
            var entity = api.World.ClassRegistry.CreateEntity(api.World.GetEntityType(new AssetLocation("vsvillage", string.Format("humanoid-villager-{0}-{1}", sex, profession))));
            entity.ServerPos = api.World.GetEntityById(message.questGiverId).ServerPos.Copy();
            api.World.SpawnEntity(entity);
        }

        private static Action<QuestCompletedMessage, IPlayer> recruitVillager(ICoreAPI api)
        {
            return (message, byPlayer) =>
            {
                var recruit = api.World.GetEntityById(message.questGiverId);
                recruit.WatchedAttributes.SetDouble("employedSince", api.World.Calendar.TotalHours);
                recruit.WatchedAttributes.SetString("guardedPlayerUid", byPlayer.PlayerUID);
                recruit.WatchedAttributes.SetBool("commandSit", false);
                recruit.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
            };
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TalkUtilMessage
    {
        public long entityId;

        public EnumTalkType talkType;
    }
}
