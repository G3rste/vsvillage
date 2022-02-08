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

            api.RegisterItemClass("ItemVillagerGear", typeof(ItemVillagerGear));

            AiTaskRegistry.Register<AiTaskVillagerMeleeAttack>("villagermeleeattack");
        }
    }
}
