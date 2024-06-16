using ProtoBuf;

namespace VsVillage
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VillagerData
    {
        public long Id;
        public string Name;
        public EnumVillagerProfession Profession;
    }
}