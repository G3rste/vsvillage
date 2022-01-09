using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VsVillage
{
    public class InventoryVillagerGear : InventoryBase
    {

        ItemSlot[] slots;
        string owningEntity;


        public override ItemSlot this[int slotId] { get => slots[slotId]; set => slots[slotId] = value; }

        public override int Count => slots.Length;
        public InventoryVillagerGear(string owningEntity, string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            this.owningEntity = owningEntity;
            var gearTypes = Enum.GetNames(typeof(VillagerGearType));
            slots = new List<string>(gearTypes)
                .ConvertAll<ItemSlotVillagerGear>(
                    gearType => new ItemSlotVillagerGear(
                        (VillagerGearType)Enum.Parse(typeof(VillagerGearType), gearType), owningEntity, this))
                .ToArray();
        }
        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree, slots);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }
        public override void LateInitialize(string inventoryID, ICoreAPI api)
        {
            base.LateInitialize(inventoryID, api);
            foreach (var slot in slots)
            {
                slot.Itemstack?.ResolveBlockOrItem(api.World);
            }
        }

        public override WeightedSlot GetBestSuitedSlot(ItemSlot sourceSlot, List<ItemSlot> skipSlots = null)
        {
            var accessory = sourceSlot?.Itemstack?.Item as ItemVillagerGear;
            if (accessory != null)
            {
                var weightedSlot = new WeightedSlot();
                weightedSlot.weight = 1;
                weightedSlot.slot = slots[(int)accessory.type];
                return weightedSlot;
            }
            return base.GetBestSuitedSlot(sourceSlot, skipSlots);
        }
    }
}