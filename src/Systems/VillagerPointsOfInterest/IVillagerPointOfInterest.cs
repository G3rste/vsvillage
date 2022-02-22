using System.Collections.Generic;
using Vintagestory.GameContent;

namespace VsVillage
{
    public interface IVillagerPointOfInterest : IPointOfInterest
    {
        VillagerPointOfInterestOccasion occasion { get; }
        List<EntityVillager> workers { get; }
        List<long> workerIds { get; }
        bool canFit(EntityVillager villager);
        void addWorker(EntityVillager villager);
        bool tryAddWorker(EntityVillager villager);
    }

    public enum VillagerPointOfInterestOccasion
    {
        FREETIME, WORK
    }
}