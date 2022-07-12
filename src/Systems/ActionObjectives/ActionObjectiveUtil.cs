using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public class ActionObjectiveUtil
    {
        public static int countBlockEntities(Vec3i pos, IBlockAccessor blockAccessor, Func<BlockEntity, bool> matcher)
        {
            int blockCount = 0;
            int chunksize = blockAccessor.ChunkSize;
            for (int x = pos.X - 100; x <= pos.X + 100; x += chunksize)
            {
                for (int y = pos.Y - 15; y <= pos.Y + 15; y += chunksize)
                {
                    for (int z = pos.Z - 100; z <= pos.Z + 100; z += chunksize)
                    {
                        var chunk = blockAccessor.GetChunkAtBlockPos(x, y, z);
                        if (chunk == null) { continue; }
                        foreach (var blockEntity in chunk.BlockEntities.Values)
                        {
                            if (matcher.Invoke(blockEntity))
                            {
                                blockCount++;
                            }
                        }
                    }
                }
            }
            return blockCount;
        }
    }
}