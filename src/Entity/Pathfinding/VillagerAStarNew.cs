using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Essentials;

namespace VsVillage
{
    public class VillagerAStarNew
    {
        public List<string> traversableCodes = ["door", "gate", "ladder", "multiblock"];
        public List<string> doorCodes = ["door", "gate", "multiblock"];
        public List<string> climbableCodes = ["ladder"];
        public List<string> steppableCodes = ["stair", "path", "bed-", "farmland", "slab"];
        public ICachingBlockAccessor blockAccessor;
        public const float stepHeight = 1.5f;
        public const int maxFallHeight = 5;

        public VillagerAStarNew(ICachingBlockAccessor blockAccessor)
        {
            this.blockAccessor = blockAccessor;
        }

        public List<VillagerPathNode> FindPath(BlockPos start, BlockPos end, int searchDepth = 1000)
        {
            if (start == null || end == null) return null;
            var reachableNodes = new SortedSet<VillagerPathNode>([new VillagerPathNode(start, end, isDoor(blockAccessor.GetBlock(start)))]);
            var visitedNodes = new HashSet<VillagerPathNode>();
            for (int i = 0; i < searchDepth && reachableNodes.Count > 0; i++)
            {
                var currentNode = reachableNodes.Min;
                if (currentNode.BlockPos.Equals(end))
                {
                    return currentNode.RetracePath();
                }
                reachableNodes.Remove(currentNode);
                visitedNodes.Add(currentNode);

                findValidNeighbourNodes(currentNode)
                    .Where(node => traversable(node, end) && !visitedNodes.Contains(node))
                    .Foreach(node => reachableNodes.Add(node));
            }
            return null;
        }

        private IEnumerable<VillagerPathNode> findValidNeighbourNodes(VillagerPathNode nearestNode)
        {
            Block current = blockAccessor.GetBlock(nearestNode.BlockPos);
            VillagerPathNode[] neighbours = [new VillagerPathNode(nearestNode, Cardinal.North), new VillagerPathNode(nearestNode, Cardinal.East), new VillagerPathNode(nearestNode, Cardinal.South), new VillagerPathNode(nearestNode, Cardinal.West)];
            if (climbableCodes.Exists(current.Code.Path.Contains))
            {
                var climbableNode = current.Variant["side"] switch
                {
                    "north" => neighbours[0],
                    "east" => neighbours[1],
                    "south" => neighbours[2],
                    _ => neighbours[3]
                };
                int i = 1;
                while (traversableCodes.Exists(code => blockAccessor.GetBlock(nearestNode.BlockPos.UpCopy(i)).Code.Path.Contains(code)))
                {
                    i++;
                }
                climbableNode.BlockPos.Y += i;
            }

            return neighbours;
        }

        protected virtual bool traversable(VillagerPathNode node, BlockPos target)
        {
            if (target.Equals(node.BlockPos)) { return true; }
            if (traversable(blockAccessor.GetBlock(node.BlockPos))
                && traversable(blockAccessor.GetBlock(node.BlockPos.UpCopy())))
            {
                for (int fallHeightLeft = maxFallHeight; 0 <= fallHeightLeft; fallHeightLeft--)
                {
                    Block belowBlock = blockAccessor.GetBlock(node.BlockPos.DownCopy());
                    if (canStep(belowBlock))
                    {
                        node.Init(target, isDoor(blockAccessor.GetBlock(node.BlockPos)));
                        return true;
                    }
                    if (!traversable(belowBlock)) { return false; }
                    ;
                    node.BlockPos.Y--;
                }
                while (climbableCodes.Exists(code => blockAccessor.GetBlock(node.BlockPos).Code.Path.Contains(code)))
                {
                    Block belowBlock = blockAccessor.GetBlock(node.BlockPos.DownCopy());
                    if (canStep(belowBlock))
                    {
                        node.Init(target, isDoor(blockAccessor.GetBlock(node.BlockPos)));
                        return true;
                    }
                    node.BlockPos.Y--;
                }
            }
            else
            {
                for (float stepHeightLeft = stepHeight; 1f < stepHeightLeft; stepHeightLeft--)
                {
                    node.BlockPos.Y++;
                    if (canStep(blockAccessor.GetBlock(node.BlockPos.DownCopy())) && traversable(blockAccessor.GetBlock(node.BlockPos)) && traversable(blockAccessor.GetBlock(node.BlockPos.UpCopy())))
                    {
                        node.Init(target, isDoor(blockAccessor.GetBlock(node.BlockPos)));
                        return true;
                    }
                }
            }
            return false;
        }

        protected virtual bool canStep(Block belowBlock)
        {
            return belowBlock.SideSolid[BlockFacing.UP.Index] || steppableCodes.Exists(belowBlock.Code.Path.Contains);
        }

        private bool traversable(Block block)
        {
            return block.CollisionBoxes == null || block.CollisionBoxes.Length == 0 || traversableCodes.Exists(block.Code.Path.Contains);
        }

        private bool isDoor(Block block)
        {
            return doorCodes.Exists(block.Code.Path.Contains);
        }

        public BlockPos GetStartPos(Vec3d startPos)
        {
            var result = startPos.AsBlockPos;
            var startBlock = blockAccessor.GetBlock(result);
            if (traversable(startBlock))
            {
                return result;
            }

            if (getDecimalPart(startPos.Z) < 0.5 && traversable(blockAccessor.GetBlock(result.NorthCopy())))
            {
                return result.NorthCopy();
            }

            if (getDecimalPart(startPos.Z) > 0.5 && traversable(blockAccessor.GetBlock(result.SouthCopy())))
            {
                return result.SouthCopy();
            }

            if (getDecimalPart(startPos.X) < 0.5 && traversable(blockAccessor.GetBlock(result.West())))
            {
                return result;
            }

            if (getDecimalPart(startPos.X) > 0.5 && traversable(blockAccessor.GetBlock(result.East())))
            {
                return result;
            }

            if (getDecimalPart(startPos.Z) < 0.5 && traversable(blockAccessor.GetBlock(result.NorthCopy())))
            {
                return result.NorthCopy();
            }

            if (getDecimalPart(startPos.Z) > 0.5 && traversable(blockAccessor.GetBlock(result.SouthCopy())))
            {
                return result.SouthCopy();
            }

            return startPos.AsBlockPos;
        }

        private double getDecimalPart(double number)
        {
            return number - Math.Truncate(number);
        }
    }
}