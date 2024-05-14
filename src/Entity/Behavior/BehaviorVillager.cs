using System;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public class EntityBehaviorVillager : EntityBehavior
    {

        public VillagerWaypointsTraverser villagerWaypointsTraverser { get; private set; }

        public EnumVillagerProfession Profession => Enum.Parse<EnumVillagerProfession>(entity.Properties.Attributes["profession"].AsString());
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
            set
            {
                if (value != null)
                    entity.WatchedAttributes.SetBlockPos("workstation", value);
                else
                    entity.WatchedAttributes.RemoveAttribute("workstationX");
            }
        }
        public BlockPos Bed
        {
            get => entity.WatchedAttributes.GetBlockPos("bed");
            set
            {
                if (value != null)
                    entity.WatchedAttributes.SetBlockPos("bed", value);
                else
                    entity.WatchedAttributes.RemoveAttribute("bedX");
            }
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
            if (entity.Api.Side == EnumAppSide.Client) return;

            villagerWaypointsTraverser = new VillagerWaypointsTraverser(entity as EntityAgent);

            var village = string.IsNullOrEmpty(VillageId)
                ? entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(entity.ServerPos.AsBlockPos)
                : entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId);
            if (village != null && (village.Id != VillageId || village.Name != VillageName || !village.VillagerSaveData.ContainsKey(entity.EntityId)))
            {
                VillageId = village.Id;
                VillageName = village.Name;
                village.VillagerSaveData[entity.EntityId] = new()
                {
                    Id = entity.EntityId,
                    Profession = Profession,
                    Name = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName ?? "S̷̡̪̦̮̜̮̳͑̅̀͛̓̋̌e̸̲̦̻̗͉̅̃ř̷͔̮̮̗̆͆̕͜v̵͈̥̩̳͊̄͘̕͠à̶̞̱̱̻́̀̈́͜͜n̷̫͕̣̓̇͘͜t̴̻̫̹̺̻͖͂̓ͅ"
                };
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            villagerWaypointsTraverser?.OnGameTick(deltaTime);
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
                infotext.AppendLine(Lang.Get("vsvillage:lives-in", Lang.Get(VillageName), Workstation != null ? ManagementGui.BlockPosToString(Workstation, entity.Api) : Lang.Get("vsvillage:nowhere"), Bed != null ? ManagementGui.BlockPosToString(Bed, entity.Api) : Lang.Get("vsvillage:nowhere")));
            }
        }
    }
}