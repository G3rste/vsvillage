using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public class VillageGrid
    {
        public EnumgGridSlot[][] grid;

        public List<WorldGenVillageStructure> structures = new List<WorldGenVillageStructure>();

        public int capacity = 16;

        public VillageGrid()
        {
            grid = new EnumgGridSlot[9][];
            for (int i = 0; i < 9; i++)
            {
                grid[i] = new EnumgGridSlot[] { EnumgGridSlot.EMPTY, EnumgGridSlot.EMPTY, EnumgGridSlot.EMPTY, EnumgGridSlot.EMPTY, EnumgGridSlot.EMPTY, EnumgGridSlot.EMPTY, EnumgGridSlot.EMPTY, EnumgGridSlot.EMPTY, EnumgGridSlot.EMPTY };
            }
        }

        public bool BigSlotAvailable()
        {
            return grid[0][0] == EnumgGridSlot.EMPTY;
        }

        public bool MediumSlotAvailable(int x, int y)
        {
            return grid[x * 4 + 1][y * 4 + 1] == EnumgGridSlot.EMPTY;
        }

        public bool SmallSlotAvailable(int x, int y)
        {
            return grid[x * 2 + 1][y * 2 + 1] == EnumgGridSlot.EMPTY;
        }

        public void AddBigStructure(WorldGenVillageStructure structure)
        {
            capacity -= 16;
            structures.Add(structure);
            for (int i = 1; i < 8; i++)
            {
                for (int k = 1; k < 8; k++)
                {
                    grid[i][k] = EnumgGridSlot.STRUCTURE;
                }
            }
            switch (structure.AttachmentPoint)
            {
                case 0:
                    grid[4][8] = EnumgGridSlot.STREET;
                    break;
                case 1:
                    grid[8][4] = EnumgGridSlot.STREET;
                    break;
                case 2:
                    grid[4][0] = EnumgGridSlot.STREET;
                    break;
                case 3:
                    grid[0][4] = EnumgGridSlot.STREET;
                    break;
            }
        }

        public void AddMediumStructure(WorldGenVillageStructure structure, int x, int y)
        {
            capacity -= 4;
            structures.Add(structure);
            for (int i = 0; i < 3; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    grid[x * 4 + 1 + i][y * 4 + 1 + k] = EnumgGridSlot.STRUCTURE;
                }
            }
            switch (structure.AttachmentPoint)
            {
                case 0:
                    grid[x * 4 + 2][y * 4 + 4] = EnumgGridSlot.STREET;
                    break;
                case 1:
                    grid[x * 4 + 4][y * 4 + 2] = EnumgGridSlot.STREET;
                    break;
                case 2:
                    grid[x * 4 + 2][y * 4] = EnumgGridSlot.STREET;
                    break;
                case 3:
                    grid[x * 4][y * 4 + 2] = EnumgGridSlot.STREET;
                    break;
            }
        }

        public void AddSmallStructure(WorldGenVillageStructure structure, int x, int y)
        {
            capacity -= 1;
            structures.Add(structure);
            grid[x * 2 + 1][y * 2 + 1] = EnumgGridSlot.STRUCTURE;
            switch (structure.AttachmentPoint)
            {
                case 0:
                    grid[x * 2 + 1][y * 2 + 2] = EnumgGridSlot.STREET;
                    break;
                case 1:
                    grid[x * 2 + 2][y * 2 + 1] = EnumgGridSlot.STREET;
                    break;
                case 2:
                    grid[x * 2 + 1][y * 2] = EnumgGridSlot.STREET;
                    break;
                case 3:
                    grid[x * 2][y * 2 + 1] = EnumgGridSlot.STREET;
                    break;
            }
        }

        //always go from biggest to smallest structure, otherwise this will break
        public bool tryAddStructure(WorldGenVillageStructure structure, Random random)
        {
            switch (structure.Size)
            {
                case EnumVillageStructureSize.LARGE:
                    if (capacity < 16) { return false; }
                    else { AddBigStructure(structure); return true; }
                case EnumVillageStructureSize.MEDIUM:
                    if (capacity < 4) { return false; }
                    else
                    {
                        var free = new List<Vec2i>();
                        for (int i = 0; i < 2; i++)
                        {
                            for (int k = 0; k < 2; k++)
                            {
                                if (MediumSlotAvailable(i, k))
                                {
                                    free.Add(new Vec2i(i, k));
                                }
                            }
                        }
                        var xy = free[random.Next(0, free.Count)];
                        AddMediumStructure(structure, xy.X, xy.Y);
                        return true;
                    }
                case EnumVillageStructureSize.SMALL:
                    if (capacity < 1) { return false; }
                    else
                    {
                        var free = new List<Vec2i>();
                        for (int i = 0; i < 4; i++)
                        {
                            for (int k = 0; k < 4; k++)
                            {
                                if (SmallSlotAvailable(i, k))
                                {
                                    free.Add(new Vec2i(i, k));
                                }
                            }
                        }
                        var xy = free[random.Next(0, free.Count)];
                        AddSmallStructure(structure, xy.X, xy.Y);
                        return true;
                    }
                default: return false;

            }
        }

        public string debugPrintGrid()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 9; i++)
            {
                for (int k = 0; k < 9; k++)
                {
                    sb.Append((int)grid[k][8 - i]).Append(" ");
                }
                sb.Append("\n");
            }
            return sb.ToString();
        }
    }

    public enum EnumgGridSlot
    {
        EMPTY, STRUCTURE, STREET
    }
}