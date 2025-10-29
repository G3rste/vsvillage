using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class EntityBehaviorVillager : EntityBehavior
    {
        public VillagerPathfind Pathfind;
        public EnumVillagerProfession Profession;
        public string VillageId
        {
            get => entity.WatchedAttributes.GetString("villageId");
        }
        public string VillageName
        {
            get => entity.WatchedAttributes.GetString("villageName");
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
                entity.WatchedAttributes.MarkPathDirty("workstationX");
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
                entity.WatchedAttributes.MarkPathDirty("bedX");
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
            set
            {
                _village = value;
                entity.WatchedAttributes.SetString("villageId", value?.Id);
                entity.WatchedAttributes.MarkPathDirty("villageId");
                entity.WatchedAttributes.SetString("villageName", value?.Name);
                entity.WatchedAttributes.MarkPathDirty("villageName");
            }
        }

        public EntityBehaviorVillager(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            Profession = Enum.Parse<EnumVillagerProfession>(attributes["profession"].AsString());
            if (entity.Api is ICoreServerAPI sapi)
            {
                Pathfind = new VillagerPathfind(entity.Api as ICoreServerAPI);
                // when this method is called, the chunk might not be loaded, therefore the village blocks might not have initialized the village, so we have to wait a short time
                entity.World.RegisterCallback(dt => InitVillageAfterChunkLoading(), 5000);
            }
        }

        private void InitVillageAfterChunkLoading()
        {
            Village village = null;
            if (!entity.Alive)
            {
                return;
            }
            if (!string.IsNullOrEmpty(VillageId))
            {
                village = entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(VillageId);
            }
            if (village == null)
            {
                village = entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(entity.ServerPos.AsBlockPos);
            }

            if (village != null)
            {
                Village = village;
                village.VillagerSaveData[entity.EntityId] = new()
                {
                    Id = entity.EntityId,
                    Profession = Profession,
                    Name = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName ?? "S̷̡̪̦̮̜̮̳͑̅̀͛̓̋̌e̸̲̦̻̗͉̅̃ř̷͔̮̮̗̆͆̕͜v̵͈̥̩̳͊̄͘̕͠à̶̞̱̱̻́̀̈́͜͜n̷̫͕̣̓̇͘͜t̴̻̫̹̺̻͖͂̓ͅ"
                };
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            Village?.RemoveVillager(entity.EntityId);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            Village?.RemoveVillager(entity.EntityId);
        }

        public void RemoveVillage()
        {
            Village = null;
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
                if (entity.Api is ICoreClientAPI capi && capi.Settings.Bool["showEntityDebugInfo"])
                {
                    infotext.AppendLine(Lang.Get("vsvillage:lives-in-debug", Lang.Get(VillageName), Workstation != null ? ManagementGui.BlockPosToString(Workstation, entity.Api) : Lang.Get("vsvillage:nowhere"), Bed != null ? ManagementGui.BlockPosToString(Bed, entity.Api) : Lang.Get("vsvillage:nowhere")));
                }
                else
                {
                    infotext.AppendLine(Lang.Get("vsvillage:lives-in", Lang.Get(VillageName)));
                }
            }
            infotext.AppendLine(Lang.Get("vsvillage:management-profession", Lang.Get("vsvillage:management-profession-" + Profession.ToString())));
        }
    }
}