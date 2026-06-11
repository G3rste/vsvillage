using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage;

public abstract class AiTaskGotoAndInteract : AiTaskBase
{
    protected float moveSpeed;

    protected long lastSearch;

    protected long lastExecution;

    protected bool stuck;

    protected Vec3d targetPos;

    protected AnimationMetaData interactAnim;

    protected bool targetReached;

    protected List<VillagerPathNode> currentPath;

    protected int currentPathIndex;

    protected VillagerAStarNew pathfinder;

    protected Vec3d lastPosition;

    protected long stuckCheckTime;

    protected int timesStuck;

    protected long lastRepathTime;

    // Proactive obstacle state. Pause 500ms when blocked, then ONE repath attempt;
    // if still blocked, fall back to a path that ignores crowd avoidance.
    private long blockedSinceMs;
    private bool blockedRepathTried;
    private bool crowdAvoidanceDisabledForThisPath;

    // Scheduled gather tasks (mayor meetup, brazier socialize) override to false
    // because N villagers converging on one point would deadlock the avoidance.
    protected virtual bool RespectCrowdAvoidance => true;

    // Far from the workstation: subclasses can call this from GetTargetPos to redirect
    // there first. Avoids long bedroom-to-workpiece pathfinds that fail under crowd
    // avoidance. Returns null when close (< 3 blocks).
    protected Vec3d GetWorkstationApproachPosOrNull()
    {
        BlockPos ws = entity.GetBehavior<EntityBehaviorVillager>()?.Workstation;
        if (ws == null) return null;
        double dx = entity.Pos.X - (ws.X + 0.5);
        double dz = entity.Pos.Z - (ws.Z + 0.5);
        if (dx * dx + dz * dz < 9.0) return null;
        return ws.ToVec3d().Add(0.5, 0.0, 0.5);
    }

    protected BlockPos currentlyOpeningDoor;

    protected long doorOpenedTime;

    private int separationCheckTick = 0;

    private long maxTaskDurationMs;

    // Post-arrival interaction cap, prevents sticky looping anims (default "nod") from parking the villager forever. JSON: maxInteractionSeconds.
    private long maxInteractionMs;
    private long targetReachedAtMs;

    protected float maxDistance { get; set; }

    protected long taskStartedAtMs;

    // Re-call cadence for GetTargetPos in ShouldExecute. Civilians default 2000ms, combat tasks override to ~250ms. JSON: targetSearchIntervalMs.
    protected long targetSearchIntervalMs;

    // Entity-suffix gating, singular or plural array form. Vanilla AiTaskBase ignores these per-instance; all GotoAndInteract subclasses inherit via SuffixGatePasses.
    private readonly string[] onlyForSuffixes;
    private readonly string[] excludeSuffixes;

    public AiTaskGotoAndInteract(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
        : base(entity, taskConfig, aiConfig)
    {
        maxDistance = taskConfig["maxdistance"].AsFloat(5f);
        moveSpeed = taskConfig["movespeed"].AsFloat(0.008f);
        maxTaskDurationMs = (long)(taskConfig["maxtaskduration"].AsFloat(90f) * 1000f);
        maxInteractionMs = (long)(taskConfig["maxInteractionSeconds"].AsFloat(5f) * 1000f);
        targetSearchIntervalMs = taskConfig["targetSearchIntervalMs"].AsInt(2000);
        onlyForSuffixes = ReadSuffixList(taskConfig, "onlyForEntitySuffix", "onlyForEntitySuffixes");
        excludeSuffixes = ReadSuffixList(taskConfig, "excludeEntitySuffix", "excludeEntitySuffixes");
        interactAnim = new AnimationMetaData
        {
            Code = "nod",
            Animation = taskConfig["interact"].AsString("nod"),
            // Ease in/out smooths the walk-to-interact anim transition. Without it pose snaps mid-stride to interact, visible "hop".
            EaseInSpeed  = 3f,
            EaseOutSpeed = 3f
        }.Init();
        pathfinder = new VillagerAStarNew(entity.World.GetCachingBlockAccessor(synchronize: false, relight: false), entity.World, entity);
        lastPosition = null;
        stuckCheckTime = 0L;
        timesStuck = 0;
        lastRepathTime = 0L;
        currentlyOpeningDoor = null;
        doorOpenedTime = 0L;
    }

    // True if entity passes the onlyFor/exclude suffix gate. Subclasses with custom ShouldExecute call this manually.
    protected bool SuffixGatePasses()
    {
        string path = entity.Code?.Path ?? "";
        if (onlyForSuffixes != null)
        {
            bool any = false;
            for (int i = 0; i < onlyForSuffixes.Length; i++)
            {
                if (path.EndsWith(onlyForSuffixes[i])) { any = true; break; }
            }
            if (!any) return false;
        }
        if (excludeSuffixes != null)
        {
            for (int i = 0; i < excludeSuffixes.Length; i++)
            {
                if (path.EndsWith(excludeSuffixes[i])) return false;
            }
        }
        return true;
    }

    private static string[] ReadSuffixList(JsonObject cfg, string singularKey, string pluralKey)
    {
        JsonObject[] arr = cfg[pluralKey].AsArray();
        if (arr != null && arr.Length > 0)
        {
            var list = new System.Collections.Generic.List<string>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                string s = arr[i].AsString();
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
            if (list.Count > 0) return list.ToArray();
        }
        string single = cfg[singularKey].AsString(null);
        if (!string.IsNullOrEmpty(single)) return new[] { single };
        return null;
    }

    public override bool ShouldExecute()
    {
        if (!PreconditionsSatisfied()) return false;
        if (!SuffixGatePasses()) return false;

        // Vanilla cooldownUntilMs is an absolute timestamp; gate on it alone (don't add lastExecution).
        long elapsedMilliseconds = entity.World.ElapsedMilliseconds;
        if (targetSearchIntervalMs + lastSearch < elapsedMilliseconds && cooldownUntilMs < elapsedMilliseconds)
        {
            lastSearch = elapsedMilliseconds;
            targetPos = GetTargetPos();
        }
        return targetPos != null && cooldownUntilMs < elapsedMilliseconds;
    }

    protected abstract Vec3d GetTargetPos();

    public override void StartExecute()
    {
        taskStartedAtMs = entity.World.ElapsedMilliseconds;
        entity.GetBehavior<EntityBehaviorVillager>()?.TouchBusy();
        pathfinder.blockAccessor.Begin();
        pathfinder.SetEntityCollisionBox(entity);
        BlockPos startPos = pathfinder.GetStartPos(entity.Pos.XYZ);
        BlockPos asBlockPos = targetPos.AsBlockPos;
        currentPath = pathfinder.FindPath(startPos, asBlockPos);
        pathfinder.blockAccessor.Commit();
        if (currentPath != null && currentPath.Count > 0)
        {
            OnPathAcquired();
        }
        else if (TryWaypointRoute(startPos, asBlockPos))
        {
            // Tier-2 waypoint route succeeded; state set inside.
        }
        else if (TryRecoveryTeleportToMayor())
        {
            // Tier-3 fallback: teleport to mayor and retry direct pathfind.
            pathfinder.blockAccessor.Begin();
            startPos = pathfinder.GetStartPos(entity.Pos.XYZ);
            currentPath = pathfinder.FindPath(startPos, asBlockPos);
            pathfinder.blockAccessor.Commit();
            if (currentPath != null && currentPath.Count > 0) OnPathAcquired();
            else stuck = true;
        }
        else
        {
            stuck = true;
        }
        blockedSinceMs = 0;
        blockedRepathTried = false;
        crowdAvoidanceDisabledForThisPath = false;
        base.StartExecute();
    }

    private void OnPathAcquired()
    {
        currentPathIndex = 0;
        stuck = false;
        targetReached = false;
        lastPosition = entity.Pos.XYZ.Clone();
        stuckCheckTime = entity.World.ElapsedMilliseconds;
        timesStuck = 0;
    }

    // Hard cap so we don't path leg-A* across half the map to reach a distant waypoint.
    private const int MaxWaypointSeekRadius = 40;

    // HPA*-style fallback: route via village waypoint graph, A* per leg, stitch into one path.
    private bool TryWaypointRoute(BlockPos startPos, BlockPos endPos)
    {
        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        if (village == null || village.WaypointGraph == null || village.WaypointGraph.Count < 2) return false;

        BlockPos nearestStart = village.FindNearesWaypoint(startPos);
        BlockPos nearestEnd = village.FindNearesWaypoint(endPos);
        if (nearestStart == null || nearestEnd == null || nearestStart.Equals(nearestEnd)) return false;
        if (nearestStart.ManhattanDistance(startPos) > MaxWaypointSeekRadius) return false;
        if (nearestEnd.ManhattanDistance(endPos) > MaxWaypointSeekRadius) return false;
        if (!village.WaypointGraph.TryGetValue(nearestStart, out VillageWaypoint startWp)) return false;
        if (!village.WaypointGraph.TryGetValue(nearestEnd, out VillageWaypoint endWp)) return false;

        List<VillageWaypoint> seq = startWp.FindPath(endWp, village.WaypointGraph.Count);
        if (seq == null || seq.Count < 2) return false;
        if (!seq[seq.Count - 1].Equals(endWp)) return false;

        List<VillagerPathNode> combined = new List<VillagerPathNode>();
        BlockPos legFrom = startPos;
        pathfinder.blockAccessor.Begin();
        try
        {
            for (int i = 0; i < seq.Count; i++)
            {
                BlockPos legTo = seq[i].Pos;
                var leg = pathfinder.FindPath(legFrom, legTo);
                if (leg == null || leg.Count == 0) return false;
                AppendLeg(combined, leg);
                legFrom = legTo;
            }
            var finalLeg = pathfinder.FindPath(legFrom, endPos);
            if (finalLeg == null || finalLeg.Count == 0) return false;
            AppendLeg(combined, finalLeg);
        }
        finally { pathfinder.blockAccessor.Commit(); }

        if (combined.Count == 0) return false;
        currentPath = combined;
        OnPathAcquired();
        return true;
    }

    // Skip leg start nodes except the first so seams don't duplicate the waypoint cell.
    private static void AppendLeg(List<VillagerPathNode> combined, List<VillagerPathNode> leg)
    {
        int from = combined.Count == 0 ? 0 : 1;
        for (int i = from; i < leg.Count; i++) combined.Add(leg[i]);
    }

    public override bool ContinueExecute(float dt)
    {
        if (entity.World.ElapsedMilliseconds - taskStartedAtMs > maxTaskDurationMs)
            return false;
        CheckIfStuck();
        if (targetReached)
        {
            // Zero walk vector first so no residual movement bleeds in,
            // then apply a clamped separation nudge so villagers don't pile up.
            if (++separationCheckTick >= 15)
            {
                separationCheckTick = 0;
                entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
                ApplySeparationForce();
            }
            // Hard timeout on the interaction phase. Some tasks (the morning
            // GotoMayor gather, ambient chat, etc.) use the default "nod"
            // interactAnim which can be sticky/looping - without this cap the
            // task never ends and the villager freezes at the destination for
            // the rest of the day.
            if (entity.World.ElapsedMilliseconds - targetReachedAtMs > maxInteractionMs)
                return false;
            return entity.AnimManager.IsAnimationActive(interactAnim.Code);
        }
        if (!targetReached && targetPos != null && currentPath != null)
        {
            HandlePathTraversal();
            if (++separationCheckTick >= 15)
            {
                separationCheckTick = 0;
                ApplySeparationForce();
            }
        }
        if (InteractionPossible())
        {
            // Zero walk vector only, NOT StopAllMovement (latter resets jump/fly/sneak and can cause a vertical "hop" on uneven ground).
            entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
            entity.AnimManager.StopAnimation(animMeta.Code);
            entity.AnimManager.StartAnimation(interactAnim);
            targetReached = true;
            targetReachedAtMs = entity.World.ElapsedMilliseconds;
            return true;
        }
        return !stuck && currentPath != null;
    }

    protected virtual bool InteractionPossible()
    {
        return targetPos != null && entity.Pos.SquareDistanceTo(targetPos) < 2.25;
    }

    public override void FinishExecute(bool cancelled)
    {
        base.FinishExecute(cancelled);
        entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
        entity.Controls.StopAllMovement();
        entity.AnimManager.StopAnimation(animMeta.Code);
        DoorPathHelper.CloseOpenDoorsAlongPath(entity, currentPath);
        if (targetReached)
        {
            ApplyInteractionEffect();
            lastExecution = entity.World.ElapsedMilliseconds;
        }
        entity.AnimManager.StopAnimation(interactAnim.Code);
        targetPos = null;
        targetReached = false;
        currentPath = null;
        lastPosition = null;
        timesStuck = 0;
        currentlyOpeningDoor = null;
    }

    // Virtual no-op default. Override only when there's an actual effect on arrival.
    protected virtual void ApplyInteractionEffect() { }

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
        if (currentPath == null || currentPathIndex >= currentPath.Count)
        {
            stuck = true;
            return;
        }

        // Proactive obstacle check: pause 500ms when the next cell is occupied by another
        // villager/player, then one repath attempt. If still blocked, abandon the path so
        // the priority system can switch tasks. Skipped during gathers (villagers cluster).
        long nowMs = entity.World.ElapsedMilliseconds;
        bool gathering = entity.GetBehavior<EntityBehaviorVillager>()?.Village?.IsGatherActive == true;
        if (!gathering && RespectCrowdAvoidance && !crowdAvoidanceDisabledForThisPath
            && IsCellBlockedByOther(currentPath[currentPathIndex].BlockPos))
        {
            if (blockedSinceMs == 0) blockedSinceMs = nowMs;
            if (nowMs - blockedSinceMs < 500)
            {
                entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
                entity.AnimManager.StopAnimation(animMeta.Code);
                return;
            }
            if (!blockedRepathTried)
            {
                blockedRepathTried = true;
                lastRepathTime = nowMs;
                AttemptRepath();
                return;
            }
            // Pause expired AND one repath already failed. Fall back to a path that
            // ignores crowd avoidance, and disable avoidance for the remainder of this
            // path so we don't immediately re-trigger when other villagers are still there.
            crowdAvoidanceDisabledForThisPath = true;
            blockedSinceMs = 0;
            blockedRepathTried = false;
            AttemptRepathIgnoringCrowd();
            return;
        }
        if (!crowdAvoidanceDisabledForThisPath)
        {
            blockedSinceMs = 0;
            blockedRepathTried = false;
        }

        VillagerPathNode villagerPathNode = currentPath[currentPathIndex];
        Vec3d vec3d = villagerPathNode.BlockPos.ToVec3d().Add(0.5, 0.0, 0.5);
        Vec3d xYZ = entity.Pos.XYZ;
        double dx = xYZ.X - vec3d.X;
        double dz = xYZ.Z - vec3d.Z;
        if (Math.Sqrt(dx * dx + dz * dz) < 0.5)
        {
            currentPathIndex++;
            if (currentPathIndex < currentPath.Count)
            {
                VillagerPathNode next = currentPath[currentPathIndex];
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
        if (currentPathIndex < currentPath.Count)
        {
            VillagerPathNode current = currentPath[currentPathIndex];
            Vec3d nextPos = current.BlockPos.ToVec3d().Add(0.5, 0.0, 0.5);
            Vec3d dir = nextPos.Clone().Sub(xYZ);
            dir.Y = 0.0;
            dir = dir.Normalize();
            entity.Pos.Yaw = (float)Math.Atan2(dir.X, dir.Z);
            entity.Controls.WalkVector.Set(dir.X * moveSpeed, 0.0, dir.Z * moveSpeed);
            if (!entity.AnimManager.IsAnimationActive(animMeta.Code))
            {
                entity.AnimManager.StartAnimation(animMeta);
            }
        }
    }

    private void ApplySeparationForce()
    {
        Vec3d myPos = entity.Pos.XYZ;
        Entity[] nearby = entity.World.GetEntitiesAround(myPos, 1.5f, 1.5f, (Entity e) => e != entity && e.Code != null && e.Code.Domain == "vsvillage");
        if (nearby == null || nearby.Length == 0) return;

        double fx = 0.0, fz = 0.0;
        foreach (Entity neighbor in nearby)
        {
            Vec3d theirPos = neighbor.Pos.XYZ;
            double ndx = myPos.X - theirPos.X;
            double ndz = myPos.Z - theirPos.Z;
            double distSq = ndx * ndx + ndz * ndz;
            if (distSq < 1.0 && distSq > 0.0001)
            {
                double dist = Math.Sqrt(distSq);
                double pushStrength = (1.0 - dist) * 0.05;
                fx += ndx / dist * pushStrength;
                fz += ndz / dist * pushStrength;
            }
        }

        // Clamp total force - tighter cap when standing at target so stacked
        // villagers can't push each other away from their workstations.
        double forceMag = Math.Sqrt(fx * fx + fz * fz);
        double maxForce = targetReached ? 0.02 : 0.08;
        if (forceMag > maxForce)
        {
            fx = fx / forceMag * maxForce;
            fz = fz / forceMag * maxForce;
        }

        entity.Controls.WalkVector.X += fx;
        entity.Controls.WalkVector.Z += fz;
    }

    private void CheckIfStuck()
    {
        long now = entity.World.ElapsedMilliseconds;
        if (now - stuckCheckTime < 3000) return;

        Vec3d pos = entity.Pos.XYZ;
        if (lastPosition != null)
        {
            double moved = pos.DistanceTo(lastPosition);
            // Scale threshold by moveSpeed so slow tasks don't false-trigger.
            // At 20 ticks/s over 3 s expected = moveSpeed*60; flag below 40% of that.
            double stuckThreshold = Math.Max(0.25, moveSpeed * 60 * 0.4);
            if (moved < stuckThreshold)
            {
                timesStuck++;

                // Stuck at a door/gate (another villager closed it mid-traverse) reopens before repath: cheaper and usually enough.
                TryReopenBlockingGate();

                if (timesStuck <= 5)
                {
                    if (now - lastRepathTime > 3000)
                    {
                        AttemptRepath();
                        lastRepathTime = now;
                    }
                }
                else if (timesStuck >= 6)
                {
                    TeleportToRecoveryPosition();
                    timesStuck = 0;
                }
            }
            else
            {
                timesStuck = 0;
            }
        }
        lastPosition = pos.Clone();
        stuckCheckTime = now;
    }

    // Reopen the current or just-passed path-node door/gate if another villager closed it on us mid-traverse.
    private void TryReopenBlockingGate()
    {
        if (currentPath == null) return;

        // Check the node we're currently targeting and the one just behind us.
        int[] candidates = { currentPathIndex, currentPathIndex - 1 };
        foreach (int idx in candidates)
        {
            if (idx < 0 || idx >= currentPath.Count) continue;
            VillagerPathNode node = currentPath[idx];
            if (!node.IsDoor) continue;

            Block b = entity.World.BlockAccessor.GetBlock(node.BlockPos);
            if (b?.Code == null) continue;

            // A gate/door is "closed" when its code path does NOT contain "opened".
            bool isClosed = !b.Code.Path.Contains("opened") && !b.Code.Path.Contains("-open");
            if (isClosed)
            {
                DoorPathHelper.ToggleDoor(entity, node.BlockPos, opened: true);
                currentlyOpeningDoor = node.BlockPos.Copy();
                doorOpenedTime = entity.World.ElapsedMilliseconds;
                // Schedule it to close again after we've passed through.
                BlockPos doorPos = node.BlockPos.Copy();
                DoorPathHelper.ScheduleDoorClose(entity, doorPos, 3000);
                return;
            }
        }
    }

    // Exact-cell match aligned with VillagerAStarNew.RefreshOccupiedCells (which uses
    // AsBlockPos = floor). The earlier 0.7m box flagged 2-4 adjacent cells per entity
    // while A* only knew about one, so repaths kept producing the same blocked path.
    private bool IsCellBlockedByOther(BlockPos pos)
    {
        Vec3d center = pos.ToVec3d().Add(0.5, 0.5, 0.5);
        var near = entity.World.GetEntitiesAround(center, 1.5f, 1.5f, e =>
        {
            if (e == entity) return false;
            if (e is EntityPlayer) return true;
            return e.HasBehavior<EntityBehaviorVillager>();
        });
        if (near == null) return false;
        for (int i = 0; i < near.Length; i++)
        {
            if (near[i].Pos.AsBlockPos.Equals(pos)) return true;
        }
        return false;
    }

    protected void AttemptRepath()
    {
        if (targetPos == null) return;

        pathfinder.blockAccessor.Begin();
        pathfinder.SetEntityCollisionBox(entity);
        BlockPos startPos = pathfinder.GetStartPos(entity.Pos.XYZ);
        List<VillagerPathNode> newPath = pathfinder.FindPath(startPos, targetPos.AsBlockPos);
        pathfinder.blockAccessor.Commit();
        if (newPath != null && newPath.Count > 0)
        {
            currentPath = newPath;
            currentPathIndex = 0;
            stuck = false;
        }
        else
        {
            entity.World.Logger.Warning("[VsVillage] Pathfinder: no alternative path found for entity " + entity.EntityId);
        }
    }

    // Fallback used after one avoidance-aware repath fails to clear the blockage.
    // Tells the A* to skip the occupied-cells set once, so the new path can go
    // through villager-occupied cells (we'll walk into them physically; engine collision handles the rest).
    protected void AttemptRepathIgnoringCrowd()
    {
        if (targetPos == null) return;

        pathfinder.blockAccessor.Begin();
        pathfinder.SetEntityCollisionBox(entity);
        BlockPos startPos = pathfinder.GetStartPos(entity.Pos.XYZ);
        pathfinder.IgnoreCrowdOnce = true;
        List<VillagerPathNode> newPath = pathfinder.FindPath(startPos, targetPos.AsBlockPos);
        pathfinder.blockAccessor.Commit();
        if (newPath != null && newPath.Count > 0)
        {
            currentPath = newPath;
            currentPathIndex = 0;
            stuck = false;
        }
    }

    // Tier-3 fallback: teleport to mayor station when we can't compute any path
    // AND we're clearly outside the village. Cooldowned so a permanently broken
    // task doesn't teleport-loop every 5 seconds.
    private long lastRecoveryTeleportMs = 0;
    private const long RecoveryTeleportCooldownMs = 60_000L;

    private bool TryRecoveryTeleportToMayor()
    {
        long now = entity.World.ElapsedMilliseconds;
        if (now - lastRecoveryTeleportMs < RecoveryTeleportCooldownMs) return false;

        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        if (village?.Pos == null) return false;

        // Only fire when villager is outside the village. Targets unreachable from
        // inside (walled-off bed) don't benefit from a mid-village teleport.
        // EffectiveCenter: worldgen villages store Pos.Y ~20, which would both fail
        // the radius check and teleport the villager underground.
        BlockPos centre = village.EffectiveCenter();
        double radiusSq = village.Radius * (double)village.Radius;
        if (centre.DistanceSqTo(entity.Pos.X, entity.Pos.Y, entity.Pos.Z) < radiusSq) return false;

        Vec3d dest = FindStandingPosNearMayor(centre.ToVec3d());
        if (dest == null) return false;

        entity.TeleportTo(dest);
        lastRecoveryTeleportMs = now;
        return true;
    }

    private Vec3d FindStandingPosNearMayor(Vec3d centre)
    {
        IBlockAccessor ba = entity.World.BlockAccessor;
        BlockPos bp = centre.AsBlockPos;
        foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
        {
            BlockPos n = bp.AddCopy(facing.Normali.X, 0, facing.Normali.Z);
            Block at    = ba.GetBlock(n);
            Block above = ba.GetBlock(n.UpCopy());
            Block below = ba.GetBlock(n.DownCopy());
            bool posClear  = at.CollisionBoxes    == null || at.CollisionBoxes.Length    == 0;
            bool headClear = above.CollisionBoxes == null || above.CollisionBoxes.Length == 0;
            bool grounded  = below.CollisionBoxes != null && below.CollisionBoxes.Length != 0;
            if (posClear && headClear && grounded) return n.ToVec3d().Add(0.5, 0.0, 0.5);
        }
        return null;
    }

    private void TeleportToRecoveryPosition()
    {
        Vec3d dest = null;

        // Try skipping a couple of nodes along the existing path first.
        if (currentPath != null && currentPathIndex < currentPath.Count)
        {
            int skip = Math.Min(2, currentPath.Count - currentPathIndex - 1);
            if (skip > 0)
            {
                dest = currentPath[currentPathIndex + skip].BlockPos.ToVec3d().Add(0.5, 0.1, 0.5);
                currentPathIndex += skip;
            }
        }

        // Fall back to nudging toward the target.
        if (dest == null && targetPos != null)
        {
            Vec3d myPos = entity.Pos.XYZ;
            Vec3d diff = targetPos.Clone().Sub(myPos);
            // Avoid NaN from normalising a near-zero vector.
            if (diff.LengthSq() < 0.0001) return;
            Vec3d dir = diff.Normalize();
            dest = myPos.Add(dir.X * 2.0, 0.5, dir.Z * 2.0);
        }

        if (dest != null)
        {
            IBlockAccessor ba = entity.World.BlockAccessor;
            BlockPos bp = dest.AsBlockPos;
            bool floorClear = ba.GetBlock(bp).CollisionBoxes == null || ba.GetBlock(bp).CollisionBoxes.Length == 0;
            bool headClear = ba.GetBlock(bp.UpCopy()).CollisionBoxes == null || ba.GetBlock(bp.UpCopy()).CollisionBoxes.Length == 0;
            if (floorClear && headClear)
            {
                entity.TeleportTo(dest);
                entity.Pos.Motion.Y = 0.1;
            }
            else
            {
                entity.World.Logger.Warning("[VsVillage] Teleport recovery: unsafe destination at " + dest + " - skipping");
                stuck = true;
            }
        }
    }
}