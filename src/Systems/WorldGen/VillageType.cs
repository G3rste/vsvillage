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
        
        [JsonProperty]
        public int MinTemp = -30;
        [JsonProperty]
        public int MaxTemp = 40;
        [JsonProperty]
        public float MinRain = 0;
        [JsonProperty]
        public float MaxRain = 1;
        [JsonProperty]
        public float MinForest = 0;
        [JsonProperty]
        public float MaxForest = 1;
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