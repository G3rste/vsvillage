using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.Essentials;

namespace VsVillage
{
    public class VillagerPathNode : IEquatable<VillagerPathNode>, IComparable<VillagerPathNode>
    {
        public VillagerPathNode Parent;
        public BlockPos BlockPos;
        public bool IsDoor = false;
        public float Cost;
        public PathNode ToPathNode() => new PathNode(BlockPos);
        public Vec3d ToWaypoint() => new Vec3d(BlockPos.X + 0.5, BlockPos.Y, BlockPos.Z + 0.5);

        public VillagerPathNode(BlockPos blockPos, BlockPos target, bool isDoor)
        {
            BlockPos = blockPos;
            Cost = blockPos.DistanceSqTo(target.X, target.Y, target.Z);
            IsDoor = isDoor;
        }

        public VillagerPathNode(VillagerPathNode parent, Cardinal cardinal)
        {
            Parent = parent;
            BlockPos = parent.BlockPos.AddCopy(cardinal.Normali.X, cardinal.Normali.Y, cardinal.Normali.Z);
        }

        public void Init(BlockPos target, bool isDoor)
        {
            Cost = BlockPos.DistanceSqTo(target.X, target.Y, target.Z);
            IsDoor = isDoor;
        }

        public bool Equals(VillagerPathNode other)
        {
            return BlockPos.Equals(other?.BlockPos);
        }

        public override bool Equals(object obj)
        {
            if (obj is VillagerPathNode other)
            {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return BlockPos.GetHashCode();
        }

        public List<VillagerPathNode> RetracePath()
        {
            var current = this;
            var result = new List<VillagerPathNode>([current]);
            while (current.Parent != null)
            {
                current = current.Parent;
                result.Add(current);
            }
            result.Reverse();
            return result;
        }

        public int CompareTo(VillagerPathNode other)
        {
            return Cost.CompareTo(other.Cost);
        }
    }
}