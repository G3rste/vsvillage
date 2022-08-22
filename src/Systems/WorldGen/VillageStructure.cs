using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;
using Newtonsoft.Json;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace VsVillage
{
    public class WorldGenVillageStructure
    {
        [JsonProperty]
        public string Code;
        [JsonProperty]
        public string StructureGroup;
        [JsonProperty]
        public string SchematicCode;
        [JsonProperty]
        public int AttachmentPoint = 0;// 0=NORTH 1=WEST 2=SOUTH 3=EAST
        [JsonProperty]
        public int VerticalOffset = -1;
        [JsonProperty]
        public EnumVillageStructureSize Size = EnumVillageStructureSize.SMALL;
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

        public BlockSchematicStructure[] Schematics;

        public void Init(ICoreServerAPI api)
        {
            var schematic = api.Assets.Get(new AssetLocation("vsvillage", "worldgen/schematics/" + SchematicCode))?.ToObject<BlockSchematicStructure>();
            if (schematic == null)
            {
                api.World.Logger.Warning("Could not load VillageStruce {0}", SchematicCode);
                return;
            }
            Schematics = new BlockSchematicStructure[4];
            for (int k = 0; k < 4; k++)
            {
                if (k > 0)
                {
                    Schematics[k] = Schematics[0].Clone();
                    Schematics[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90);
                }
                else
                {
                    Schematics[k] = schematic;
                    Schematics[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, AttachmentPoint * 90);
                }
                Schematics[k].Init(api.World.BlockAccessor);
                Schematics[k].LoadMetaInformationAndValidate(api.World.BlockAccessor, api.World, schematic.FromFileName);
            }

        }

        public void Generate(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos, int neededBlockFacing)
        {
            Schematics[neededBlockFacing].PlaceReplacingBlocks(blockAccessor, worldForCollectibleResolve, pos, EnumReplaceMode.ReplaceAllNoAir, new Dictionary<int, Dictionary<int, int>>());
        }
    }

    public enum EnumVillageStructureSize
    {
        SMALL, // 7x7
        MEDIUM, // 17x17
        LARGE // 37x37
    }
}