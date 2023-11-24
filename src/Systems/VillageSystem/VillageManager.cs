using System.Collections.Concurrent;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;

namespace VsVillage{
    public class VillageManager : ModSystem
    {

        private ConcurrentDictionary<long, VillageData> villages;
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            api.Event.SaveGameLoaded += OnLoad;
            api.Event.GameWorldSave += OnSave;
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public VillageData GetVillage(long id)
        {
            VillageData data = null;
            villages.TryGetValue(id, out data);
            return data;
        }

        private void OnSave()
        {
            sapi.WorldManager.SaveGame.StoreData("villagemanager", SerializerUtil.Serialize(villages));
        }

        private void OnLoad()
        {
            byte[] data = sapi.WorldManager.SaveGame.GetData("villagemanager");
            villages = data == null ? new ConcurrentDictionary<long, VillageData>() : SerializerUtil.Deserialize<ConcurrentDictionary<long, VillageData>>(data);
        }
        public void Remove(long id)
        {
            if (villages.ContainsKey(id))
            {
                villages.Remove(id);
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VillageData
    {
        public long Id;
        public BlockPos Pos;
        public string Name;
        public List<BlockPos> Beds;
        public List<BlockPos> Workstations;
        public List<BlockPos> Gatherplaces;
    }
}