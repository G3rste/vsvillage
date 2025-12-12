using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VsVillage
{
    public class VillageType
    {
        [JsonProperty]
        public string Code;
        [JsonProperty]
        public string Names;

        [JsonProperty]
        public List<StructureGroup> StructureGroups = new List<StructureGroup>();
        [JsonProperty]
        public string StreetCode = "game:packeddirt";
        [JsonProperty]
        public string BridgeCode = "game:planks-aged-ns";

        [JsonProperty]
        public int Height = 2;
        [JsonProperty]
        public int Length = 2;

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

    public class StructureGroup
    {
        [JsonProperty]
        public string Code;
        [JsonProperty]
        public EnumVillageStructureSize Size;
        [JsonProperty]
        public int MinStructuresPerVillage;

        [JsonProperty]
        public int MaxStructuresPerVillage;

        public List<WorldGenVillageStructure> MatchingStructures = new List<WorldGenVillageStructure>();
    }
}