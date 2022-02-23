using System.Collections.Generic;
using Vintagestory.GameContent;

namespace VsVillage
{
    public interface IVillagerPointOfInterest : IPointOfInterest
    {
        VillagerPointOfInterestOccasion occasion { get; }
        List<EntityVillager> villagers { get; }
        List<long> villagerIds { get; }
        bool canFit(EntityVillager candidate);
        void addVillager(EntityVillager candidate);
        bool tryAddVillager(EntityVillager candidate);
    }

    public enum VillagerPointOfInterestOccasion
    {
        FREETIME, WORK
    }
}