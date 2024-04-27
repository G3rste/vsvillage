using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public class VillageGrid
    {
        public const int pathWidth = 3;
        public const int squareSize = 7;

        public EnumgGridSlot[,] grid;

        public List<StructureWithOrientation> structures = new List<StructureWithOrientation>();

        public int capacity;

        public readonly int width;
        public readonly int height;

        public int avgheight;
        public VillageType VillageType;

        public VillageGrid(int width = 1, int height = 1)
        {
            this.width = width * 8 + 1;
            this.height = height * 8 + 1;
            this.capacity = (this.width / 2) * (this.height / 2);
        }

        public void Init(VillageType type, LCGRandom rand)
        {
            grid = new EnumgGridSlot[this.width, this.height];
            for (int i = 0; i < this.width; i++)
            {
                for (int k = 0; k < this.height; k++)
                {
                    grid[i, k] = EnumgGridSlot.EMPTY;
                }
            }

            VillageType = type;
            foreach (var group in type.StructureGroups)
            {
                int amount = rand.NextInt(group.MaxStructuresPerVillage + 1 - group.MinStructuresPerVillage) + group.MinStructuresPerVillage;
                for (int i = 0; i < amount; i++)
                {
                    tryAddStructure(group.MatchingStructures[rand.NextInt(group.MatchingStructures.Count)], rand);
                }
            }
        }

        public bool BigSlotAvailable(int x, int y)
        {
            return grid[x * 8 + 1, y * 8 + 1] == EnumgGridSlot.EMPTY;
        }

        public bool MediumSlotAvailable(int x, int y)
        {
            return grid[x * 4 + 1, y * 4 + 1] == EnumgGridSlot.EMPTY;
        }

        public bool SmallSlotAvailable(int x, int y)
        {
            return grid[x * 2 + 1, y * 2 + 1] == EnumgGridSlot.EMPTY;
        }

        public void AddBigStructure(WorldGenVillageStructure structure, int x, int y, int orientation)
        {
            capacity -= 16;
            structures.Add(new StructureWithOrientation()
            {
                structure = structure,
                orientation = orientation,
                gridCoords = new Vec2i(x * 8 + 1, y * 8 + 1)
            });
            for (int i = 0; i < 7; i++)
            {
                for (int k = 0; k < 7; k++)
                {
                    grid[x * 8 + 1 + i, y * 8 + 1 + k] = EnumgGridSlot.STRUCTURE;
                }
            }
            switch (orientation)
            {
                case 0:
                    grid[x * 8 + 4, y * 8 + 8] = EnumgGridSlot.STREET;
                    break;
                case 1:
                    grid[x * 8 + 8, y * 8 + 4] = EnumgGridSlot.STREET;
                    break;
                case 2:
                    grid[x * 8 + 4, y * 8] = EnumgGridSlot.STREET;
                    break;
                case 3:
                    grid[x * 8, y * 8 + 4] = EnumgGridSlot.STREET;
                    break;
            }
        }

        public void AddMediumStructure(WorldGenVillageStructure structure, int x, int y, int orientation)
        {
            capacity -= 4;
            structures.Add(new StructureWithOrientation()
            {
                structure = structure,
                orientation = orientation,
                gridCoords = new Vec2i(x * 4 + 1, y * 4 + 1)
            });
            for (int i = 0; i < 3; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    grid[x * 4 + 1 + i, y * 4 + 1 + k] = EnumgGridSlot.STRUCTURE;
                }
            }
            switch (orientation)
            {
                case 0:
                    grid[x * 4 + 2, y * 4 + 4] = EnumgGridSlot.STREET;
                    break;
                case 1:
                    grid[x * 4 + 4, y * 4 + 2] = EnumgGridSlot.STREET;
                    break;
                case 2:
                    grid[x * 4 + 2, y * 4] = EnumgGridSlot.STREET;
                    break;
                case 3:
                    grid[x * 4, y * 4 + 2] = EnumgGridSlot.STREET;
                    break;
            }
        }

        public void AddSmallStructure(WorldGenVillageStructure structure, int x, int y, int orientation)
        {
            capacity -= 1;
            structures.Add(new StructureWithOrientation()
            {
                structure = structure,
                orientation = orientation,
                gridCoords = new Vec2i(x * 2 + 1, y * 2 + 1)
            });
            grid[x * 2 + 1, y * 2 + 1] = EnumgGridSlot.STRUCTURE;
            switch (orientation)
            {
                case 0:
                    grid[x * 2 + 1, y * 2 + 2] = EnumgGridSlot.STREET;
                    break;
                case 1:
                    grid[x * 2 + 2, y * 2 + 1] = EnumgGridSlot.STREET;
                    break;
                case 2:
                    grid[x * 2 + 1, y * 2] = EnumgGridSlot.STREET;
                    break;
                case 3:
                    grid[x * 2, y * 2 + 1] = EnumgGridSlot.STREET;
                    break;
            }
        }

        //always go from biggest to smallest structure, otherwise this might break
        public bool tryAddStructure(WorldGenVillageStructure structure, LCGRandom random)
        {
            int orientation = random.NextInt(4);
            switch (structure.Size)
            {
                case EnumVillageStructureSize.LARGE:
                    if (capacity < 16) { return false; }
                    else
                    {
                        var free = new List<Vec2i>();
                        for (int i = 0; i < width / 8; i++)
                        {
                            for (int k = 0; k < height / 8; k++)
                            {
                                if (BigSlotAvailable(i, k))
                                {
                                    free.Add(new Vec2i(i, k));
                                }
                            }
                        }
                        var xy = free[random.NextInt(free.Count)];
                        AddBigStructure(structure, xy.X, xy.Y, orientation);
                        return true;
                    }
                case EnumVillageStructureSize.MEDIUM:
                    if (capacity < 4) { return false; }
                    else
                    {
                        var free = new List<Vec2i>();
                        for (int i = 0; i < width / 4; i++)
                        {
                            for (int k = 0; k < height / 4; k++)
                            {
                                if (MediumSlotAvailable(i, k))
                                {
                                    free.Add(new Vec2i(i, k));
                                }
                            }
                        }
                        var xy = free[random.NextInt(free.Count)];
                        AddMediumStructure(structure, xy.X, xy.Y, orientation);
                        return true;
                    }
                case EnumVillageStructureSize.SMALL:
                    if (capacity < 1) { return false; }
                    else
                    {
                        var free = new List<Vec2i>();
                        for (int i = 0; i < width / 2; i++)
                        {
                            for (int k = 0; k < height / 2; k++)
                            {
                                if (SmallSlotAvailable(i, k))
                                {
                                    free.Add(new Vec2i(i, k));
                                }
                            }
                        }
                        var xy = free[random.NextInt(free.Count)];
                        AddSmallStructure(structure, xy.X, xy.Y, orientation);
                        return true;
                    }
                default: return false;

            }
        }

        public BlockPos getEnd(BlockPos start)
        {
            var end = GridCoordsToMapCoords(width, height);
            return start.AddCopy(end.X + 3, 20, end.Y + 3);
        }

        public BlockPos getMiddle(BlockPos start)
        {
            var end = GridCoordsToMapCoords(width, height);
            return start.AddCopy((end.X + 3) / 2, 20, (end.Y + 3) / 2);
        }

        public void connectStreets()
        {
            var connectedStreets = new List<Vec2i>();
            int currentX = 0;
            int currentY = 0;
            int offsetX = 0;
            int offsetY = 0;
            bool rightEdge = false;
            for (int i = 0; i < width * height; i++)
            {
                if (grid[currentX, currentY] == EnumgGridSlot.STREET)
                {
                    addStreedToGrid(connectedStreets, new Vec2i(currentX, currentY));
                }
                currentX--;
                currentY++;
                if (currentX < 0 || currentY >= height)
                {
                    offsetX = rightEdge ? width - 1 : offsetX + 1;
                    offsetY = rightEdge ? offsetY + 1 : offsetY;
                    rightEdge |= offsetX >= width - 1;
                    currentX = offsetX;
                    currentY = offsetY;
                }
            }
        }

        private void addStreedToGrid(List<Vec2i> connectedStreets, Vec2i newStreet)
        {
            if (connectedStreets.Count == 0)
            {
                connectedStreets.Add(newStreet);
            }
            else
            {
                // get closest street
                var nearest = connectedStreets[0];
                var nearestDistance = Math.Abs(newStreet.X - nearest.X) + Math.Abs(newStreet.Y - nearest.Y);
                foreach (var candidate in connectedStreets)
                {
                    var candidateDistance = Math.Abs(newStreet.X - candidate.X) + Math.Abs(newStreet.Y - candidate.Y);
                    if (candidateDistance < nearestDistance)
                    {
                        nearest = candidate;
                        nearestDistance = candidateDistance;
                    }
                }
                // conntect streets
                int currentX = nearest.X;
                int currentY = nearest.Y;
                bool canWalkY;
                bool canWalkX;
                bool canWalkTowards = true;
                int directionX = Math.Sign(newStreet.X - currentX + 0.5f);
                int directionY = Math.Sign(newStreet.Y - currentY + 0.5f);
                bool? goHorizontal = null;
                while (Math.Abs(newStreet.X - currentX) + Math.Abs(newStreet.Y - currentY) > 1)
                {
                    canWalkX = currentY % 2 == 0 && inWidthBounds(currentX + directionX) && grid[currentX + directionX, currentY] != EnumgGridSlot.STRUCTURE;
                    canWalkY = currentX % 2 == 0 && inHeightBounds(currentY + directionY) && grid[currentX, currentY + directionY] != EnumgGridSlot.STRUCTURE;
                    canWalkTowards &= canWalkX && (newStreet.X - currentX) * directionX > 0 || canWalkY && (newStreet.Y - currentY) * directionY > 0;
                    if (!canWalkTowards)
                    {
                        if (goHorizontal == null)
                        {
                            goHorizontal = canWalkX;
                        }

                        if (canWalkY && goHorizontal == true || canWalkX && goHorizontal == false)
                        {
                            if (goHorizontal == true) { currentY += directionY; }
                            else { currentX += directionX; }
                            goHorizontal = null;
                            canWalkTowards = true;
                            directionX = Math.Sign(newStreet.X - currentX + 0.5f);
                            directionY = Math.Sign(newStreet.Y - currentY + 0.5f);
                        }
                        else if (goHorizontal == true) { currentX += directionX; }
                        else { currentY += directionY; }
                    }
                    else if (canWalkX && Math.Abs(newStreet.X - currentX) >= Math.Abs(newStreet.Y - currentY) || !canWalkY)
                    {
                        currentX += directionX;
                    }
                    else if (canWalkY)
                    {
                        currentY += directionY;
                    }
                    grid[currentX, currentY] = EnumgGridSlot.STREET;
                    connectedStreets.Add(new Vec2i(currentX, currentY));
                }
            }
        }

        public string debugPrintGrid()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < height; i++)
            {
                for (int k = 0; k < width; k++)
                {
                    sb.Append((int)grid[k, height - 1 - i]).Append(" ");
                }
                sb.Append("\n");
            }
            return sb.ToString();
        }

        public Vec2i GridCoordsToMapCoords(int x, int y)
        {
            return new Vec2i(GridDistToMapDist(x), GridDistToMapDist(y));
        }

        public static int GridDistToMapDist(int x)
        {
            return x * pathWidth + (x / 2) * (squareSize - pathWidth);
        }

        public Vec2i GridCoordsToMapSize(int x, int y)
        {
            return new Vec2i(x % 2 == 0 ? pathWidth : squareSize, y % 2 == 0 ? pathWidth : squareSize);
        }

        public void GenerateStreets(BlockPos start, IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve)
        {
            int idpath = worldForCollectibleResolve.GetBlock(new AssetLocation(VillageType.StreetCode)).Id;
            int idbridge = worldForCollectibleResolve.GetBlock(new AssetLocation(VillageType.BridgeCode)).Id;
            for (int i = 0; i < width; i++)
            {
                for (int k = 0; k < height; k++)
                {
                    if (grid[i, k] == EnumgGridSlot.STREET)
                    {
                        GenerateStreetPart(start, i, k, blockAccessor, idpath, idbridge, i % 4 + k % 4 == 0);
                    }
                }
            }
        }

        private void GenerateStreetPart(BlockPos start, int x, int z, IBlockAccessor blockAccessor, int idpath, int idbridge, bool generateWaypoint)
        {
            var coords = GridCoordsToMapCoords(x, z);
            var size = GridCoordsToMapSize(x, z);
            for (int i = 0; i < size.X; i++)
            {
                for (int k = 0; k < size.Y; k++)
                {
                    var pos = start.AddCopy(coords.X + i, 0, coords.Y + k);
                    int terrainheight = blockAccessor.GetTerrainMapheightAt(pos);
                    int rainheight = blockAccessor.GetRainMapHeightAt(pos);
                    int id = idbridge;
                    pos.Y = rainheight;
                    if (terrainheight >= rainheight || blockAccessor.GetBlock(pos, BlockLayersAccess.Fluid).Id == 0)
                    {
                        pos.Y = terrainheight;
                        id = idpath;
                    }
                    blockAccessor.SetBlock(id, pos);
                    blockAccessor.SetBlock(0, pos.Add(0, 1, 0)); // can probably be removed when hooked properly into world gen
                    blockAccessor.SetBlock(0, pos.Add(0, 1, 0)); // can probably be removed when hooked properly into world gen
                    if (generateWaypoint && i == 0 && k == 0)
                    {
                        blockAccessor.SetBlock(blockAccessor.GetBlock(new AssetLocation("vsvillage:waypoint")).Id, pos.Add(0, -1, 0));
                    }
                }
            }
        }

        public void GenerateHouses(BlockPos start, IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve)
        {
            foreach (var house in structures)
            {
                GenerateHouse(house, start, blockAccessor, worldForCollectibleResolve);
            }
        }

        private void GenerateHouse(StructureWithOrientation house, BlockPos start, IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve)
        {

            var coords = GridCoordsToMapCoords(house.gridCoords.X, house.gridCoords.Y);
            var pos = start.AddCopy(coords.X, 0, coords.Y);
            var offsetForHeight = connectingPathOffset(house);
            var posForHeightDetection = pos.AddCopy(offsetForHeight.X, 0, offsetForHeight.Y);
            posForHeightDetection.Y = blockAccessor.GetTerrainMapheightAt(posForHeightDetection);
            while (blockAccessor.GetBlock(posForHeightDetection.UpCopy(), BlockLayersAccess.Fluid).Id != 0)
            {
                posForHeightDetection.Up();
            }
            pos.Y = posForHeightDetection.Y + house.structure.VerticalOffset;
            house.structure.Generate(blockAccessor, worldForCollectibleResolve, pos, house.orientation);
        }

        private Vec2i connectingPathOffset(StructureWithOrientation house)
        {
            var size = house.structure.Size;
            switch (house.orientation)
            {
                case 0: return new Vec2i(getSize(size) / 2, getSize(size));
                case 1: return new Vec2i(getSize(size), getSize(size) / 2);
                case 2: return new Vec2i(getSize(size) / 2, -1);
                case 3: return new Vec2i(-1, getSize(size) / 2);
                default: throw new ArgumentException("House has invalid orientation.");
            }
        }

        private int getSize(EnumVillageStructureSize size)
        {
            switch (size)
            {
                case EnumVillageStructureSize.SMALL: return squareSize;
                case EnumVillageStructureSize.MEDIUM: return squareSize + pathWidth + squareSize;
                case EnumVillageStructureSize.LARGE: return squareSize + pathWidth + squareSize + pathWidth + squareSize + pathWidth + squareSize;
                default: throw new ArgumentException("House has invalid size.");
            }
        }

        private bool inHeightBounds(int y)
        {
            return y >= 0 && y < height;
        }

        private bool inWidthBounds(int x)
        {
            return x >= 0 && x < width;
        }
    }

    public enum EnumgGridSlot
    {
        EMPTY, STRUCTURE, STREET
    }

    public class StructureWithOrientation
    {
        public WorldGenVillageStructure structure;
        public int orientation;

        public Vec2i gridCoords;
    }
}