﻿using ProtoBuf;
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

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntity("EntityVillager", typeof(EntityVillager));

            api.RegisterEntityBehaviorClass("Villager", typeof(EntityBehaviorVillager));

            api.RegisterItemClass("ItemVillagerGear", typeof(ItemVillagerGear));
            api.RegisterItemClass("ItemVillagerHorn", typeof(ItemVillagerHorn));

            api.RegisterBlockEntityClass("VillagerBed", typeof(BlockEntityVillagerBed));
            api.RegisterBlockEntityClass("VillagerWorkstation", typeof(BlockEntityVillagerWorkstation));
            api.RegisterBlockEntityClass("VillagerWaypoint", typeof(BlockEntityVillagerWaypoint));
            api.RegisterBlockEntityClass("VillagerBrazier", typeof(BlockEntityVillagerBrazier));

            api.RegisterBlockClass("MayorWorkstation", typeof(BlockMayorWorkstation));

            AiTaskRegistry.Register<AiTaskVillagerMeleeAttack>("villagermeleeattack");
            AiTaskRegistry.Register<AiTaskVillagerSeekEntity>("villagerseekentity");
            AiTaskRegistry.Register<AiTaskVillagerGoto>("villagergoto");
            AiTaskRegistry.Register<AiTaskVillagerSleep>("villagersleep");
            AiTaskRegistry.Register<AiTaskVillagerSocialize>("villagersocialize");
            AiTaskRegistry.Register<AiTaskVillagerGotoWork>("villagergotowork");
            AiTaskRegistry.Register<AiTaskVillagerGotoGatherspot>("villagergotogather");
            AiTaskRegistry.Register<AiTraskVillagerFillTrough>("villagerfilltrough");
            AiTaskRegistry.Register<AiTraskVillagerCultivateCrops>("villagercultivatecrops");
            AiTaskRegistry.Register<AiTraskVillagerFlipWeapon>("villagerflipweapon");
            AiTaskRegistry.Register<AiTaskStayCloseToEmployer>("villagerstayclose");
            AiTaskRegistry.Register<AiTaskHealWounded>("villagerhealwounded");
            AiTaskRegistry.Register<AiTaskVillagerRangedAttack>("villagerrangedattack");
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.clientAPI = api;

            api.Network.RegisterChannel("villagertalknetwork")
                .RegisterMessageType<TalkUtilMessage>().SetMessageHandler<TalkUtilMessage>(OnTalkMessageClient);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.serverAPI = api;
            api.Network.RegisterChannel("villagertalknetwork")
                .RegisterMessageType<TalkUtilMessage>();
        }

        private void OnTalkMessageClient(TalkUtilMessage networkMessage)
        {
            (clientAPI.World.GetEntityById(networkMessage.entityId) as EntityVillager)?.talkUtil.Talk(networkMessage.talkType);
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TalkUtilMessage
    {
        public long entityId;

        public EnumTalkType talkType;
    }
}
