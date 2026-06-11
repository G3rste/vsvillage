using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage;

// The mechhelper's only movement task.  It finds the nearest village's mayor
// workstation and walks to stand next to it.  Once there it stays put.
// If the workstation is relocated it walks to the new position.
// Before any village is founded (no mayor workstation within range) the
// mechhelper stands wherever it was spawned and does not move.
public class AiTaskMechHelperReturnToBase : AiTaskGotoAndInteract
{
    // Acceptable distance from the mayor workstation (blocks).
    private const float DefaultStandoff = 3f;
    // Village search radius - matches horn spawn-check.
    private const float SearchRadius    = 80f;

    private float standoff2;

    public AiTaskMechHelperReturnToBase(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
        : base(entity, taskConfig, aiConfig)
    {
        float standoff = taskConfig["standoffRadius"].AsFloat(DefaultStandoff);
        standoff2 = standoff * standoff;
    }

    // === AiTaskGotoAndInteract overrides ===

    protected override Vec3d GetTargetPos()
    {
        BlockPos mayor = FindNearestMayorWorkstation();
        if (mayor == null) return null;   // no village yet - stand still

        Vec3d mayorVec = mayor.ToVec3d().Add(0.5, 0.5, 0.5);

        // Already close enough - stay put.
        if (entity.Pos.XYZ.SquareDistanceTo(mayorVec) <= standoff2) return null;

        return PickSpotNear(mayorVec);
    }

    protected override void ApplyInteractionEffect()
    {
        // Nothing on arrival - entity stops and idles here.
    }

    // === Helpers ===

    private BlockPos FindNearestMayorWorkstation()
    {
        VillageManager vm = entity.World.Api.ModLoader.GetModSystem<VillageManager>();
        if (vm == null) return null;

        Vec3d selfPos = entity.Pos.XYZ;
        BlockPos best = null;
        double bestD  = SearchRadius * SearchRadius;

        foreach (Village v in vm.Villages.Values)
        {
            if (v.Pos == null) continue;
            // EffectiveCenter: worldgen villages store Pos.Y ~20, which would push
            // every village past SearchRadius and send the helper underground.
            BlockPos centre = v.EffectiveCenter();
            double d = selfPos.SquareDistanceTo(centre.ToVec3d());
            if (d < bestD)
            {
                bestD = d;
                best  = centre;
            }
        }
        return best;
    }

    private Vec3d PickSpotNear(Vec3d centre)
    {
        IBlockAccessor ba = entity.World.BlockAccessor;
        for (int i = 0; i < 10; i++)
        {
            double dx = entity.World.Rand.NextDouble() * 4 - 2;
            double dz = entity.World.Rand.NextDouble() * 4 - 2;
            Vec3d candidate = centre.AddCopy(dx, 0, dz);
            if (ba.GetBlock(candidate.AsBlockPos.UpCopy()).Id == 0)
                return candidate;
        }
        return centre;
    }
}
