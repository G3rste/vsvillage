using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class VsVillage : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntityBehaviorClass("Villager", typeof(EntityBehaviorVillager));
            api.RegisterEntityBehaviorClass("replacewithentity", typeof(EntityBehaviorReplaceWithEntity));

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

            ActivityModSystem.ActionTypes.TryAdd(GotoPointOfInterestAction.ActionType, typeof(GotoPointOfInterestAction));
            ActivityModSystem.ActionTypes.TryAdd(SleepAction.ActionType, typeof(SleepAction));
            ActivityModSystem.ActionTypes.TryAdd(ToggleBrazierFireAction.ActionType, typeof(ToggleBrazierFireAction));

            ActivityModSystem.ConditionTypes.TryAdd(CloseToPointOfInterestCondition.ConditionType, typeof(CloseToPointOfInterestCondition));
        }
    }
}
