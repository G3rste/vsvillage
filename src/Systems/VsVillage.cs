using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class VsVillage : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntity("EntityVillager", typeof(EntityVillager));

            api.RegisterEntityBehaviorClass("AlternatePathTraverser", typeof(EntityBehaviorAlternatePathtraverser));

            api.RegisterItemClass("ItemVillagerGear", typeof(ItemVillagerGear));
            
            api.RegisterBlockEntityBehaviorClass("VillagerBed", typeof(BlockEntityBehaviorVillagerBed));

            AiTaskRegistry.Register<AiTaskVillagerMeleeAttack>("villagermeleeattack");
            AiTaskRegistry.Register<AiTaskVillagerGoto>("villagergoto");
            AiTaskRegistry.Register<AiTaskVillagerSleep>("villagersleep");
        }
    }
}
