using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage;

/// <summary>
/// Villager "drinking water" idle routine.
///
/// Fires once or twice per game day (controlled by drinkIntervalHours) during
/// waking hours.  Requires a water source within the village — a
/// hydrateordiedrate:winch-*, watersheds:wellpulley-*, or any game:water-* block.
///
/// Only one villager may be in this task at a time (per village).  If another
/// villager is already drinking, ShouldExecute returns false until they finish.
///
/// Phases:
///   1. GoToWater  — path to a spot 2 blocks from the source
///   2. Drinking   — equip jug, play mug-standdrink, wait drinkDurationSeconds
///   3. GoHome     — walk back to the mayor workstation
/// </summary>
public class AiTaskVillagerDrinkWater : AiTaskGotoAndInteract
{
    private enum Phase { GoToWater, Drinking, GoHome }

    // ── static lock — one villager drinking at a time, per village ────────────
    // Keyed on village ID so separate villages don't block each other.
    private static readonly System.Collections.Generic.Dictionary<string, bool> drinkingInProgress
        = new System.Collections.Generic.Dictionary<string, bool>();

    private static bool IsDrinkingInProgress(string villageId)
    {
        if (villageId == null) return false;
        return drinkingInProgress.TryGetValue(villageId, out bool v) && v;
    }

    private static void SetDrinkingInProgress(string villageId, bool value)
    {
        if (villageId == null) return;
        drinkingInProgress[villageId] = value;
    }

    // ── config ────────────────────────────────────────────────────────────────
    private double drinkIntervalHours;
    private long drinkDurationMs;
    private float offset;

    // ── runtime state ─────────────────────────────────────────────────────────
    private Phase phase;
    private double lastDrankTotalHours = -9999.0;
    private long drinkStartMs;
    private ItemStack savedRightHand;
    private string currentVillageId;

    // cached water-source scan — refreshed every 2 real-time minutes
    private BlockPos cachedWaterPos;
    private long cacheExpiresMs = -1L;

    // ── ctor ──────────────────────────────────────────────────────────────────

    public AiTaskVillagerDrinkWater(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
        : base(entity, taskConfig, aiConfig)
    {
        drinkIntervalHours = taskConfig["drinkIntervalHours"].AsDouble(13.0);
        drinkDurationMs = (long)(taskConfig["drinkDurationSeconds"].AsFloat(5f) * 1000f);
        offset = (float)entity.World.Rand.Next(
            taskConfig["minoffset"].AsInt(-30),
            taskConfig["maxoffset"].AsInt(30)) / 100f;
    }

    // ── AiTaskGotoAndInteract overrides ───────────────────────────────────────

    protected override Vec3d GetTargetPos() => targetPos;

    protected override void ApplyInteractionEffect() { }

    protected override bool InteractionPossible()
    {
        if (phase == Phase.GoHome) return false;
        return targetPos != null && entity.Pos.SquareDistanceTo(targetPos) < 4.0;
    }

    public override bool ShouldExecute()
    {
        if (!IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World, offset))
            return false;

        double now = entity.World.Calendar.TotalHours;
        if (now - lastDrankTotalHours < drinkIntervalHours) return false;

        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        if (village == null) return false;

        // Only one villager per village may drink at a time.
        if (IsDrinkingInProgress(village.Id)) return false;

        BlockPos water = GetCachedWaterSource(village);
        if (water == null) return false;

        targetPos = FindDrinkingSpot(water);
        if (targetPos == null) return false;

        currentVillageId = village.Id;
        return true;
    }

    public override void StartExecute()
    {
        phase = Phase.GoToWater;
        targetReached = false;
        taskStartedAtMs = entity.World.ElapsedMilliseconds;
        SetDrinkingInProgress(currentVillageId, true);
        base.StartExecute();
    }

    public override bool ContinueExecute(float dt)
    {
        // ── Phase 1: walk to water ────────────────────────────────────────────
        if (phase == Phase.GoToWater)
        {
            bool cont = base.ContinueExecute(dt);
            if (targetReached)
            {
                entity.AnimManager.StopAnimation("nod");
                BeginDrinking();
                phase = Phase.Drinking;
                return true;
            }
            return cont;
        }

        // ── Phase 2: drink ────────────────────────────────────────────────────
        if (phase == Phase.Drinking)
        {
            if (entity.World.ElapsedMilliseconds - drinkStartMs >= drinkDurationMs)
            {
                EndDrinking();
                BeginGoHome();
                phase = Phase.GoHome;
            }
            return true;
        }

        // ── Phase 3: walk home ────────────────────────────────────────────────
        if (phase == Phase.GoHome)
        {
            if (targetPos != null && entity.Pos.XYZ.SquareDistanceTo(targetPos) < 9.0)
                return false;

            return base.ContinueExecute(dt);
        }

        return false;
    }

    public override void FinishExecute(bool cancelled)
    {
        entity.AnimManager.StopAnimation("mug-standdrink");
        RestoreRightHand();

        if (phase == Phase.Drinking || phase == Phase.GoHome)
            lastDrankTotalHours = entity.World.Calendar.TotalHours;

        // Release the lock so another villager can drink.
        SetDrinkingInProgress(currentVillageId, false);
        currentVillageId = null;

        base.FinishExecute(cancelled);
    }

    // ── Phase helpers ─────────────────────────────────────────────────────────

    private void BeginDrinking()
    {
        drinkStartMs = entity.World.ElapsedMilliseconds;
        savedRightHand = entity.RightHandItemSlot?.Itemstack?.Clone();

        Item jug = entity.World.GetItem(new AssetLocation("game:jug-black-fired"));
        if (jug != null)
        {
            entity.RightHandItemSlot.Itemstack = new ItemStack(jug);
            entity.RightHandItemSlot.MarkDirty();
        }

        entity.AnimManager.StartAnimation(new AnimationMetaData
        {
            Animation = "mug-standdrink",
            Code = "mug-standdrink",
            AnimationSpeed = 1.0f,
            BlendMode = EnumAnimationBlendMode.Average,
            EaseInSpeed = 2f,
            EaseOutSpeed = 2f
        }.Init());
    }

    private void EndDrinking()
    {
        entity.AnimManager.StopAnimation("mug-standdrink");
        RestoreRightHand();
    }

    private void BeginGoHome()
    {
        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        BlockPos mayorPos = village?.Pos;
        if (mayorPos == null) return;

        targetPos = mayorPos.ToVec3d().Add(0.5, 0.5, 0.5);
        targetReached = false;
        stuck = false;
        taskStartedAtMs = entity.World.ElapsedMilliseconds;
        AttemptRepath();
    }

    private void RestoreRightHand()
    {
        if (entity.RightHandItemSlot == null) return;
        if (entity.RightHandItemSlot.Itemstack?.Collectible?.Code?.Path?.Contains("jug") == true)
        {
            entity.RightHandItemSlot.Itemstack = savedRightHand;
            entity.RightHandItemSlot.MarkDirty();
        }
        savedRightHand = null;
    }

    // ── Water-source scanning ─────────────────────────────────────────────────

    private BlockPos GetCachedWaterSource(Village village)
    {
        long now = entity.World.ElapsedMilliseconds;
        if (cachedWaterPos != null && now < cacheExpiresMs) return cachedWaterPos;
        cachedWaterPos = ScanForWaterSource(village);
        cacheExpiresMs = now + 120_000L;
        return cachedWaterPos;
    }

    private BlockPos ScanForWaterSource(Village village)
    {
        // EffectiveCenter: worldgen villages store Pos.Y ~20, and a 10-block Y pad
        // around that would scan for water far below the surface.
        BlockPos center = village.EffectiveCenter();
        int r = village.Radius;
        const int yPad = 10;
        IBlockAccessor ba = entity.World.BlockAccessor;
        BlockPos tmp = new BlockPos(0);
        BlockPos best = null;
        double bestD = double.MaxValue;
        Vec3d selfPos = entity.Pos.XYZ;

        for (int x = center.X - r; x <= center.X + r; x++)
            for (int z = center.Z - r; z <= center.Z + r; z++)
            {
                int dx = x - center.X, dz = z - center.Z;
                if (dx * dx + dz * dz > r * r) continue;

                for (int y = center.Y - yPad; y <= center.Y + yPad; y++)
                {
                    tmp.Set(x, y, z);
                    Block b = ba.GetBlock(tmp);
                    if (!IsWaterSource(b)) continue;

                    double d = selfPos.SquareDistanceTo(tmp.ToVec3d());
                    if (d < bestD) { bestD = d; best = tmp.Copy(); }
                }
            }
        return best;
    }

    private static bool IsWaterSource(Block b)
    {
        if (b?.Code == null) return false;
        string domain = b.Code.Domain;
        string path = b.Code.Path;
        if (domain == "hydrateordiedrate" && path.Contains("winch")) return true;
        if (domain == "watersheds" && path.Contains("wellpulley")) return true;
        if (domain == "game" && path.StartsWith("water")) return true;
        return false;
    }

    private Vec3d FindDrinkingSpot(BlockPos water)
    {
        IBlockAccessor ba = entity.World.BlockAccessor;
        int[][] offsets =
        {
            new[]{2,0}, new[]{-2,0}, new[]{0,2},  new[]{0,-2},
            new[]{2,1}, new[]{-2,1}, new[]{1,2},  new[]{-1,2},
            new[]{3,0}, new[]{-3,0}, new[]{0,3},  new[]{0,-3}
        };
        foreach (int[] off in offsets)
        {
            BlockPos candidate = new BlockPos(
                water.X + off[0], water.Y, water.Z + off[1], water.dimension);
            if (IsPassable(ba, candidate))
                return candidate.ToVec3d().Add(0.5, 0.0, 0.5);
        }
        return null;
    }

    private static bool IsPassable(IBlockAccessor ba, BlockPos pos)
    {
        Block atPos = ba.GetBlock(pos);
        Block above = ba.GetBlock(pos.UpCopy());
        return (atPos.CollisionBoxes == null || atPos.CollisionBoxes.Length == 0)
            && (above.CollisionBoxes == null || above.CollisionBoxes.Length == 0);
    }
}