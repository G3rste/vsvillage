using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VsVillage
{
    public class InventoryVillagerGear : InventoryBase
    {

        ItemSlot[] slots;

        public ItemSlot leftHandSlot { get; set; }

        public ItemSlot rightHandSlot { get; set; }
        string owningEntity;


        public override ItemSlot this[int slotId] { get => slots[slotId]; set => slots[slotId] = value; }

        public override int Count => slots.Length;
        public InventoryVillagerGear(string owningEntity, string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            this.owningEntity = owningEntity;
            leftHandSlot = new ItemSlotUniversal(this);
            rightHandSlot = new ItemSlotUniversal(this);

            var gearTypes = Enum.GetNames(typeof(VillagerGearType));
            var slotsAsList = new List<string>(gearTypes)
                .ConvertAll<ItemSlot>(
                    gearType => new ItemSlotVillagerGear(
                        (VillagerGearType)Enum.Parse(typeof(VillagerGearType), gearType), owningEntity, this));
            slotsAsList.Add(rightHandSlot);
            slotsAsList.Add(leftHandSlot);
            slots = slotsAsList.ToArray();
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
            if (rightHandSlot.Empty)
            {
                var weightedSlot = new WeightedSlot();
                weightedSlot.weight = 1;
                weightedSlot.slot = rightHandSlot;
                return weightedSlot;
            }
            if (leftHandSlot.Empty)
            {
                var weightedSlot = new WeightedSlot();
                weightedSlot.weight = 0.5f;
                weightedSlot.slot = leftHandSlot;
                return weightedSlot;
            }
            return base.GetBestSuitedSlot(sourceSlot, skipSlots);
        }
    }
}