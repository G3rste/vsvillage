using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage;

public class AiTaskTravellingTraderLeave : AiTaskBase
{
    private const float MoveSpeed = 0.009f;

    // All exit/despawn distances use this single constant for consistency.
    private const float ExitDist = 55f;

    // Despawn immediately if within this many blocks of the exit target.
    private const float ProximityDespawnDist = 5f;

    // Despawn immediately if stuck within this many blocks of the exit target.
    private const float StuckNearExitDist = 15f;

    private const int PathfindNodes = 20000;

    // If we've been executing for more than this without despawning, give up
    // and let the behavior's fallback timeout handle it.
    private const long TaskTimeoutMs = 60000;

    private EntityBehaviorTravellingTrader _behavior;

    private VillagerAStarNew _pathfinder;

    private List<VillagerPathNode> _path;

    private int _pathIndex;

    private bool _stuck;

    private Vec3d _lastPos;

    private long _stuckCheckTime;

    private int _timesStuck;

    private long _taskStartedMs;

    // Cached so proximity check doesn't need to re-resolve the village each tick.
    private Vec3d _exitTargetVec;

    public AiTaskTravellingTraderLeave(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
        : base(entity, taskConfig, aiConfig)
    {
        _pathfinder = new VillagerAStarNew(entity.World.GetCachingBlockAccessor(synchronize: false, relight: false), entity.World, entity);
    }

    public override bool ShouldExecute()
    {
        if (_behavior == null)
        {
            _behavior = entity.GetBehavior<EntityBehaviorTravellingTrader>();
        }
        return _behavior?.IsLeaving ?? false;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        _path = null;
        _pathIndex = 0;
        _stuck = false;
        _timesStuck = 0;
        _exitTargetVec = null;
        _taskStartedMs = entity.World.ElapsedMilliseconds;

        if (_behavior != null)
        {
            _behavior.IsAtStall = false;
        }

        string villageId = _behavior?.VillageId;
        if (villageId == null)
        {
            _stuck = true;
            return;
        }

        Village village = entity.World.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(villageId);
        if (village == null)
        {
            _stuck = true;
            return;
        }

        // If already outside 55 blocks of village centre, just despawn now.
        // EffectiveCenter: village.Pos.Y is unreliable (worldgen stores ~20), which
        // would inflate this 3D distance by ~90 blocks and trip the check instantly.
        double currentDist = entity.Pos.XYZ.DistanceTo(village.EffectiveCenter().ToVec3d());
        if (currentDist > ExitDist)
        {
            entity.World.Logger.Debug($"[TravellingTraderLeave:{entity.EntityId}] Already outside village - notifying behavior.");
            _behavior?.NotifyPathComplete();
            _stuck = true;
            return;
        }

        BlockPos exitTarget = FindExitTarget(village);
        if (exitTarget == null)
        {
            entity.World.Logger.Debug($"[TravellingTraderLeave:{entity.EntityId}] No valid exit surface found - will retry.");
            return;
        }

        _exitTargetVec = exitTarget.ToVec3d().Add(0.5, 0.0, 0.5);

        _pathfinder.blockAccessor.Begin();
        _pathfinder.SetEntityCollisionBox(entity);
        BlockPos startPos = _pathfinder.GetStartPos(entity.Pos.XYZ);
        _path = _pathfinder.FindPath(startPos, exitTarget, PathfindNodes);
        _pathfinder.blockAccessor.Commit();

        if (_path == null || _path.Count == 0)
        {
            entity.World.Logger.Debug($"[TravellingTraderLeave:{entity.EntityId}] No path to exit target {exitTarget} - will retry.");
        }
        else
        {
            _pathIndex = 0;
            _lastPos = entity.Pos.XYZ.Clone();
            _stuckCheckTime = entity.World.ElapsedMilliseconds;
            entity.World.Logger.Debug($"[TravellingTraderLeave:{entity.EntityId}] Departing toward {exitTarget} ({_path.Count} nodes).");
        }
    }

    public override bool ContinueExecute(float dt)
    {
        if (_stuck)
            return false;

        if (_path == null)
            return false;

        // Proximity check - close enough to the exit target, just despawn.
        if (_exitTargetVec != null && entity.Pos.XYZ.DistanceTo(_exitTargetVec) <= ProximityDespawnDist)
        {
            entity.World.Logger.Debug($"[TravellingTraderLeave:{entity.EntityId}] Within {ProximityDespawnDist} blocks of exit target - despawning.");
            entity.Controls.StopAllMovement();
            _behavior?.DespawnSelf();
            return false;
        }

        // Per-execution timeout.
        if (entity.World.ElapsedMilliseconds - _taskStartedMs > TaskTimeoutMs)
        {
            entity.World.Logger.Debug($"[TravellingTraderLeave:{entity.EntityId}] Task timeout - restarting.");
            return false;
        }

        CheckIfStuck();
        if (_stuck)
            return false;

        if (_pathIndex >= _path.Count)
        {
            entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
            entity.Controls.StopAllMovement();
            if (animMeta != null)
            {
                entity.AnimManager.StopAnimation(animMeta.Code);
            }
            _behavior?.NotifyPathComplete();
            return false;
        }

        HandlePathTraversal();
        return true;
    }

    public override void FinishExecute(bool cancelled)
    {
        base.FinishExecute(cancelled);
        entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
        entity.Controls.StopAllMovement();
        if (animMeta != null)
        {
            entity.AnimManager.StopAnimation(animMeta.Code);
        }
        entity.Pos.Motion.X = 0.0;
        entity.Pos.Motion.Z = 0.0;
    }

    private BlockPos FindExitTarget(Village village)
    {
        IBlockAccessor ba = entity.World.BlockAccessor;
        Vec3d myPos = entity.Pos.XYZ;
        Vec3d centre = village.Pos.ToVec3d();
        // FindSurface only probes +/-5 blocks, so it must start near the actual
        // surface - village.Pos.Y is unreliable (worldgen stores ~20).
        int centerY = village.EffectiveCenterY();
        double baseAngle = Math.Atan2(myPos.Z - centre.Z, myPos.X - centre.X);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            double spread = (double)attempt / 9.0 * Math.PI;
            double sign = (attempt % 2 == 0) ? 1.0 : -1.0;
            double angle = baseAngle + sign * spread;
            int tx = village.Pos.X + (int)(Math.Cos(angle) * ExitDist);
            int tz = village.Pos.Z + (int)(Math.Sin(angle) * ExitDist);
            BlockPos surface = FindSurface(ba, new BlockPos(tx, centerY, tz, 0));
            if (surface != null)
            {
                return surface;
            }
        }
        return null;
    }

    private static BlockPos FindSurface(IBlockAccessor ba, BlockPos pos)
    {
        BlockPos check = pos.Copy();
        for (int dy = 5; dy >= -5; dy--)
        {
            check.Y = pos.Y + dy;
            Block floor = ba.GetBlock(check);
            Block space = ba.GetBlock(check.UpCopy());
            Block head = ba.GetBlock(check.UpCopy().UpCopy());
            bool hasFloor = floor.CollisionBoxes != null && floor.CollisionBoxes.Length != 0;
            bool spaceClear = space.CollisionBoxes == null || space.CollisionBoxes.Length == 0;
            bool headClear = head.CollisionBoxes == null || head.CollisionBoxes.Length == 0;
            if (hasFloor && spaceClear && headClear)
            {
                return check.UpCopy();
            }
        }
        return null;
    }

    private void HandlePathTraversal()
    {
        if (_pathIndex >= _path.Count)
            return;

        VillagerPathNode node = _path[_pathIndex];
        Vec3d nodePos = node.BlockPos.ToVec3d().Add(0.5, 0.0, 0.5);
        Vec3d myPos = entity.Pos.XYZ;
        double dx = myPos.X - nodePos.X;
        double dz = myPos.Z - nodePos.Z;
        if (Math.Sqrt(dx * dx + dz * dz) < 0.5)
        {
            _pathIndex++;
            if (_pathIndex >= _path.Count)
                return;
        }
        if (_pathIndex < _path.Count)
        {
            Vec3d nextPos = _path[_pathIndex].BlockPos.ToVec3d().Add(0.5, 0.0, 0.5);
            Vec3d dir = nextPos.Clone().Sub(myPos);
            dir.Y = 0.0;
            dir = dir.Normalize();
            entity.Pos.Yaw = (float)Math.Atan2(dir.X, dir.Z);
            entity.Controls.WalkVector.Set(dir.X * MoveSpeed, 0.0, dir.Z * MoveSpeed);
            if (animMeta != null && !entity.AnimManager.IsAnimationActive(animMeta.Code))
            {
                entity.AnimManager.StartAnimation(animMeta);
            }
        }
    }

    private void CheckIfStuck()
    {
        long now = entity.World.ElapsedMilliseconds;
        if (now - _stuckCheckTime < 3000)
            return;

        Vec3d myPos = entity.Pos.XYZ;

        // If stuck near the exit target, just despawn - no point retrying.
        if (_exitTargetVec != null && myPos.DistanceTo(_exitTargetVec) <= StuckNearExitDist)
        {
            entity.World.Logger.Debug($"[TravellingTraderLeave:{entity.EntityId}] Stuck within {StuckNearExitDist} blocks of exit - despawning.");
            _behavior?.DespawnSelf();
            _stuck = true;
            return;
        }

        if (_lastPos != null && myPos.DistanceTo(_lastPos) < 0.5)
        {
            if (++_timesStuck >= 4)
            {
                _stuck = true;
                _timesStuck = 0;
                entity.World.Logger.Debug($"[TravellingTraderLeave:{entity.EntityId}] Stuck while departing - will retry with new direction.");
            }
        }
        else
        {
            _timesStuck = 0;
        }

        _lastPos = myPos.Clone();
        _stuckCheckTime = now;
    }
}