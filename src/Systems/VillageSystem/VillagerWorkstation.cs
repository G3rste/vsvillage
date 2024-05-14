using ProtoBuf;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VillagerWorkstation
    {
        public BlockPos Pos;
        public long OwnerId = -1;
        public EnumVillagerProfession Profession;
    }
}