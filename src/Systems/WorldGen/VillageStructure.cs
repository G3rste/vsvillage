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
        public int AttachmentPoint = 0;// 0=NORTH 1=EAST 2=SOUTH 3=WEST 
        [JsonProperty]
        public int VerticalOffset = 0;
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

        public Vec2i gridCoords;

        public void Init(ICoreServerAPI api)
        {
            var asset = api.Assets.Get(new AssetLocation("game", "worldgen/schematics/overground/village/" + SchematicCode + ".json"));
            var schematic = asset?.ToObject<BlockSchematicStructure>();
            if (schematic == null)
            {
                api.World.Logger.Warning("Could not load VillageStruce {0}", SchematicCode);
                return;
            }
            Schematics = new BlockSchematicStructure[4];
            Schematics[0] = schematic;
            Schematics[0].FromFileName = asset.Name;
            Schematics[0].Init(api.World.BlockAccessor);
            Schematics[0].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, 90 * (4 - AttachmentPoint));
            Schematics[0].LoadMetaInformationAndValidate(api.World.BlockAccessor, api.World, schematic.FromFileName);
            api.World.Logger.Debug(Schematics[0].FromFileName);
            for (int k = 1; k < 4; k++)
            {
                Schematics[k] = Schematics[0].Clone();
                Schematics[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90);
                Schematics[k].Init(api.World.BlockAccessor);
                Schematics[k].LoadMetaInformationAndValidate(api.World.BlockAccessor, api.World, schematic.FromFileName);
            }

        }

        public void Generate(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos, int neededBlockFacing)
        {
            //Schematics[neededBlockFacing].PlaceReplacingBlocks(blockAccessor, worldForCollectibleResolve, pos, EnumReplaceMode.ReplaceAllNoAir, new Dictionary<int, Dictionary<int, int>>());
            Schematics[neededBlockFacing].Place(blockAccessor, worldForCollectibleResolve, pos, EnumReplaceMode.ReplaceAllNoAir);
            blockAccessor.Commit();
            Schematics[neededBlockFacing].PlaceEntitiesAndBlockEntities(blockAccessor, worldForCollectibleResolve, pos);
            //blockAccessor.Commit();        
            /*
            for (int i = 0; i < Schematics[neededBlockFacing].Indices.Count; i++)
            {
                uint index = Schematics[neededBlockFacing].Indices[i];
                int storedBlockid = Schematics[neededBlockFacing].BlockIds[i];

                int dx = (int)(index & 0x1ff);
                int dy = (int)((index >> 20) & 0x1ff);
                int dz = (int)((index >> 10) & 0x1ff);

                Block block = blockAccessor.GetBlock(Schematics[neededBlockFacing].BlockCodes[storedBlockid]);
                if (block == null) { worldForCollectibleResolve.Logger.Debug(Schematics[neededBlockFacing].BlockCodes[storedBlockid].Domain); continue;}
                blockAccessor.SetBlock(block.Id, pos.AddCopy(dx, dy, dz));

                Schematics[neededBlockFacing].blocksByPos[dx, dy, dz] = block;
            }
            for (int x = 0; x < Schematics[neededBlockFacing].blocksByPos.GetLength(0); x++)
                for (int y = 0; y < Schematics[neededBlockFacing].blocksByPos.GetLength(1); y++)
                    for (int z = 0; z < Schematics[neededBlockFacing].blocksByPos.GetLength(2); z++)
                        if (Schematics[neededBlockFacing].blocksByPos[x, y, z] != null)
                        {
                            worldForCollectibleResolve.Logger.Debug("Placed block " + Schematics[neededBlockFacing].blocksByPos[x, y, z].Id);
                            blockAccessor.SetBlock(Schematics[neededBlockFacing].blocksByPos[x, y, z].Id, pos.AddCopy(x, y, z));
                        }
            foreach (var entry in Schematics[neededBlockFacing].BlockCodes.Values)
            {
                worldForCollectibleResolve.Logger.Debug(entry.Path);
            }*/
        }
    }

    public enum EnumVillageStructureSize
    {
        SMALL, // 7x7
        MEDIUM, // 17x17
        LARGE // 37x37
    }
}