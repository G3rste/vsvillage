using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using System;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;
using System.Text.RegularExpressions;
using System.Linq;

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
                        villageData = data == null || data.Length < 10 ? null : SerializerUtil.Deserialize<Village>(data);
                        villageData?.Init(sapi);
                        if (villageData != null)
                        {
                            Villages.TryAdd(id, villageData);
                        }
                    }
                    catch (Exception)
                    {
                        Api.Logger.Error(string.Format("Village with id={0} could not be loaded and will be newly created. Maybe it was removed/ outdated/ corrupted. I guess we will never know for sure because I am too lazy to log this information.", id));
                        var pos = Regex.Match(id, @"village-(\d+), (\d+), (\d+)").Groups.Values.ToList().GetRange(1, 3).ConvertAll(number => int.Parse(number.Value));
                        villageData = new() { Pos = new BlockPos(pos[0], pos[1], pos[2], 0), Radius = 50, Name = "Lauras little World" };
                        Villages.TryAdd(id, villageData);
                    }
                }
            }
            return villageData;
        }

        public Village GetVillage(BlockPos pos)
        {
            foreach (var village in Villages.Values)
            {
                var villagePos = village.Pos;
                var radius = village.Radius;
                if (villagePos.X - radius <= pos.X
                    && villagePos.X + radius >= pos.X
                    && villagePos.Z - radius <= pos.Z
                    && villagePos.Z + radius >= pos.Z)
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
                village.Workstations.Values.Foreach(workstation => Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(workstation.Pos)?.RemoveVillage());
                village.Gatherplaces.Foreach(gatherplace => Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBrazier>(gatherplace)?.RemoveVillage());
                village.Beds.Values.Foreach(bed => Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBed>(bed.Pos)?.RemoveVillage());
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
            switch (message.Operation)
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
                    village.Workstations.Add(message.Pos, new() { OwnerId = -1, Pos = message.Pos, Profession = workstation.Type });
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

                    if (village.Workstations.Remove(message.Pos))
                    {
                        Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(message.Pos)?.RemoveVillage();
                    }

                    if (village.Gatherplaces.Remove(message.Pos))
                    {
                        Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBrazier>(message.Pos)?.RemoveVillage();
                    }

                    if (village.Beds.Remove(message.Pos))
                    {
                        Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBed>(message.Pos)?.RemoveVillage();
                    }

                    break;
                case EnumVillageManagementOperation.removeVillager:
                    village = GetVillage(message.Id);

                    if (village.VillagerSaveData.Remove(message.VillagerToRemove))
                    {
                        Api.World.GetEntityById(message.VillagerToRemove)?.GetBehavior<EntityBehaviorVillager>()?.RemoveVillage();
                    }

                    break;
            }
        }
    }
}