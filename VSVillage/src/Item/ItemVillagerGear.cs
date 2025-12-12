using System;
using Vintagestory.API.Common;

namespace VsVillage
{
    public class ItemVillagerGear : Item
    {
        public VillagerGearType type
        {
            get
            {
                VillagerGearType type;
                if (Enum.TryParse<VillagerGearType>(Variant["type"].ToUpper(), out type))
                {
                    return type;
                }
                else
                {
                    return VillagerGearType.HEAD;
                }
            }
        }
        public string weaponAssetLocation => Attributes["weaponAssetLocation"].AsString();
        public int backpackSlots => Attributes["backpackslots"].AsInt(0);
    }
    public enum VillagerGearType
    {
        HEAD, BACK, CHEST, SHOULDERS, BELT, BELTSLOT, ARMS, THIGH, FEET, WEAPON
    }
}