using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace VsVillage
{
    public class EntityBehaviorReplaceWithEntity : EntityBehavior
    {
        public EntityBehaviorReplaceWithEntity(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "replacewithentity";
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            var entityCode = attributes["entitycode"].AsString();


            AssetLocation location = new AssetLocation(entityCode);
            EntityProperties type = entity.World.GetEntityType(location);
            if (type == null)
            {
                entity.World.Logger.Error("ItemCreature: No such entity - {0}", location);
                return;
            }

            Entity newEntity = entity.World.ClassRegistry.CreateEntity(type);

            if (newEntity != null)
            {
                newEntity.ServerPos.SetFrom(entity.ServerPos);
                newEntity.PositionBeforeFalling.Set(newEntity.ServerPos.X, newEntity.ServerPos.Y, newEntity.ServerPos.Z);

                entity.World.SpawnEntity(newEntity);
                entity.Die(EnumDespawnReason.Removed);
            }
        }
    }
}