using System.Collections.Concurrent;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using System.Linq;
using System;

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
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public Village GetVillage(string id)
        {
            Village villageData;
            if (!Villages.TryGetValue(id, out villageData))
            {
                if (Api is ICoreServerAPI sapi)
                {
                    byte[] data = sapi.WorldManager.SaveGame.GetData(id);
                    villageData = data == null ? null : SerializerUtil.Deserialize<Village>(data);
                    villageData?.Init(sapi);
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
                Villages.Remove(id);
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.None)]
    public class Village
    {
        public string Id => "village-" + Pos.ToString();
        [ProtoMember(0)]
        public BlockPos Pos;
        [ProtoMember(1)]
        public int Radius;
        [ProtoMember(2)]
        public string Name;
        [ProtoMember(3)]
        public List<VillagerPOI> Beds = new();
        [ProtoMember(4)]
        public List<VillagerPOI> Workstations = new();
        [ProtoMember(5)]
        public List<BlockPos> Gatherplaces = new();
        [ProtoMember(6)]
        public List<long> VillagerIds = new();

        public ICoreAPI Api;

        public void Init(ICoreAPI api)
        {
            Api = api;
        }

        public BlockPos FindFreeBed(long villagerId)
        {
            foreach (var bed in Beds)
            {
                if (bed.OwnerId == -1 || bed.OwnerId == villagerId)
                {
                    bed.OwnerId = villagerId;
                    return bed.Pos;
                }
            }
            return null;
        }

        public BlockPos FindFreeWorkstation(long villagerId)
        {
            foreach (var workstation in Workstations)
            {
                if (workstation.OwnerId == -1 || workstation.OwnerId == villagerId)
                {
                    workstation.OwnerId = villagerId;
                    return workstation.Pos;
                }
            }
            return null;
        }

        public BlockPos FindRandomGatherplace()
        {
            if (Gatherplaces.Count == 0)
            {
                return null;
            }
            return Gatherplaces[Api.World.Rand.Next(Gatherplaces.Count)];
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VillagerPOI
    {
        public BlockPos Pos;
        public long OwnerId = -1;
    }
}