using Vintagestory.API.Common;

namespace VsVillage
{
    public class VsVillage : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntity("EntityVillager", typeof(EntityVillager));

            api.RegisterItemClass("ItemVillagerEquipment", typeof(ItemVillagerGear));
        }
    }
}
