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
        public int AttachmentPoint = 0;// 0=NORTH 1=EAST 2=SOUTH 3=WEST 
        [JsonProperty]
        public int VerticalOffset = 0;
        [JsonProperty]
        public EnumVillageStructureSize Size = EnumVillageStructureSize.SMALL;

        public BlockSchematicStructure[] Schematics;

        public void Init(ICoreServerAPI api)
        {
            var asset = api.Assets.Get(new AssetLocation("game", "worldgen/schematics/vsvillage/" + SchematicCode + ".json"));
            var schematic = asset?.ToObject<BlockSchematicStructure>();
            if (schematic == null)
            {
                api.World.Logger.Warning("Could not load VillageStruce {0}", SchematicCode);
                return;
            }
            Schematics = new BlockSchematicStructure[4];
            schematic.FromFileName = asset.Name;
            schematic.Init(api.World.BlockAccessor);
            schematic.TransformWhilePacked(api.World, EnumOrigin.BottomCenter, 90 * (4 - AttachmentPoint));
            schematic.LoadMetaInformationAndValidate(api.World.BlockAccessor, api.World, schematic.FromFileName);
            Schematics[0] = schematic;
            for (int k = 1; k < 4; k++)
            {
                Schematics[k] = schematic.Clone();
                Schematics[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90);
                Schematics[k].Init(api.World.BlockAccessor);
                Schematics[k].LoadMetaInformationAndValidate(api.World.BlockAccessor, api.World, schematic.FromFileName);
            }

            // while in the VS World +z coordinate equals south, in the grid worl it equals north, so we have to invert here
            Schematics[0].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, 180);
            Schematics[2].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, 180);
        }

        public void Generate(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos, int neededBlockFacing)
        {
            Schematics[neededBlockFacing].Place(blockAccessor, worldForCollectibleResolve, pos, EnumReplaceMode.ReplaceAllNoAir);
            blockAccessor.Commit();
            Schematics[neededBlockFacing].PlaceEntitiesAndBlockEntities(blockAccessor, worldForCollectibleResolve, pos);
        }
    }

    public enum EnumVillageStructureSize
    {
        SMALL, // 7x7
        MEDIUM, // 17x17
        LARGE // 37x37
    }
}