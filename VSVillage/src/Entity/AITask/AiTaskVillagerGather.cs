using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsVillage;

// Villagers path to the mayor station and idle there while
// Village.IsGatherActive is true.  The flag is cleared either
// by the "Clear Gather" button / command, or by the auto-timer registered
// when the gather was started (default 3 minutes).
// Priority 9.5 - above sleep (9.0) but below combat (10.0) and storm shelter (11.0).
public class AiTaskVillagerGather : AiTaskBase
{
	private const float WalkSpeed = 0.014f;
	private const double ArrivalDistanceSq = 2.25; // 1.5 block radius

	private VillagerAStarNew pathfinder;
	private List<VillagerPathNode> currentPath;
	private int currentPathIndex;

	private Vec3d targetPos;
	private Vec3d lastPosition;
	private long stuckCheckTime;
	private int timesStuck;

	private bool reachedStation;
	private bool stuck;

	public AiTaskVillagerGather(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
		: base(entity, taskConfig, aiConfig)
	{
		pathfinder = new VillagerAStarNew(entity.World.GetCachingBlockAccessor(synchronize: false, relight: false), entity.World, entity);
	}

	// === Lifecycle ===

	public override bool ShouldExecute()
	{
		EntityBehaviorVillager beh = entity.GetBehavior<EntityBehaviorVillager>();
		if (beh?.Village?.IsGatherActive != true) return false;
		if (reachedStation) return true; // already there - keep idling
		return cooldownUntilMs <= entity.World.ElapsedMilliseconds;
	}

	public override void StartExecute()
	{
		base.StartExecute();
		ResetState();

		EntityBehaviorVillager beh = entity.GetBehavior<EntityBehaviorVillager>();
		Village village = beh?.Village;
		if (village?.Pos == null) { stuck = true; return; }

		targetPos = FindStandingPosNear(village.EffectiveCenter().ToVec3d());

		// Already close enough - skip pathfinding and idle immediately.
		if (entity.Pos.SquareDistanceTo(targetPos) < ArrivalDistanceSq)
		{
			Arrive();
			return;
		}

		pathfinder.blockAccessor.Begin();
		pathfinder.SetEntityCollisionBox(entity);
		BlockPos start = pathfinder.GetStartPos(entity.Pos.XYZ);
		currentPath = pathfinder.FindPath(start, targetPos.AsBlockPos, 10000);
		pathfinder.blockAccessor.Commit();

		if (currentPath == null || currentPath.Count == 0)
		{
			// Could not find a path - give up for this tick.
			stuck = true;
			return;
		}

		currentPathIndex = 0;
		lastPosition = entity.Pos.XYZ.Clone();
		stuckCheckTime = entity.World.ElapsedMilliseconds;
	}

	public override bool ContinueExecute(float dt)
	{
		// Gather was cancelled externally - stop immediately.
		EntityBehaviorVillager beh = entity.GetBehavior<EntityBehaviorVillager>();
		if (beh?.Village?.IsGatherActive != true) return false;

		if (reachedStation)
		{
			entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
			PlayIdleAnimation();
			return true;
		}

		if (targetPos == null || stuck || currentPath == null) return false;

		CheckIfStuck();

		if (currentPathIndex >= currentPath.Count || entity.Pos.SquareDistanceTo(targetPos) < ArrivalDistanceSq)
		{
			Arrive();
			return true;
		}

		HandlePathTraversal();
		return true;
	}

	public override void FinishExecute(bool cancelled)
	{
		base.FinishExecute(cancelled);
		entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
		entity.Controls.StopAllMovement();
		entity.Pos.Motion.Set(0.0, 0.0, 0.0);
		entity.AnimManager.StopAnimation("idlelook");
		if (animMeta != null) entity.AnimManager.StopAnimation(animMeta.Code);
		DoorPathHelper.CloseOpenDoorsAlongPath(entity, currentPath);
		ResetState();
	}

	// === Helpers ===

	private void ResetState()
	{
		reachedStation = false;
		stuck = false;
		currentPath = null;
		currentPathIndex = 0;
		targetPos = null;
		lastPosition = null;
		timesStuck = 0;
	}

	private void Arrive()
	{
		entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
		entity.Controls.StopAllMovement();
		entity.Pos.Motion.Set(0.0, 0.0, 0.0);
		if (animMeta != null) entity.AnimManager.StopAnimation(animMeta.Code);
		reachedStation = true;
		PlayIdleAnimation();
	}

	private void PlayIdleAnimation()
	{
		if (!entity.AnimManager.IsAnimationActive("idlelook"))
		{
			entity.AnimManager.StartAnimation(new AnimationMetaData
			{
				Animation = "idlelook",
				Code = "idlelook",
				AnimationSpeed = 1.0f,
				BlendMode = EnumAnimationBlendMode.Average
			}.Init());
		}
	}

	private void HandlePathTraversal()
	{
		if (currentPath == null || currentPathIndex >= currentPath.Count)
		{
			stuck = true;
			return;
		}

		VillagerPathNode node = currentPath[currentPathIndex];
		Vec3d nodePos = node.BlockPos.ToVec3d().Add(0.5, 0.0, 0.5);
		Vec3d myPos = entity.Pos.XYZ;
		double dx = myPos.X - nodePos.X;
		double dz = myPos.Z - nodePos.Z;

		if (Math.Sqrt(dx * dx + dz * dz) < 0.5)
		{
			currentPathIndex++;
			if (currentPathIndex < currentPath.Count)
			{
				VillagerPathNode next = currentPath[currentPathIndex];
				if (next.IsDoor) DoorPathHelper.ToggleDoor(entity, next.BlockPos, opened: true);
			}
			if (node.IsDoor)
			{
				BlockPos doorPos = node.BlockPos.Copy();
				DoorPathHelper.ScheduleDoorClose(entity, doorPos, 5000);
			}
		}

		if (currentPathIndex < currentPath.Count)
		{
			VillagerPathNode next2 = currentPath[currentPathIndex];
			Vec3d nextPos = next2.BlockPos.ToVec3d().Add(0.5, 0.0, 0.5);
			Vec3d dir = nextPos.Clone().Sub(myPos);
			dir.Y = 0.0;
			if (dir.LengthSq() < 0.001) return;
			dir = dir.Normalize();
			entity.Pos.Yaw = (float)Math.Atan2(dir.X, dir.Z);
			entity.Controls.WalkVector.Set(dir.X * WalkSpeed, 0.0, dir.Z * WalkSpeed);
			if (animMeta != null && !entity.AnimManager.IsAnimationActive(animMeta.Code))
				entity.AnimManager.StartAnimation(animMeta);
		}
	}

	private void CheckIfStuck()
	{
		long now = entity.World.ElapsedMilliseconds;
		if (now - stuckCheckTime < 3000) return;

		Vec3d myPos = entity.Pos.XYZ;
		if (lastPosition != null && myPos.DistanceTo(lastPosition) < 0.5f)
		{
			if (++timesStuck >= 4) { stuck = true; timesStuck = 0; }
		}
		else
		{
			timesStuck = 0;
		}
		lastPosition = myPos.Clone();
		stuckCheckTime = now;
	}


	private Vec3d FindStandingPosNear(Vec3d centre)
	{
		IBlockAccessor ba = entity.World.BlockAccessor;
		BlockPos bp = centre.AsBlockPos;
		foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
		{
			BlockPos neighbor = bp.AddCopy(facing.Normali.X, 0, facing.Normali.Z);
			Block atPos  = ba.GetBlock(neighbor);
			Block above  = ba.GetBlock(neighbor.UpCopy());
			Block below  = ba.GetBlock(neighbor.DownCopy());
			bool posClear  = atPos.CollisionBoxes  == null || atPos.CollisionBoxes.Length  == 0;
			bool headClear = above.CollisionBoxes  == null || above.CollisionBoxes.Length  == 0;
			bool grounded  = below.CollisionBoxes  != null && below.CollisionBoxes.Length  != 0;
			if (posClear && headClear && grounded) return neighbor.ToVec3d().Add(0.5, 0.0, 0.5);
		}
		return centre.Add(0.5, 1.0, 0.5); // fallback: slightly above centre
	}
}
