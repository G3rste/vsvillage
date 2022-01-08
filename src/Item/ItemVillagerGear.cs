using System;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace VsVillage
{
    public class ItemVillagerGear : Item
    {
        public VillagerGearType type
        {
            get
            {
                VillagerGearType type;
                if (Enum.TryParse<VillagerGearType>(Attributes["cothingType"].AsString(), out type))
                {
                    return type;
                }
                else
                {
                    return VillagerGearType.TORSO;
                }
            }
        }
        public int backpackSlots { get { return Attributes["backpackslots"].AsInt(0); } }
    }
    public enum VillagerGearType
    {
        FACE, HEAD, TORSO, ARMS, HANDS, LEGS, FEET, BACK, WEAPON 
    }
}