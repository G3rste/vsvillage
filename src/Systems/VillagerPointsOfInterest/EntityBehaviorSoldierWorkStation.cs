using System;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class EntityBehaviorSoldierWorkStation : EntityBehavior, IVillagerPointOfInterest
    {
        public EntityBehaviorSoldierWorkStation(Entity entity) : base(entity)
        {
        }

        protected virtual int maximumWorkers => 3;

        public List<EntityVillager> villager => villagerIds.ConvertAll<EntityVillager>(value => entity.World.GetEntityById(value) as EntityVillager);

        public List<long> villagerIds => new List<string>(entity.Attributes.GetStringArray("workers", new string[0])).ConvertAll<long>(Convert.ToInt64);

        public Vec3d Position => entity.ServerPos.XYZ;

        public string Type => "soldierworkstation";

        public VillagerPointOfInterestOccasion occasion => VillagerPointOfInterestOccasion.WORK;
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            var sapi = entity.Api as ICoreServerAPI;
            if (sapi != null)
            {
                sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
            }
        }

        public void addVillager(EntityVillager villager)
        {
            var list = villagerIds;
            list.Add(villager.EntityId);
            entity.Attributes.SetStringArray("workers", list.ConvertAll<string>(Convert.ToString).ToArray());
        }

        public bool canFit(EntityVillager villager)
        {
            if (villager.profession == "soldier")
            {
                if (villagerIds.Count < maximumWorkers) { return true; }
                var aliveWorkers = new List<long>();
                foreach (var worker in this.villager)
                {
                    if (worker != null && worker.Alive)
                    {
                        aliveWorkers.Add(worker.EntityId);
                    }
                }
                entity.Attributes.SetStringArray("workers", aliveWorkers.ConvertAll<string>(Convert.ToString).ToArray());
                if (villagerIds.Count < maximumWorkers) { return true; }
            }
            return false;
        }

        public bool tryAddVillager(EntityVillager villager)
        {
            if (canFit(villager))
            {
                addVillager(villager);
                return true;
            }
            return false;
        }

        public override string PropertyName()
        {
            return "soldierstation";
        }
    }
}