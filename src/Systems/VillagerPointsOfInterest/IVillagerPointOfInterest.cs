using System.Collections.Generic;
using Vintagestory.GameContent;

namespace VsVillage
{
    public interface IVillagerPointOfInterest : IPointOfInterest
    {
        VillagerPointOfInterestOccasion occasion { get; }
        List<EntityVillager> villager { get; }
        List<long> villagerIds { get; }
        bool canFit(EntityVillager villager);
        void addVillager(EntityVillager villager);
        bool tryAddVillager(EntityVillager villager);
    }

    public enum VillagerPointOfInterestOccasion
    {
        FREETIME, WORK
    }
}