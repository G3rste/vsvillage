using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage{
    public class EntityBehaviorVillager : EntityBehavior
    {

        public VillagerWaypointsTraverser villagerWaypointsTraverser {get; private set;}

        public string Profession => entity.Properties.Attributes["profession"].AsString();
        public string VillageId
        {
            get => entity.WatchedAttributes.GetString("villageId");
            set
            {
                entity.WatchedAttributes.SetString("villageId", value);
                entity.WatchedAttributes.MarkPathDirty("villageId");
            }
        }
        public string VillageName
        {
            get => entity.WatchedAttributes.GetString("villageName");
            set
            {
                entity.WatchedAttributes.SetString("villageName", value);
                entity.WatchedAttributes.MarkPathDirty("villageName");
            }
        }
        public BlockPos Workstation
        {
            get => entity.WatchedAttributes.GetBlockPos("workstation");
            set => entity.WatchedAttributes.SetBlockPos("workstation", value);
        }
        public BlockPos Bed
        {
            get => entity.WatchedAttributes.GetBlockPos("bed");
            set => entity.WatchedAttributes.SetBlockPos("bed", value);
        }

        private Village _village;
        public Village Village
        {
            get
            {
                if (_village == null && !string.IsNullOrEmpty(VillageId))
                {
                    _village = entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId);
                }
                return _village;
            }
        }

        public EntityBehaviorVillager(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            villagerWaypointsTraverser = new VillagerWaypointsTraverser(entity as EntityAgent);
            if (string.IsNullOrEmpty(VillageId))
            {
                var village = entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(entity.SidedPos.AsBlockPos);
                VillageId = village?.Id;
                VillageName = village?.Name;
                village?.VillagerSaveData.Add(entity.EntityId, new()
                {
                    Id = entity.EntityId,
                    Profession = Profession,
                    Name = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName ?? "S̷̡̪̦̮̜̮̳͑̅̀͛̓̋̌e̸̲̦̻̗͉̅̃ř̷͔̮̮̗̆͆̕͜v̵͈̥̩̳͊̄͘̕͠à̶̞̱̱̻́̀̈́͜͜n̷̫͕̣̓̇͘͜t̴̻̫̹̺̻͖͂̓ͅ"
                });
            }
            else
            {
                //load the village if not loaded
                entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId);
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            villagerWaypointsTraverser.OnGameTick(deltaTime);
        }
        
        public void RemoveVillage()
        {
            VillageId = null;
            VillageName = null;
            _village = null;
        }

        public override string PropertyName()
        {
            return "Villager";
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);
            if (!string.IsNullOrEmpty(VillageName))
            {
                infotext.AppendLine(Lang.Get("vsvillage:lives-in", VillageName, Workstation != null ? ManagementGui.BlockPosToString(Workstation, entity.Api) : Lang.Get("vsvillage:nowhere"), Bed != null ? ManagementGui.BlockPosToString(Bed, entity.Api) : Lang.Get("vsvillage:nowhere")));
            }
        }
    }
}