using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;

namespace VsVillage
{
    public class VillagerAStar
    {
        protected ICoreAPI api;
        protected ICachingBlockAccessor blockAccess;

        public List<string> traversableCodes { get; set; } = new List<string>() { "door", "gate", "ladder", "multiblock" };

        public List<string> climbableCodes { get; set; } = new List<string>() { "ladder" };
        public List<string> steppableCodes { get; set; } = new List<string>() { "stair", "path", "bed-", "farmland", "slab" };

        public int NodesChecked;

        public const double centerOffsetX = 0.5;
        public const double centerOffsetZ = 0.5;

        public VillagerAStar(ICoreAPI api)
        {
            this.api = api;
            blockAccess = api.World.GetCachingBlockAccessor(true, true);
        }

        public PathNodeSet openSet = new PathNodeSet();
        public HashSet<PathNode> closedSet = new HashSet<PathNode>();

        public List<PathNode> FindPath(BlockPos start, BlockPos end, int maxFallHeight, float stepHeight, int searchDepth = 999, bool allowReachAlmost = true)
        {
            blockAccess.Begin();

            NodesChecked = 0;

            PathNode startNode = new PathNode(start);
            PathNode targetNode = new PathNode(end);

            openSet.Clear();
            closedSet.Clear();

            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                if (NodesChecked++ > searchDepth) return null;

                PathNode nearestNode = openSet.RemoveNearest();
                closedSet.Add(nearestNode);

                if (nearestNode == targetNode || (allowReachAlmost && Math.Abs(nearestNode.X - targetNode.X) <= 1 && Math.Abs(nearestNode.Z - targetNode.Z) <= 1 && Math.Abs(nearestNode.Y - targetNode.Y) <= 2))
                {
                    return retracePath(startNode, nearestNode);
                }

                foreach (var neighbourNode in findValidNeighbourNodes(nearestNode, targetNode, stepHeight, maxFallHeight))
                {
                    float extraCost = 0;
                    PathNode existingNeighbourNode = openSet.TryFindValue(neighbourNode);
                    if (!(existingNeighbourNode is null))   // we have to do a null check using "is null" due to foibles in PathNode.Equals()
                    {
                        // if it is already in openSet, update the gCost and parent if this nearestNode gives a shorter route to it
                        float baseCostToNeighbour = nearestNode.gCost + nearestNode.distanceTo(neighbourNode);
                        if (existingNeighbourNode.gCost > baseCostToNeighbour + 0.0001f)
                        {
                            if (traversable(neighbourNode, targetNode, stepHeight, maxFallHeight) && existingNeighbourNode.gCost > baseCostToNeighbour + extraCost + 0.0001f)
                            {
                                UpdateNode(nearestNode, existingNeighbourNode, extraCost);
                            }
                        }
                    }
                    else if (!closedSet.Contains(neighbourNode))
                    {
                        if (traversable(neighbourNode, targetNode, stepHeight, maxFallHeight))
                        {
                            UpdateNode(nearestNode, neighbourNode, extraCost);
                            neighbourNode.hCost = neighbourNode.distanceTo(targetNode);
                            openSet.Add(neighbourNode);
                        }
                    }
                }
            }

            return null;
        }

        protected virtual IEnumerable<PathNode> findValidNeighbourNodes(PathNode nearestNode, PathNode targetNode, float stepHeight, int maxFallHeight)
        {
            Block current = blockAccess.GetBlock(new BlockPos(nearestNode.X, nearestNode.Y, nearestNode.Z, 0));
            if (climbableCodes.Exists(code => current.Code.Path.Contains(code)))
            {
                Cardinal climbableCard;
                List<PathNode> neighbourNodes;
                switch (current.Variant["side"])
                {
                    case "east":
                        climbableCard = Cardinal.East;
                        neighbourNodes = new List<PathNode>(new PathNode[] { new PathNode(nearestNode, Cardinal.North), new PathNode(nearestNode, Cardinal.South), new PathNode(nearestNode, Cardinal.West) });
                        break;
                    case "west":
                        climbableCard = Cardinal.West;
                        neighbourNodes = new List<PathNode>(new PathNode[] { new PathNode(nearestNode, Cardinal.North), new PathNode(nearestNode, Cardinal.East), new PathNode(nearestNode, Cardinal.South) });
                        break;
                    case "south":
                        climbableCard = Cardinal.South;
                        neighbourNodes = new List<PathNode>(new PathNode[] { new PathNode(nearestNode, Cardinal.North), new PathNode(nearestNode, Cardinal.East), new PathNode(nearestNode, Cardinal.West) });
                        break;
                    default:
                        climbableCard = Cardinal.North;
                        neighbourNodes = new List<PathNode>(new PathNode[] { new PathNode(nearestNode, Cardinal.East), new PathNode(nearestNode, Cardinal.South), new PathNode(nearestNode, Cardinal.West) });
                        break;
                }
                int i = 1;
                while (traversableCodes.Exists(code => blockAccess.GetBlock(new BlockPos(nearestNode.X, nearestNode.Y + i, nearestNode.Z, 0)).Code.Path.Contains(code)))
                {
                    i++;
                }
                var climbableNode = new PathNode(nearestNode, climbableCard);
                climbableNode.Y += i;
                neighbourNodes.Add(climbableNode);
                return neighbourNodes;
            }
            else
            {
                return new PathNode[] { new PathNode(nearestNode, Cardinal.North), new PathNode(nearestNode, Cardinal.East), new PathNode(nearestNode, Cardinal.South), new PathNode(nearestNode, Cardinal.West) };
            }
        }



        /// <summary>
        /// Actually now only sets fields in neighbourNode as appropriate.  The calling code must add this to openSet if necessary.
        /// </summary>
        private void UpdateNode(PathNode nearestNode, PathNode neighbourNode, float extraCost)
        {
            neighbourNode.gCost = nearestNode.gCost + nearestNode.distanceTo(neighbourNode) + extraCost;
            neighbourNode.Parent = nearestNode;
            neighbourNode.pathLength = nearestNode.pathLength + 1;
        }

        protected virtual bool traversable(PathNode node, PathNode target, float stepHeight, int maxFallHeight)
        {
            if (target.X == node.X && target.Z == node.Z && target.Y == node.Y) { return true; }
            if (traversable(blockAccess.GetBlock(new BlockPos(node.X, node.Y, node.Z, 0)))
                && traversable(blockAccess.GetBlock(new BlockPos(node.X, node.Y + 1, node.Z, 0))))
            {
                for (; 0 <= maxFallHeight; maxFallHeight--)
                {
                    Block belowBlock = blockAccess.GetBlock(new BlockPos(node.X, node.Y - 1, node.Z, 0));
                    if (canStep(belowBlock)) { return true; }
                    if (!traversable(belowBlock)) { return false; };
                    node.Y--;
                }
                while (climbableCodes.Exists(code => blockAccess.GetBlock(new BlockPos(node.X, node.Y, node.Z, 0)).Code.Path.Contains(code)))
                {
                    Block belowBlock = blockAccess.GetBlock(new BlockPos(node.X, node.Y - 1, node.Z, 0));
                    if (canStep(belowBlock)) { return true; }
                    node.Y--;
                }
            }
            else
            {
                for (; 1f < stepHeight; stepHeight--)
                {
                    node.Y++;
                    if (canStep(blockAccess.GetBlock(new BlockPos(node.X, node.Y - 1, node.Z, 0))) && traversable(blockAccess.GetBlock(new BlockPos(node.X, node.Y, node.Z, 0))) && traversable(blockAccess.GetBlock(new BlockPos(node.X, node.Y + 1, node.Z, 0))))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public BlockPos GetStartPos(Vec3d startPos)
        {
            var result = startPos.AsBlockPos;
            var startBlock = blockAccess.GetBlock(result);
            if (traversable(startBlock))
            {
                return result;
            }

            if (getDecimalPart(startPos.Z) < 0.5 && traversable(blockAccess.GetBlock(result.NorthCopy())))
            {
                return result.NorthCopy();
            }

            if (getDecimalPart(startPos.Z) > 0.5 && traversable(blockAccess.GetBlock(result.SouthCopy())))
            {
                return result.SouthCopy();
            }

            if (getDecimalPart(startPos.X) < 0.5 && traversable(blockAccess.GetBlock(result.West())))
            {
                return result;
            }

            if (getDecimalPart(startPos.X) > 0.5 && traversable(blockAccess.GetBlock(result.East())))
            {
                return result;
            }

            if (getDecimalPart(startPos.Z) < 0.5 && traversable(blockAccess.GetBlock(result.NorthCopy())))
            {
                return result.NorthCopy();
            }

            if (getDecimalPart(startPos.Z) > 0.5 && traversable(blockAccess.GetBlock(result.SouthCopy())))
            {
                return result.SouthCopy();
            }

            return startPos.AsBlockPos;
        }

        private double getDecimalPart(double number)
        {
            return number - Math.Truncate(number);
        }

        protected virtual bool canStep(Block belowBlock)
        {
            return belowBlock.SideSolid[BlockFacing.UP.Index] || steppableCodes.Exists(code => belowBlock.Code.Path.Contains(code));
        }

        protected virtual bool traversable(Block block)
        {
            return block.CollisionBoxes == null || block.CollisionBoxes.Length == 0 || traversableCodes.Exists(code => block.Code.Path.Contains(code));
        }

        List<PathNode> retracePath(PathNode startNode, PathNode endNode)
        {
            int length = endNode.pathLength;
            List<PathNode> path = new List<PathNode>(length + 1);
            for (int i = 0; i < length + 1; i++) path.Add(null);  // pre-fill the path with dummy values to achieve the required Count, needed for assignment to path[i] later
            PathNode currentNode = endNode;

            for (int i = length; i >= 0; i--)
            {
                path[i] = currentNode;
                currentNode = currentNode.Parent;
            }

            return path;
        }
    }
}