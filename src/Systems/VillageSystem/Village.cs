using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;

namespace VsVillage
{
    [ProtoContract(ImplicitFields = ImplicitFields.None)]
    public class Village
    {
        public string Id => "village-" + Pos.ToString();
        [ProtoMember(1)]
        public BlockPos Pos;
        [ProtoMember(2)]
        public int Radius;
        [ProtoMember(3)]
        public string Name;
        [ProtoMember(4)]
        public List<VillagerBed> Beds = new();
        [ProtoMember(5)]
        public List<VillagerWorkstation> Workstations = new();
        [ProtoMember(6)]
        public List<BlockPos> Gatherplaces = new();
        [ProtoMember(7)]
        public List<VillagerData> VillagerSaveData = new();

        public ICoreAPI Api;
        public List<EntityVillager> Villagers => VillagerSaveData.ConvertAll(data => Api.World.GetEntityById(data.Id) as EntityVillager);

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

        public BlockPos FindFreeWorkstation(long villagerId, string profession)
        {
            foreach (var workstation in Workstations)
            {
                if (workstation.Profession == profession && (workstation.OwnerId == -1 || workstation.OwnerId == villagerId))
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
}