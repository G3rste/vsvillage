using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace VsVillage{
    public class EntityBehaviorAlternatePathtraverser : EntityBehavior
    {

        public VillagerWaypointsTraverser villagerWaypointsTraverser {get; private set;}
        public EntityBehaviorAlternatePathtraverser(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            villagerWaypointsTraverser = new VillagerWaypointsTraverser(entity as EntityVillager);
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            villagerWaypointsTraverser.OnGameTick(deltaTime);
        }

        public override string PropertyName()
        {
            return "alternatepathtraverser";
        }
    }
}