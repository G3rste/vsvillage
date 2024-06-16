using Vintagestory.API.MathTools;
using ProtoBuf;

namespace VsVillage
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VillageManagementMessage
    {
        public EnumVillageManagementOperation Operation;
        public string Id;
        public string Name;
        public int Radius;
        public BlockPos Pos;

        public long VillagerToRemove;
        public BlockPos StructureToRemove;
        public EnumVillagerProfession VillagerProfession;
        public string VillagerType;
    }
}