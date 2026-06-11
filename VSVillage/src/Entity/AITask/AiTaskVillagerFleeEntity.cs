using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage;

// Villager flee task using VillagerAStarNew (gates, fences, terrain). Replaces vanilla fleeentity for villagers.
// SHELTER mode first (nearest barracks, then bed), PANIC mode fallback (5-angle away-from-threat A* legs).
public class AiTaskVillagerFleeEntity : AiTaskBase
{
    // === Config ===
    private readonly List<string> threatCodes = new List<string>();
    private readonly List<string> excludeSuffixes = new List<string>();
    private float seekingRange = 20f;
    private float fleeingDistance = 30f;
    private float moveSpeed = 0.015f;
    private long fleeDurationMs = 12000L;

    // === Runtime state ===
    private Entity threat;
    private List<VillagerPathNode> currentPath;
    private int pathIndex;
    private VillagerAStarNew pathfinder;
    private long fleeStartMs;
    private bool stuck;
    private Vec3d lastPos;
    private long stuckCheckMs;
    private int timesStuck;
    private long lastRepathMs;

    // === Shelter-mode state ===
    // Non-null when we're heading for shelter (barracks or bed room).
    private Vec3d shelterPos;
    // True once we have arrived at shelterPos and are cowering in place.
    private bool reachedShelter;

    // === Door state ===
    private BlockPos currentlyOpeningDoor;
    private long doorOpenedTime;

    private const long RepathIntervalMs = 1500L;
    private const long StuckCheckMs = 2000L;

    // Cower animation played once the villager is at the shelter position.
    // Matches the storm-shelter idle so it reads as "huddled and waiting".
    private const string CowerAnimation = "idlelook";

    // Vertical detection cap. Threats beyond this Y delta are ignored so villagers don't flee unreachable monsters.
    private const float MaxVertDetection = 3f;

    // === Constructor ===

    public AiTaskVillagerFleeEntity(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
        : base(entity, taskConfig, aiConfig)
    {
        seekingRange = taskConfig["seekingRange"].AsFloat(20f);
        fleeingDistance = taskConfig["fleeingDistance"].AsFloat(30f);
        moveSpeed = taskConfig["movespeed"].AsFloat(0.015f);
        fleeDurationMs = taskConfig["fleeDurationMs"].AsInt(12000);

        JsonObject[] codes = taskConfig["entityCodes"].AsArray();
        if (codes != null)
            foreach (JsonObject c in codes)
            {
                string s = c.AsString();
                if (!string.IsNullOrEmpty(s)) threatCodes.Add(s);
            }

        JsonObject exclNode = taskConfig["excludeEntitySuffixes"];
        if (exclNode != null && exclNode.Exists)
        {
            string[] excl = exclNode.AsArray(System.Array.Empty<string>());
            excludeSuffixes.AddRange(excl);
        }

        pathfinder = new VillagerAStarNew(
            entity.World.GetCachingBlockAccessor(synchronize: false, relight: false),
            entity.World, entity);
    }

    // === Sleep-window hurt gate ===

    private long _lastHurtAtMs = -1L;
    // Window during which a recent hit still authorises a sleep-hours flee. Long enough
    // to cover the full fleeDurationMs (12s) plus aftermath, short enough that one hit
    // doesn't unlock fleeing for the rest of the night.
    private const long HurtMemoryMs = 30_000L;

    public override void OnEntityHurt(DamageSource source, float damage)
    {
        base.OnEntityHurt(source, damage);
        if (source?.Type == EnumDamageType.Heal) return;
        _lastHurtAtMs = entity.World.ElapsedMilliseconds;
    }

    private bool IsSleepHours()
    {
        float hour = entity.World.Calendar.HourOfDay / entity.World.Calendar.HoursPerDay * 24f;
        return hour >= 21f || hour < 6f;
    }

    private bool RecentlyHurt()
        => _lastHurtAtMs >= 0L && entity.World.ElapsedMilliseconds - _lastHurtAtMs < HurtMemoryMs;

    // === ShouldExecute ===

    public override bool ShouldExecute()
    {
        if (threatCodes.Count == 0) return false;
        if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;

        // Skip for entity types that never flee (soldiers, archers).
        string myPath = entity.Code?.Path ?? "";
        foreach (string sfx in excludeSuffixes)
            if (myPath.EndsWith(sfx)) return false;

        // Asleep villagers don't preemptively flee from prowling hostiles. Only run if
        // a hit has actually landed recently, otherwise they should keep sleeping.
        if (IsSleepHours() && !RecentlyHurt()) return false;

        threat = entity.World.GetNearestEntity(
            entity.Pos.XYZ, seekingRange, MaxVertDetection,
            e => e != entity && e.Alive && e.IsInteractable && MatchesThreat(e));

        return threat != null;
    }

    // === StartExecute ===

    public override void StartExecute()
    {
        base.StartExecute();
        fleeStartMs = entity.World.ElapsedMilliseconds;
        lastRepathMs = entity.World.ElapsedMilliseconds;
        stuckCheckMs = entity.World.ElapsedMilliseconds;
        stuck = false;
        pathIndex = 0;
        currentPath = null;
        timesStuck = 0;
        lastPos = entity.Pos.XYZ.Clone();
        currentlyOpeningDoor = null;
        doorOpenedTime = 0L;
        shelterPos = null;
        reachedShelter = false;

        // First try to find a shelter (barracks → bed room). If neither is
        // reachable, fall through to the original PANIC flee behaviour.
        TryAcquireShelter();

        if (shelterPos != null)
        {
            PathToShelter();
            // If the shelter pathfind failed, drop back to panic mode.
            if (currentPath == null || currentPath.Count == 0)
            {
                shelterPos = null;
                ComputeAndPath();
            }
        }
        else
        {
            ComputeAndPath();
        }
    }

    // === ContinueExecute ===

    public override bool ContinueExecute(float dt)
    {
        long now = entity.World.ElapsedMilliseconds;

        if (now - fleeStartMs > fleeDurationMs) return false;
        if (threat == null || !threat.Alive) return false;

        // Horizontal-only safety check + Y-clamp.
        double dxSafe = entity.Pos.X - threat.Pos.X;
        double dzSafe = entity.Pos.Z - threat.Pos.Z;
        bool threatIsFar = (dxSafe * dxSafe + dzSafe * dzSafe) > fleeingDistance * fleeingDistance
                          || Math.Abs(entity.Pos.Y - threat.Pos.Y) > MaxVertDetection;

        // === Cowering branch ===
        if (reachedShelter)
        {
            // Once the threat is far enough away, stop cowering and resume normal life.
            if (threatIsFar) return false;
            PlayCowerAnimation();
            // Hold still - no walk vector.
            entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
            return true;
        }

        // === Moving branch ===
        if (currentPath == null || stuck) return false;
        if (threatIsFar) return false;

        // SHELTER mode: did we arrive?
        if (shelterPos != null && entity.Pos.SquareDistanceTo(shelterPos) < 2.25)
        {
            ArriveAtShelter();
            return true;
        }

        // Repath periodically. PANIC mode re-runs ComputeAndPath as the threat
        // moves; SHELTER mode keeps the same destination so we don't get stuck
        // recomputing the same path.
        if (shelterPos == null && now - lastRepathMs > RepathIntervalMs)
        {
            lastRepathMs = now;
            ComputeAndPath();
            if (stuck) return false;
        }

        CheckIfStuck();
        if (stuck) return false;

        // Reached end of current path leg.
        if (pathIndex >= currentPath.Count)
        {
            if (shelterPos != null)
            {
                // SHELTER mode: we've reached the end of the path without arriving.
                // Treat as arrived (close enough) and start cowering.
                ArriveAtShelter();
                return true;
            }
            // PANIC mode: plan another leg.
            ComputeAndPath();
            if (stuck) return false;
        }

        HandlePathTraversal();
        return true;
    }

    // === FinishExecute ===

    public override void FinishExecute(bool cancelled)
    {
        base.FinishExecute(cancelled);
        entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
        entity.Controls.StopAllMovement();
        if (animMeta != null)
            entity.AnimManager.StopAnimation(animMeta.Code);
        entity.AnimManager.StopAnimation(CowerAnimation);
        entity.Pos.Motion.X = 0.0;
        entity.Pos.Motion.Z = 0.0;
        DoorPathHelper.CloseOpenDoorsAlongPath(entity, currentPath);
        threat = null;
        currentPath = null;
        pathIndex = 0;
        timesStuck = 0;
        lastPos = null;
        currentlyOpeningDoor = null;
        shelterPos = null;
        reachedShelter = false;
    }

    // === Shelter acquisition ===

    // Shelter chain: barracks (designed for combat) -> mayor station (central, almost
    // always present) -> villager's own bed (last resort, may be far from threat).
    private void TryAcquireShelter()
    {
        EntityBehaviorVillager vb = entity.GetBehavior<EntityBehaviorVillager>();
        Village village = vb?.Village;

        // 1. Barracks (nearest soldier workstation, scattered inside its room).
        if (village != null)
            shelterPos = FindBarracksPos(village);

        // 2. Mayor station as the central village fallback. Almost always present.
        if (shelterPos == null && village?.Pos != null)
            shelterPos = FindStandingPosNear(village.EffectiveCenter().ToVec3d());

        // 3. Bed room (a standing tile next to the villager's bed).
        if (shelterPos == null && vb?.Bed != null)
            shelterPos = FindStandingPosNear(vb.Bed.ToVec3d());
    }

    private Vec3d FindBarracksPos(Village village)
    {
        List<VillagerWorkstation> barracks = village.Workstations.Values
            .Where(ws => ws.Profession == EnumVillagerProfession.soldier)
            .ToList();
        if (barracks.Count == 0) return null;

        Vec3d myPos = entity.Pos.XYZ;
        VillagerWorkstation nearest = barracks
            .OrderBy(ws => ws.Pos.DistanceTo(myPos.AsBlockPos))
            .First();

        // Constrain to the workstation's room so we don't end up outside.
        RoomRegistry roomReg = entity.Api.ModLoader.GetModSystem<RoomRegistry>();
        Room room = null;
        try { room = roomReg?.GetRoomForPosition(nearest.Pos); } catch { }

        System.Random rng = entity.World.Rand;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int ox = rng.Next(-3, 4);
            int oz = rng.Next(-3, 4);
            BlockPos candidateBlock = nearest.Pos.AddCopy(ox, 0, oz);

            if (room != null)
            {
                Cuboidi loc = room.Location;
                if (candidateBlock.X < loc.X1 || candidateBlock.X > loc.X2 ||
                    candidateBlock.Z < loc.Z1 || candidateBlock.Z > loc.Z2)
                    continue;
            }

            Vec3d pos = FindStandingPosNear(candidateBlock.ToVec3d());
            if (pos != null) return pos;
        }

        return FindStandingPosNear(nearest.Pos.ToVec3d());
    }

    // Find a clear standing tile adjacent to .
    private Vec3d FindStandingPosNear(Vec3d centre)
    {
        IBlockAccessor ba = entity.World.BlockAccessor;
        BlockPos bp = centre.AsBlockPos;
        foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
        {
            BlockPos neighbor = bp.AddCopy(facing.Normali.X, 0, facing.Normali.Z);
            Block atPos = ba.GetBlock(neighbor);
            Block above = ba.GetBlock(neighbor.UpCopy());
            Block below = ba.GetBlock(neighbor.DownCopy());
            bool posClear = atPos.CollisionBoxes == null || atPos.CollisionBoxes.Length == 0;
            bool headClear = above.CollisionBoxes == null || above.CollisionBoxes.Length == 0;
            bool grounded = below.CollisionBoxes != null && below.CollisionBoxes.Length != 0;
            if (posClear && headClear && grounded)
                return neighbor.ToVec3d().Add(0.5, 0.0, 0.5);
        }
        return centre.Add(0.5, 1.0, 0.5);
    }

    // Run A* once to the shelter destination.
    private void PathToShelter()
    {
        if (shelterPos == null) { stuck = true; return; }
        pathfinder.blockAccessor.Begin();
        pathfinder.SetEntityCollisionBox(entity);
        BlockPos start = pathfinder.GetStartPos(entity.Pos.XYZ);
        currentPath = pathfinder.FindPath(start, shelterPos.AsBlockPos, 10000);
        pathfinder.blockAccessor.Commit();
        if (currentPath != null && currentPath.Count > 0)
        {
            pathIndex = 0;
            stuck = false;
            currentlyOpeningDoor = null;
        }
    }

    private void ArriveAtShelter()
    {
        entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
        entity.Controls.StopAllMovement();
        entity.Pos.Motion.X = 0.0;
        entity.Pos.Motion.Z = 0.0;
        if (animMeta != null)
            entity.AnimManager.StopAnimation(animMeta.Code);
        reachedShelter = true;
        PlayCowerAnimation();
    }

    private void PlayCowerAnimation()
    {
        if (!entity.AnimManager.IsAnimationActive(CowerAnimation))
        {
            entity.AnimManager.StartAnimation(new AnimationMetaData
            {
                Animation = CowerAnimation,
                Code = CowerAnimation,
                AnimationSpeed = 1.0f,
                BlendMode = EnumAnimationBlendMode.Average
            }.Init());
        }
    }

    // === PANIC-mode pathfinding ===

    // Five angles away from threat; first viable one wins. Destinations outside
    // the village radius are skipped so a panicking villager can't sprint into
    // the wilderness and get stranded.
    private void ComputeAndPath()
    {
        if (threat == null) { stuck = true; return; }

        Vec3d myPos = entity.Pos.XYZ;
        Vec3d threatPos = threat.Pos.XYZ;

        double dx = myPos.X - threatPos.X;
        double dz = myPos.Z - threatPos.Z;
        double dist = Math.Sqrt(dx * dx + dz * dz);
        if (dist < 0.01) { dx = 1; dz = 0; } else { dx /= dist; dz /= dist; }

        float legDist = fleeingDistance * 0.75f;
        float[] angles = { 0f, -0.785f, 0.785f, -1.571f, 1.571f };

        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        double villageRadiusSq = village != null ? village.Radius * (double)village.Radius : 0.0;

        pathfinder.blockAccessor.Begin();
        pathfinder.SetEntityCollisionBox(entity);
        BlockPos startPos = pathfinder.GetStartPos(myPos);

        foreach (float angle in angles)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            double fdx = dx * cos - dz * sin;
            double fdz = dx * sin + dz * cos;

            BlockPos dest = new BlockPos(
                (int)(myPos.X + fdx * legDist),
                (int)myPos.Y,
                (int)(myPos.Z + fdz * legDist));

            // Stay inside village radius. Skip any angle that lands outside.
            if (village?.Pos != null && village.EffectiveCenter().DistanceSqTo(dest.X, dest.Y, dest.Z) > villageRadiusSq) continue;

            List<VillagerPathNode> path = pathfinder.FindPath(startPos, dest, 1200);
            if (path != null && path.Count > 1)
            {
                pathfinder.blockAccessor.Commit();
                currentPath = path;
                pathIndex = 0;
                stuck = false;
                currentlyOpeningDoor = null;
                return;
            }
        }

        pathfinder.blockAccessor.Commit();
        stuck = true;
    }

    // === Movement ===

    private void HandlePathTraversal()
    {
        if (currentlyOpeningDoor != null)
        {
            if (entity.World.ElapsedMilliseconds - doorOpenedTime < 500)
            {
                entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
                return;
            }
            currentlyOpeningDoor = null;
        }
        if (currentPath == null || pathIndex >= currentPath.Count)
        {
            stuck = true;
            return;
        }
        VillagerPathNode villagerPathNode = currentPath[pathIndex];
        Vec3d vec3d = villagerPathNode.BlockPos.ToVec3d().Add(0.5, 0.0, 0.5);
        Vec3d xYZ = entity.Pos.XYZ;
        double dx = xYZ.X - vec3d.X;
        double dz = xYZ.Z - vec3d.Z;
        if (Math.Sqrt(dx * dx + dz * dz) < 0.5)
        {
            pathIndex++;
            if (pathIndex < currentPath.Count)
            {
                VillagerPathNode next = currentPath[pathIndex];
                if (next.IsDoor)
                {
                    DoorPathHelper.ToggleDoor(entity, next.BlockPos, opened: true);
                    currentlyOpeningDoor = next.BlockPos.Copy();
                    doorOpenedTime = entity.World.ElapsedMilliseconds;
                }
            }
            if (villagerPathNode.IsDoor)
            {
                BlockPos doorPos = villagerPathNode.BlockPos.Copy();
                DoorPathHelper.ScheduleDoorClose(entity, doorPos, 3000);
            }
        }
        if (pathIndex < currentPath.Count)
        {
            VillagerPathNode current = currentPath[pathIndex];
            Vec3d nextPos = current.BlockPos.ToVec3d().Add(0.5, 0.0, 0.5);
            Vec3d dir = nextPos.Clone().Sub(xYZ);
            dir.Y = 0.0;
            dir = dir.Normalize();
            entity.Pos.Yaw = (float)Math.Atan2(dir.X, dir.Z);
            entity.Controls.WalkVector.Set(dir.X * moveSpeed, 0.0, dir.Z * moveSpeed);
            if (animMeta != null && !entity.AnimManager.IsAnimationActive(animMeta.Code))
            {
                entity.AnimManager.StartAnimation(animMeta);
            }
        }
    }

    private void CheckIfStuck()
    {
        long now = entity.World.ElapsedMilliseconds;
        if (now - stuckCheckMs < StuckCheckMs) return;

        Vec3d myPos = entity.Pos.XYZ;
        if (lastPos != null && myPos.DistanceTo(lastPos) < 0.3f)
        {
            timesStuck++;
            if (timesStuck >= 3)
            {
                timesStuck = 0;
                // SHELTER mode: drop to PANIC mode rather than re-attempting the
                // same blocked shelter path forever.
                if (shelterPos != null)
                {
                    shelterPos = null;
                    ComputeAndPath();
                }
                else
                {
                    ComputeAndPath();
                }
            }
        }
        else
        {
            timesStuck = 0;
        }

        lastPos = myPos.Clone();
        stuckCheckMs = now;
    }

    // === Helpers ===

    private bool MatchesThreat(Entity e)
    {
        string path = e.Code?.Path ?? "";
        foreach (string pattern in threatCodes)
        {
            if (pattern.EndsWith("*"))
            {
                if (path.StartsWith(pattern.Substring(0, pattern.Length - 1),
                    StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
