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
        public List<StructureGroup> StructureGroups = new List<StructureGroup>();

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

        public VillageGrid genVillageGrid(Random rand){
            var grid = new VillageGrid(Length, Height);
            foreach(var group in StructureGroups){
                int amount = rand.Next(group.MinStructuresPerVillage, group.MaxStructuresPerVillage + 1);
                for(int i = 0; i< amount; i++){
                    grid.tryAddStructure(group.MatchingStructures[rand.Next(0, group.MatchingStructures.Count)], rand);
                }
            }
            return grid;
        }
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