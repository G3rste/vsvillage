using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage;

public class AiTaskVillagerShepherdWander : AiTaskBase
{
	private Vec3d targetPos;

	private float moveSpeed = 0.008f;

	private float wanderRange = 25f;

	private VillagerAStarNew pathfinder;

	private List<VillagerPathNode> currentPath;

	private int currentPathIndex;

	private bool stuck;

	private Vec3d lastPosition;

	private long stuckCheckTime;

	private int timesStuck;

	// Lore / hostile mob prefixes — shepherd never wanders toward these.
	private static readonly string[] LorePrefixes =
	{
		"bell-", "bellmini-", "bowtorn-", "drifter-", "locust-", "shiver-"
	};
	private static readonly string[] LoreExact = { "mechhelper" };

	public AiTaskVillagerShepherdWander(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
		: base(entity, taskConfig, aiConfig)
	{
		if (taskConfig["moveSpeed"] != null)
		{
			moveSpeed = taskConfig["moveSpeed"].AsFloat(0.008f);
		}
		if (taskConfig["wanderRange"] != null)
		{
			wanderRange = taskConfig["wanderRange"].AsFloat(25f);
		}
		pathfinder = new VillagerAStarNew(entity.World.GetCachingBlockAccessor(synchronize: false, relight: false), entity.World, entity);
	}

	public override bool ShouldExecute()
	{
		if (!IsShepherd())
		{
			return false;
		}
		if (duringDayTimeFrames != null && duringDayTimeFrames.Length != 0 && !IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World))
		{
			return false;
		}
		if (cooldownUntilMs > entity.World.ElapsedMilliseconds)
		{
			return false;
		}
		return entity.World.Rand.NextDouble() < 0.65;
	}

	public override void StartExecute()
	{
		base.StartExecute();
		targetPos = null;
		stuck = false;
		currentPath = null;
		currentPathIndex = 0;
		timesStuck = 0;
		if (entity.World.Rand.NextDouble() < 0.6)
		{
			targetPos = FindNearbyLivestock();
		}
		if (targetPos == null)
		{
			targetPos = FindNearbyTrough();
		}
		if (!(targetPos == null))
		{
			pathfinder.blockAccessor.Begin();
			pathfinder.SetEntityCollisionBox(entity);
			BlockPos startPos = pathfinder.GetStartPos(entity.Pos.XYZ);
			currentPath = pathfinder.FindPath(startPos, targetPos.AsBlockPos, 800);
			pathfinder.blockAccessor.Commit();
			if (currentPath == null || currentPath.Count == 0)
			{
				targetPos = null;
				stuck = true;
			}
			else
			{
				currentPathIndex = 0;
				lastPosition = entity.Pos.XYZ.Clone();
				stuckCheckTime = entity.World.ElapsedMilliseconds;
			}
		}
	}

	public override bool ContinueExecute(float dt)
	{
		if (targetPos == null || stuck || currentPath == null)
		{
			return false;
		}
		if (duringDayTimeFrames != null && duringDayTimeFrames.Length != 0 && !IntervalUtil.matchesCurrentTime(duringDayTimeFrames, entity.World))
		{
			return false;
		}
		CheckIfStuck();
		if (currentPathIndex >= currentPath.Count)
		{
			return false;
		}
		if (entity.Pos.SquareDistanceTo(targetPos) < 3.0)
		{
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
		targetPos = null;
		currentPath = null;
		currentPathIndex = 0;
		lastPosition = null;
		timesStuck = 0;
	}

	private Vec3d FindNearbyLivestock()
	{
		Entity[] nearby = entity.World.GetEntitiesAround(entity.Pos.XYZ, wanderRange, 6f, (Entity e) => e != entity && e.Alive && IsWardAnimal(e));
		if (nearby.Length == 0)
		{
			return null;
		}
		Entity target = nearby[entity.World.Rand.Next(nearby.Length)];
		Vec3d animalPos = target.Pos.XYZ;
		return new Vec3d(animalPos.X + (entity.World.Rand.NextDouble() - 0.5) * 4.0, animalPos.Y, animalPos.Z + (entity.World.Rand.NextDouble() - 0.5) * 4.0);
	}

	private Vec3d FindNearbyTrough()
	{
		return entity.Api.ModLoader.GetModSystem<POIRegistry>().GetNearestPoi(entity.Pos.XYZ, wanderRange, (IPointOfInterest p) => p is BlockEntityTrough)?.Position;
	}

	private static bool IsLoreEntity(string path)
	{
		if (path == null) return false;
		foreach (string e in LoreExact)
			if (path == e) return true;
		foreach (string p in LorePrefixes)
			if (path.StartsWith(p)) return true;
		return false;
	}

	/// <summary>
	/// Returns true for any creature the shepherd should tend to: alive, not a
	/// player, not a villager, not a lore/hostile mob.  No species whitelist —
	/// modded animals are included automatically.
	/// </summary>
	private bool IsWardAnimal(Entity e)
	{
		if (e?.Code?.Path == null) return false;
		if (e.Code?.Path == "player") return false;
		if (e.Code.Domain == "vsvillage") return false;
		return !IsLoreEntity(e.Code.Path);
	}

	private bool IsShepherd()
	{
		EntityAgent entityAgent = entity;
		return entityAgent != null && entityAgent.Code?.Path?.EndsWith("-shepherd") == true;
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
		}
		if (currentPathIndex < currentPath.Count)
		{
			VillagerPathNode nextNode = currentPath[currentPathIndex];
			Vec3d nextPos = nextNode.BlockPos.ToVec3d().Add(0.5, 0.0, 0.5);
			Vec3d dir = nextPos.Clone().Sub(myPos);
			dir.Y = 0.0;
			dir = dir.Normalize();
			entity.Pos.Yaw = (float)Math.Atan2(dir.X, dir.Z);
			entity.Controls.WalkVector.Set(dir.X * (double)moveSpeed, 0.0, dir.Z * (double)moveSpeed);
			if (animMeta != null && !entity.AnimManager.IsAnimationActive(animMeta.Code))
			{
				entity.AnimManager.StartAnimation(animMeta);
			}
		}
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
			timesStuck++;
			if (timesStuck >= 4)
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
