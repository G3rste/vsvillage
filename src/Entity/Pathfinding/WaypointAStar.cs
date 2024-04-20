using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;

namespace VsVillage
{
    public class WaypointAStar : VillagerAStar
    {
        public WaypointAStar(ICoreServerAPI api) : base(api)
        {
            traversableCodes = new List<string>() { "door", "gate", "multiblock" };
            climbableCodes = new List<string>();
            steppableCodes = new List<string>() { "stair", "path", "packed" };
        }

        protected override IEnumerable<PathNode> findValidNeighbourNodes(PathNode nearestNode, PathNode targetNode, float stepHeight, int maxFallHeight)
        {
            return new PathNode[] {
                new PathNode(nearestNode, Cardinal.North),
                new PathNode(nearestNode, Cardinal.East),
                new PathNode(nearestNode, Cardinal.South),
                new PathNode(nearestNode, Cardinal.West)
            };
        }

        protected override bool traversable(PathNode node, PathNode target, float stepHeight, int maxFallHeight)
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
            }
            else
            {
                for (; 1f < stepHeight; stepHeight--)
                {
                    node.Y++;
                    if (canStep(blockAccess.GetBlock(new BlockPos(node.X, node.Y - 1, node.Z, 0)))
                        && traversable(blockAccess.GetBlock(new BlockPos(node.X, node.Y, node.Z, 0)))
                        && traversable(blockAccess.GetBlock(new BlockPos(node.X, node.Y + 1, node.Z, 0))))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected override bool canStep(Block belowBlock)
        {
            return steppableCodes.Exists(code => belowBlock.Code.Path.Contains(code));
        }
    }
}