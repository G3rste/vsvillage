using System.Collections.Generic;
using Newtonsoft.Json;

namespace VsVillage
{
    public class VillageType
    {
        [JsonProperty]
        public string Code;

        [JsonProperty]
        public List<StructureGroup> StructureGroups;
    }

    public class StructureGroup{
        [JsonProperty]
        public string Code;
        [JsonProperty]
        public int MinStructuresPerVillage;

        [JsonProperty]
        public int MaxStructuresPerVillage;
    }
}