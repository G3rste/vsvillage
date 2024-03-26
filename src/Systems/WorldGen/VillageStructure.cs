using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;
using Newtonsoft.Json;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using System.Collections.Generic;

namespace VsVillage
{
    public class WorldGenVillageStructure
    {
        [JsonProperty]
        public string Code;
        [JsonProperty]
        public string Group;
        [JsonProperty]
        public int AttachmentPoint = 0;// 0=NORTH 1=EAST 2=SOUTH 3=WEST 
        [JsonProperty]
        public int VerticalOffset = 0;
        [JsonProperty]
        public EnumVillageStructureSize Size = EnumVillageStructureSize.SMALL;

        public BlockSchematicStructure[] Schematics;

        public void Init(ICoreServerAPI api)
        {
            var asset = api.Assets.Get(new AssetLocation("vsvillage", "worldgen/schematics/vsvillage/" + Code + ".json"));
            var schematic = asset?.ToObject<BlockSchematicStructure>();
            if (schematic == null)
            {
                api.World.Logger.Warning("Could not load VillageStruce {0}", Code);
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
                Schematics[k] = schematic.ClonePacked() as BlockSchematicStructure;
                Schematics[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90);
                Schematics[k].Init(api.World.BlockAccessor);
                Schematics[k].LoadMetaInformationAndValidate(api.World.BlockAccessor, api.World, schematic.FromFileName);
            }

            // while in the VS World +z coordinate equals south, in the grid worl it equals north, so we have to invert here
            Schematics[0].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, 180);
            Schematics[2].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, 180);
        }

        public void Generate(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos, int orientation)
        {
            Schematics[orientation].PlaceReplacingBlocks(blockAccessor, worldForCollectibleResolve, pos, EnumReplaceMode.ReplaceAllNoAir, new Dictionary<int, Dictionary<int, int>>(), null);
            if (orientation % 2 == 0)
            {
                orientation = (orientation + 2) % 4; // has something to do with the rotation by 180° a couple lines earlier, needs to be done for some reason...
            }
            generateFoundation(blockAccessor, pos, Schematics[orientation], orientation);
        }

        private void generateFoundation(IBlockAccessor blockAccessor, BlockPos pos, BlockSchematicStructure schematic, int orientation)
        {
            BlockPos probePos = pos.DownCopy();
            int length = schematic.blocksByPos.GetLength(0);
            int width = schematic.blocksByPos.GetLength(2);
            while (probe(blockAccessor, probePos, length, width))
            {
                for (int i = 0; i < length; i++)
                {
                    for (int k = 0; k < width; k++)
                    {
                        Block block = schematic.blocksByPos[i, 0, k];
                        if (block != null)
                        {
                            blockAccessor.SetBlock(block.Id, probePos.AddCopy(i, 0, k));
                        }
                    }
                }
                probePos.Down();
            }
        }

        private bool probe(IBlockAccessor blockAccessor, BlockPos pos, int length, int width)
        {
            BlockPos[] probePositions = new BlockPos[] { pos.Copy(), pos.AddCopy(length, 0, 0), pos.AddCopy(0, 0, width), pos.AddCopy(length, 0, width), pos.AddCopy(length / 2, 0, 0), pos.AddCopy(0, 0, width / 2), pos.AddCopy(length / 2, 0, width), pos.AddCopy(length, 0, width / 2) };
            for (int i = 0; i < probePositions.Length; i++)
            {
                if (blockAccessor.GetBlock(probePositions[i], BlockLayersAccess.Solid).Id == 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public enum EnumVillageStructureSize
    {
        SMALL, // 7x7
        MEDIUM, // 17x17
        LARGE // 37x37
    }
}