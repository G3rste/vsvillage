using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using System;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class VillageManager : ModSystem
    {

        public ConcurrentDictionary<string, Village> Villages = new ConcurrentDictionary<string, Village>();
        private ICoreAPI Api;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            Api = api;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            api.Event.GameWorldSave += () => OnSave(api);

            api.Network.RegisterChannel("villagemanagementnetwork")
                .RegisterMessageType<Village>()
                .RegisterMessageType<VillageManagementMessage>().SetMessageHandler<VillageManagementMessage>((fromPlayer, message) => OnManagementMessage(fromPlayer, message, api));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            api.Network.RegisterChannel("villagemanagementnetwork")
                .RegisterMessageType<Village>().SetMessageHandler<Village>(village => OnVillageMessage(village, api))
                .RegisterMessageType<VillageManagementMessage>();
        }

        public Village GetVillage(string id)
        {
            if (string.IsNullOrEmpty(id)) { return null; }
            Village villageData;
            if (!Villages.TryGetValue(id, out villageData))
            {
                if (Api is ICoreServerAPI sapi)
                {
                    try
                    {
                        byte[] data = sapi.WorldManager.SaveGame.GetData(id);
                        villageData = data == null ? null : SerializerUtil.Deserialize<Village>(data);
                        villageData?.Init(sapi);
                        if (villageData != null)
                        {
                            Villages.TryAdd(id, villageData);
                        }
                    }
                    catch (Exception)
                    {
                        Api.Logger.Error(string.Format("Village with id={0} could not be loaded. Maybe it was removed/ outdated/ corrupted. I guess we will never know for sure because I am too lazy to log this information.", id));
                    }
                }
            }
            return villageData;
        }

        public Village GetVillage(BlockPos pos)
        {
            foreach (var village in Villages.Values)
            {
                if (village.Pos.HorDistanceSqTo(pos.X, pos.Z) < village.Radius * village.Radius)
                {
                    return village;
                }
            }
            return null;
        }


        private void OnSave(ICoreServerAPI sapi)
        {
            foreach (var village in Villages.Values)
            {
                sapi.WorldManager.SaveGame.StoreData(village.Id, SerializerUtil.Serialize(village));
            }
        }
        public void RemoveVillage(string id)
        {
            if (Villages.ContainsKey(id))
            {
                var village = Villages.Get(id);
                Villages.Remove(id);
                (Api as ICoreServerAPI).WorldManager.SaveGame.StoreData(id, null);
                village.Workstations.ForEach(workstation => Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(workstation.Pos)?.RemoveVillage());
                village.Gatherplaces.ForEach(gatherplace => Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBrazier>(gatherplace)?.RemoveVillage());
                village.Beds.ForEach(bed => Api.World.BlockAccessor.GetBlockEntity<BlockEntityBed>(bed.Pos)?.GetBehavior<BlockEntityBehaviorVillagerBed>()?.RemoveVillage());
                village.Villagers.ForEach(villager => villager?.RemoveVillage());
            }
        }

        private void OnVillageMessage(Village village, ICoreClientAPI capi)
        {
            village.Init(capi);
            new ManagementGui(capi, village.Pos, village).TryOpen();
        }

        private void OnManagementMessage(IServerPlayer fromPlayer, VillageManagementMessage message, ICoreServerAPI api)
        {
            switch(message.Operation)
            {
                case EnumVillageManagementOperation.create:
                    var village = new Village()
                    {
                        Radius = message.Radius > 0 ? message.Radius : 20,
                        Pos = message.Pos,
                        Name = string.IsNullOrEmpty(message.Name) ? "Lauras little World" : message.Name
                    };
                    village.Init(api);
                    var workstation = api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(message.Pos);
                    workstation.VillageId = village.Id;
                    workstation.VillageName = village.Name;
                    workstation.MarkDirty();
                    village.Workstations.Add(new() { OwnerId = -1, Pos = message.Pos, Profession = workstation.Type });
                    Villages.TryAdd(village.Id, village);
                    break;
                case EnumVillageManagementOperation.destroy:
                    RemoveVillage(message.Id);
                    break;
                case EnumVillageManagementOperation.changeStats:
                    village = GetVillage(message.Id);
                    village.Radius = message.Radius;
                    village.Name = message.Name;
                    break;
                case EnumVillageManagementOperation.removeStructure:
                    village = GetVillage(message.Id);

                    var workStructures = village.Workstations.FindAll(candidate => candidate.Pos == message.StructureToRemove);
                    workStructures.ForEach(candidate => Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(candidate.Pos)?.RemoveVillage());
                    village.Workstations.RemoveAll(candidate => workStructures.Contains(candidate));

                    
                    var gatherStructures = village.Gatherplaces.FindAll(candidate => candidate == message.StructureToRemove);
                    gatherStructures.ForEach(candidate => Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBrazier>(candidate)?.RemoveVillage());
                    village.Gatherplaces.RemoveAll(candidate => gatherStructures.Contains(candidate));

                    
                    var bedStructures = village.Beds.FindAll(candidate => candidate.Pos == message.StructureToRemove);
                    bedStructures.ForEach(candidate => Api.World.BlockAccessor.GetBlockEntity<BlockEntityBed>(candidate.Pos)?.GetBehavior<BlockEntityBehaviorVillagerBed>()?.RemoveVillage());
                    village.Beds.RemoveAll(candidate => bedStructures.Contains(candidate));

                    break;
                case EnumVillageManagementOperation.removeVillager:
                    village = GetVillage(message.Id);

                    var villagerIds = village.VillagerSaveData.FindAll(candidate => candidate.Id == message.VillagerToRemove);
                    villagerIds.ConvertAll<EntityVillager>(candidate => Api.World.GetEntityById(candidate.Id) as EntityVillager).ForEach(villager => villager.RemoveVillage());
                    village.VillagerSaveData.RemoveAll(candidate => villagerIds.Contains(candidate));
                    break;
            }
        }
    }
}