using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityBehaviorVillagerPOI : BlockEntityBehavior, IVillagerPointOfInterest
    {

        public int maximumVillagers { get; set; }
        public string forProfession { get; set; }
        private VillagerPointOfInterestOccasion _occasion;
        public VillagerPointOfInterestOccasion occasion => _occasion;

        public List<EntityVillager> villagers => _villagerIds.ConvertAll<EntityVillager>(value => Blockentity.Api.World.GetEntityById(value) as EntityVillager);

        private List<long> _villagerIds = new List<long>();
        public List<long> villagerIds => _villagerIds;

        public Vec3d Position => Blockentity.Pos.ToVec3d();

        private string _Type;
        public string Type => _Type;
        public BlockEntityBehaviorVillagerPOI(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            if (!Enum.TryParse<VillagerPointOfInterestOccasion>(properties["occasion"].AsString().ToUpper(), out _occasion))
            {
                _occasion = VillagerPointOfInterestOccasion.WORK;
            }
            forProfession = properties["forProfession"].AsString();
            _Type = properties["type"].AsString("villagerPOI");
            maximumVillagers = properties["maximumVillagers"].AsInt(1);

            var sapi = api as ICoreServerAPI;
            if (sapi != null)
            {
                sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            if (tree.HasAttribute("villagers") && !String.IsNullOrEmpty(tree.GetString("villagers")))
            {
                _villagerIds = new List<string>(tree.GetString("villagers").Split(',')).ConvertAll<long>(Convert.ToInt64);
            }
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (_villagerIds.Count > 0)
            {
                String.Join(",", _villagerIds.ToArray());
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            var sapi = Blockentity.Api as ICoreServerAPI;
            if (sapi != null)
            {
                sapi.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public void addVillager(EntityVillager candidate)
        {
            _villagerIds.Add(candidate.EntityId);
        }

        public bool canFit(EntityVillager candidate)
        {
            if (String.IsNullOrEmpty(forProfession) || forProfession == candidate.profession)
            {
                if (villagerIds.Count < maximumVillagers) { return true; }
                var aliveVillagers = new List<long>();
                foreach (var villager in villagers)
                {
                    if (villager != null && villager.Alive)
                    {
                        aliveVillagers.Add(villager.EntityId);
                    }
                }
                _villagerIds = aliveVillagers;
                if (villagerIds.Count < maximumVillagers) { return true; }
                if (villagerIds.Contains(candidate.EntityId)) { return true; }
            }
            return false;
        }

        public bool tryAddVillager(EntityVillager candidate)
        {
            if (canFit(candidate))
            {
                addVillager(candidate);
                return true;
            }
            return false;
        }
    }
}