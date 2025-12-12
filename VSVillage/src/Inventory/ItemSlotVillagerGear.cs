using Vintagestory.API.Common;

namespace VsVillage
{
    public class ItemSlotVillagerGear : ItemSlotSurvival
    {
        VillagerGearType type { get; }
        string owningEntity { get; }
        public ItemSlotVillagerGear(VillagerGearType type, string owningEntity, InventoryBase inventory) : base(inventory)
        {
            this.type = type;
            this.owningEntity = owningEntity;
        }

        public ItemSlotVillagerGear(InventoryBase inventory) : base(inventory)
        {
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            return base.CanTakeFrom(sourceSlot, priority) && isCorrectAccessory(sourceSlot);
        }
        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            return base.CanHold(itemstackFromSourceSlot) && isCorrectAccessory(itemstackFromSourceSlot);
        }
        private bool isCorrectAccessory(ItemSlot sourceSlot)
        {
            var accessory = sourceSlot.Itemstack.Item as ItemVillagerGear;
            if (accessory != null)
            {
                return type == accessory.type;
            }
            return false;
        }
    }
}