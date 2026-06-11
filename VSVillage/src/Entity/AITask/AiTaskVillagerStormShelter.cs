using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage;

public class AiTaskVillagerStormShelter : AiTaskBase
{
	private const float RunSpeed = 0.014f;

	private const string ShelterAnimation = "idlelook";

	private VillagerAStarNew pathfinder;

	private List<VillagerPathNode> currentPath;

	private int currentPathIndex;

	private Vec3d targetPos;

	private bool stuck;

	private bool reachedShelter;

	private Vec3d lastPosition;

	private long stuckCheckTime;

	private int timesStuck;

	public AiTaskVillagerStormShelter(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
		: base(entity, taskConfig, aiConfig)
	{
		pathfinder = new VillagerAStarNew(entity.World.GetCachingBlockAccessor(synchronize: false, relight: false), entity.World, entity);
	}

	public override bool ShouldExecute()
	{

		if (!IsTemporalStormActive())
		{
			return false;
		}
		if (reachedShelter)
		{
			return true;
		}
		return cooldownUntilMs <= entity.World.ElapsedMilliseconds;
	}

	public override void StartExecute()
	{
		base.StartExecute();
		stuck = false;
		reachedShelter = false;
		currentPath = null;
		currentPathIndex = 0;
		timesStuck = 0;
		targetPos = null;
		EntityBehaviorVillager villager = entity.GetBehavior<EntityBehaviorVillager>();
		Village village = villager?.Village;
		if (village != null)
		{
			targetPos = FindBarracksPos(village);
		}
		if (targetPos == null && villager?.Bed != null)
		{
			targetPos = FindStandingPosNear(villager.Bed.ToVec3d());
		}
		if (!(targetPos == null))
		{
			pathfinder.blockAccessor.Begin();
			pathfinder.SetEntityCollisionBox(entity);
			BlockPos start = pathfinder.GetStartPos(entity.Pos.XYZ);
			currentPath = pathfinder.FindPath(start, targetPos.AsBlockPos, 10000);
			pathfinder.blockAccessor.Commit();
			if (currentPath == null || currentPath.Count == 0)
			{
				// Tier-3 fallback: storm exposure kills villagers. Teleport directly to
				// the shelter spot or the mayor station rather than letting them stand
				// in the open until something else picks them up.
				if (IsPositionSafe(targetPos))
				{
					entity.TeleportTo(targetPos);
					ArriveAtShelter();
					return;
				}
				Vec3d mayorStand = village?.Pos != null ? FindStandingPosNear(village.EffectiveCenter().ToVec3d()) : null;
				if (mayorStand != null && IsPositionSafe(mayorStand))
				{
					entity.TeleportTo(mayorStand);
					targetPos = mayorStand;
					ArriveAtShelter();
					return;
				}
				stuck = true;
				return;
			}
			currentPathIndex = 0;
			lastPosition = entity.Pos.XYZ.Clone();
			stuckCheckTime = entity.World.ElapsedMilliseconds;
		}
	}

	public override bool ContinueExecute(float dt)
	{
		if (reachedShelter)
		{
			if (!IsTemporalStormActive())
			{
				return false;
			}
			PlayShelterAnimation();
			return true;
		}
		if (targetPos == null || stuck || currentPath == null)
		{
			return false;
		}
		if (!IsTemporalStormActive())
		{
			return false;
		}
		CheckIfStuck();
		if (currentPathIndex >= currentPath.Count || entity.Pos.SquareDistanceTo(targetPos) < 2.25)
		{
			ArriveAtShelter();
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
		if (animMeta != null)
		{
			entity.AnimManager.StopAnimation(animMeta.Code);
		}
		DoorPathHelper.CloseOpenDoorsAlongPath(entity, currentPath);
		targetPos = null;
		currentPath = null;
		currentPathIndex = 0;
		reachedShelter = false;
		lastPosition = null;
		timesStuck = 0;
	}

	private bool IsTemporalStormActive()
	{
		// Admin drill flag overrides the real stability system for testing.
		if (VillageManager.StormDrillActive) return true;
		SystemTemporalStability tempSys = entity.World.Api.ModLoader.GetModSystem<SystemTemporalStability>();
		return tempSys != null && tempSys.StormData?.nowStormActive == true;
	}

	private Vec3d FindBarracksPos(Village village)
	{
		// Collect all soldier workstations (barracks anchor points).
		List<VillagerWorkstation> barracks = village.Workstations.Values
			.Where(ws => ws.Profession == EnumVillagerProfession.soldier)
			.ToList();

		if (barracks.Count == 0) return null;

		// Pick the nearest barracks workstation so distant ones aren't preferred.
		Vec3d myPos = entity.Pos.XYZ;
		VillagerWorkstation nearest = barracks
			.OrderBy(ws => ws.Pos.DistanceTo(myPos.AsBlockPos))
			.First();

		// Get the room the workstation is in so we never place villagers outside.
		RoomRegistry roomReg = entity.Api.ModLoader.GetModSystem<RoomRegistry>();
		Room room = null;
		try { room = roomReg?.GetRoomForPosition(nearest.Pos); } catch { }

		System.Random rng = entity.World.Rand;

		// Spread villagers inside the barracks room (±3 blocks of workstation).
		// Each candidate must be clear to stand in AND inside the same room.
		for (int attempt = 0; attempt < 12; attempt++)
		{
			int ox = rng.Next(-3, 4);
			int oz = rng.Next(-3, 4);
			BlockPos candidateBlock = nearest.Pos.AddCopy(ox, 0, oz);

			// Reject if the candidate is outside the barracks room.
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

		// Fallback: direct neighbour of the workstation (original behaviour).
		return FindStandingPosNear(nearest.Pos.ToVec3d());
	}

	private Vec3d FindStandingPosNear(Vec3d centre)
	{
		IBlockAccessor ba = entity.World.BlockAccessor;
		BlockPos bp = centre.AsBlockPos;
		BlockFacing[] hORIZONTALS = BlockFacing.HORIZONTALS;
		foreach (BlockFacing facing in hORIZONTALS)
		{
			BlockPos neighbor = bp.AddCopy(facing.Normali.X, 0, facing.Normali.Z);
			Block atPos = ba.GetBlock(neighbor);
			Block above = ba.GetBlock(neighbor.UpCopy());
			Block below = ba.GetBlock(neighbor.DownCopy());
			bool posClear = atPos.CollisionBoxes == null || atPos.CollisionBoxes.Length == 0;
			bool headClear = above.CollisionBoxes == null || above.CollisionBoxes.Length == 0;
			bool grounded = below.CollisionBoxes != null && below.CollisionBoxes.Length != 0;
			if (posClear && headClear && grounded)
			{
				return neighbor.ToVec3d().Add(0.5, 0.0, 0.5);
			}
		}
		return centre.Add(0.5, 1.0, 0.5);
	}

	private void ArriveAtShelter()
	{
		entity.Controls.WalkVector.Set(0.0, 0.0, 0.0);
		entity.Controls.StopAllMovement();
		entity.Pos.Motion.Set(0.0, 0.0, 0.0);
		if (animMeta != null)
		{
			entity.AnimManager.StopAnimation(animMeta.Code);
		}
		reachedShelter = true;
		PlayShelterAnimation();
	}

	private void PlayShelterAnimation()
	{
		if (!entity.AnimManager.IsAnimationActive("idlelook"))
		{
			entity.AnimManager.StartAnimation(new AnimationMetaData
			{
				Animation = "idlelook",
				Code = "idlelook",
				AnimationSpeed = 1.2f,
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
				if (next.IsDoor)
				{
					DoorPathHelper.ToggleDoor(entity, next.BlockPos, opened: true);
				}
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
			dir = dir.Normalize();
			entity.Pos.Yaw = (float)Math.Atan2(dir.X, dir.Z);
			entity.Controls.WalkVector.Set(dir.X * 0.014f, 0.0, dir.Z * 0.014f);
			if (animMeta != null && !entity.AnimManager.IsAnimationActive(animMeta.Code))
			{
				entity.AnimManager.StartAnimation(animMeta);
			}
		}
	}

	// Safety check used by the Tier-3 teleport fallback; matches the shape the
	// sleep task uses (clear at, clear above, solid below).
	private bool IsPositionSafe(Vec3d pos)
	{
		if (pos == null) return false;
		IBlockAccessor ba = entity.World.BlockAccessor;
		BlockPos bp = pos.AsBlockPos;
		bool atClear   = ba.GetBlock(bp).CollisionBoxes == null || ba.GetBlock(bp).CollisionBoxes.Length == 0;
		bool headClear = ba.GetBlock(bp.UpCopy()).CollisionBoxes == null || ba.GetBlock(bp.UpCopy()).CollisionBoxes.Length == 0;
		bool grounded  = ba.GetBlock(bp.DownCopy()).CollisionBoxes != null && ba.GetBlock(bp.DownCopy()).CollisionBoxes.Length != 0;
		return atClear && headClear && grounded;
	}

	private void CheckIfStuck()
	{
		long now = entity.World.ElapsedMilliseconds;
		if (now - stuckCheckTime < 3000)
		{
			return;
		}
		Vec3d myPos = entity.Pos.XYZ;
		if (lastPosition != null && (double)myPos.DistanceTo(lastPosition) < 0.5)
		{
			if (++timesStuck >= 4)
			{
				stuck = true;
				timesStuck = 0;
			}
		}
		else
		{
			timesStuck = 0;
		}
		lastPosition = myPos.Clone();
		stuckCheckTime = now;
	}
}
