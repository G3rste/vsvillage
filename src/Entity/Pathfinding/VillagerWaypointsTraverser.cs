using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class VillagerWaypointsTraverser : PathTraverserBase
    {
        float minTurnAnglePerSec;
        float maxTurnAnglePerSec;
        VillagerPathfind villagerPathfind;
        public List<Vec3d> waypoints;

        public int waypointToReachIndex;
        float sqDistToTarget;
        Vec3d prevPos = new();
        Vec3d targetVec = new();
        public override Vec3d CurrentTarget
        {
            get
            {
                if (waypoints == null)
                {
                    waypoints = new List<Vec3d>() { entity.ServerPos.XYZ };
                }
                return waypoints[waypoints.Count - 1];
            }
        }

        public VillagerWaypointsTraverser(EntityAgent entity) : base(entity)
        {
            if (entity?.Properties.Server?.Attributes?.GetTreeAttribute("pathfinder") != null)
            {
                minTurnAnglePerSec = (float)entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetDecimal("minTurnAnglePerSec", 250);
                maxTurnAnglePerSec = (float)entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetDecimal("maxTurnAnglePerSec", 450);
            }
            else
            {
                minTurnAnglePerSec = 250;
                maxTurnAnglePerSec = 450;
            }

            villagerPathfind = new VillagerPathfind(entity.Api as ICoreServerAPI);
        }

        public override bool NavigateTo(Vec3d target, float movingSpeed, float targetDistance, Action OnGoalReached, Action OnStuck, bool giveUpWhenNoPath = false, int searchDepth = 999, int mhdistanceTolerance = 0)
        {
            BlockPos startBlockPos = villagerPathfind.GetStartPos(entity.ServerPos.XYZ);
            waypointToReachIndex = 0;

            var bh = entity.GetBehavior<EntityBehaviorControlledPhysics>();
            float stepHeight = bh?.stepHeight ?? 1.01f;
            bool canFallDamage = entity.Properties.FallDamage;


            waypoints = villagerPathfind.FindPathAsWaypoints(startBlockPos, target.AsBlockPos, canFallDamage ? 8 : 4, stepHeight, entity.GetBehavior<EntityBehaviorVillager>()?.Village);

            if (waypoints == null)
            {
                if (giveUpWhenNoPath)
                {
                    return false;
                }

                waypoints = new List<Vec3d>();

                entity.OnNoPath(target);

            }

            waypoints.Add(target);


            return WalkTowards(target, movingSpeed, targetDistance, OnGoalReached, OnStuck);
        }


        public override bool WalkTowards(Vec3d target, float movingSpeed, float targetDistance, Action OnGoalReached, Action OnStuck)
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                waypoints = new List<Vec3d>() { target };
            }

            // entity.World.HighlightBlocks(entity.World.GetPlayersAround(entity.ServerPos.XYZ, 50, 50)[0], (int)entity.EntityId, waypoints.ConvertAll(waypoint=>waypoint.AsBlockPos).ToList());

            return base.WalkTowards(target, movingSpeed, targetDistance, OnGoalReached, OnStuck);
        }

        protected override bool BeginGo()
        {
            entity.Controls.Forward = true;
            entity.ServerControls.Forward = true;
            curTurnRadPerSec = minTurnAnglePerSec + (float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec);
            curTurnRadPerSec *= GameMath.DEG2RAD * 50 * movingSpeed;

            stuckCounter = 0;
            waypointToReachIndex = 0;
            toggleDoor(entity.ServerPos.AsBlockPos, false);
            toggleDoor(waypoints[waypointToReachIndex].AsBlockPos, false);

            // very important (otherwise the target is set to the last waypoint which can fuck stuff up)
            target = waypoints[0];

            prevPos.Set(entity.ServerPos);

            return true;
        }

        public override void OnGameTick(float dt)
        {
            if (!Active) return;

            bool nearHorizontally = false;
            int offset = 0;
            bool nearAllDirs =
                IsNearTarget(offset++, ref nearHorizontally)
                || IsNearTarget(offset++, ref nearHorizontally)
                || IsNearTarget(offset++, ref nearHorizontally)
            ;

            EntityControls controls = entity.MountedOn == null ? entity.Controls : entity.MountedOn.Controls;
            if (controls == null) return;

            if (nearAllDirs)
            {
                waypointToReachIndex += offset;
                if (waypointToReachIndex >= waypoints.Count - targetDistance)
                {
                    Stop();
                    OnGoalReached?.Invoke();
                    return;
                }

                target = waypoints[Math.Min(waypoints.Count - 1, waypointToReachIndex)];
                toggleDoor(waypoints[waypointToReachIndex].AsBlockPos, false);
                toggleDoor(waypoints[waypointToReachIndex - 1].AsBlockPos, false);
                if (waypointToReachIndex > 2)
                {
                    toggleDoor(waypoints[waypointToReachIndex - 2].AsBlockPos, false);
                    toggleDoor(waypoints[waypointToReachIndex - 3].AsBlockPos, true);
                }
                if (target.Y < entity.ServerPos.Y && target.X == entity.ServerPos.X && target.Z == entity.ServerPos.Z)
                {
                    controls.Sneak = true;
                }
                else
                {
                    controls.Sneak = false;
                }
            }


            double distsq = entity.ServerPos.SquareDistanceTo(prevPos);
            bool stuck = distsq < 0.01 * 0.01;
            stuckCounter = stuck ? stuckCounter + 1 : 0;
            if (stuckCounter > 40)
            {
                //entity.World.SpawnParticles(10, ColorUtil.WhiteArgb, prevPos, prevPos, new Vec3f(0, 0, 0), new Vec3f(0, -1, 0), 1, 1);
                Stop();
                OnStuck?.Invoke();
                return;
            }

            targetVec.Set(
                (float)(target.X - entity.ServerPos.X),
                (float)(target.Y - entity.ServerPos.Y),
                (float)(target.Z - entity.ServerPos.Z)
            );
            targetVec.Normalize();

            float desiredYaw = 0;

            if (sqDistToTarget >= 0.01)
            {
                desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);
            }



            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            float turnSpeed = curTurnRadPerSec * dt * GlobalConstants.OverallSpeedMultiplier;
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -turnSpeed, turnSpeed);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;



            double cosYaw = Math.Cos(entity.ServerPos.Yaw);
            double sinYaw = Math.Sin(entity.ServerPos.Yaw);
            controls.WalkVector.Set(sinYaw, GameMath.Clamp(targetVec.Y, -1, 1), cosYaw);
            controls.WalkVector.Mul(movingSpeed * GlobalConstants.OverallSpeedMultiplier);// * speedMul);

            // Make it walk along the wall, but not walk into the wall, which causes it to climb
            if (entity.Properties.RotateModelOnClimb && entity.Controls.IsClimbing && entity.ClimbingOnFace != null && entity.Alive)
            {
                BlockFacing facing = entity.ClimbingOnFace;
                if (Math.Sign(facing.Normali.X) == Math.Sign(controls.WalkVector.X))
                {
                    controls.WalkVector.X = 0;
                }

                if (Math.Sign(facing.Normali.Z) == Math.Sign(controls.WalkVector.Z))
                {
                    controls.WalkVector.Z = 0;
                }
            }

            //   entity.World.SpawnParticles(0.3f, ColorUtil.WhiteAhsl, target, target, new Vec3f(), new Vec3f(), 0.1f, 0.1f, 3f, EnumParticleModel.Cube);

            if (entity.Properties.Habitat == EnumHabitat.Underwater)
            {
                controls.FlyVector.Set(controls.WalkVector);

                Vec3d pos = entity.Pos.XYZ;
                Block inblock = entity.World.BlockAccessor.GetBlock(new BlockPos((int)pos.X, (int)(pos.Y), (int)pos.Z, 0));
                Block aboveblock = entity.World.BlockAccessor.GetBlock(new BlockPos((int)pos.X, (int)(pos.Y + 1), (int)pos.Z, 0));
                float waterY = (int)pos.Y + inblock.LiquidLevel / 8f + (aboveblock.IsLiquid() ? 9 / 8f : 0);
                float bottomSubmergedness = waterY - (float)pos.Y;

                // 0 = at swim line  1 = completely submerged
                float swimlineSubmergedness = GameMath.Clamp(bottomSubmergedness - ((float)entity.SwimmingOffsetY), 0, 1);
                swimlineSubmergedness = 1f - Math.Min(1f, swimlineSubmergedness + 0.5f);
                if (swimlineSubmergedness > 0f)
                {
                    //Push the fish back underwater if part is poking out ...  (may need future adaptation for sharks[?], probably by changing SwimmingOffsetY)
                    controls.FlyVector.Y = GameMath.Clamp(controls.FlyVector.Y, -0.04f, -0.02f) * (1f - swimlineSubmergedness);
                }
                else
                {
                    float factor = movingSpeed * GlobalConstants.OverallSpeedMultiplier / (float)Math.Sqrt(targetVec.X * targetVec.X + targetVec.Z * targetVec.Z);
                    controls.FlyVector.Y = targetVec.Y * factor;
                }
            }
            else if (entity.Swimming)
            {
                controls.FlyVector.Set(controls.WalkVector);

                Vec3d pos = entity.Pos.XYZ;
                Block inblock = entity.World.BlockAccessor.GetBlock(new BlockPos((int)pos.X, (int)(pos.Y), (int)pos.Z, 0));
                Block aboveblock = entity.World.BlockAccessor.GetBlock(new BlockPos((int)pos.X, (int)(pos.Y + 1), (int)pos.Z, 0));
                float waterY = (int)pos.Y + inblock.LiquidLevel / 8f + (aboveblock.IsLiquid() ? 9 / 8f : 0);
                float bottomSubmergedness = waterY - (float)pos.Y;

                // 0 = at swim line
                // 1 = completely submerged
                float swimlineSubmergedness = GameMath.Clamp(bottomSubmergedness - ((float)entity.SwimmingOffsetY), 0, 1);
                swimlineSubmergedness = Math.Min(1, swimlineSubmergedness + 0.5f);
                controls.FlyVector.Y = GameMath.Clamp(controls.FlyVector.Y, 0.02f, 0.04f) * swimlineSubmergedness;


                if (entity.CollidedHorizontally)
                {
                    controls.FlyVector.Y = 0.05f;
                }
            }
            prevPos.Set(entity.ServerPos);
        }

        public override bool NavigateTo_Async(Vec3d target, float movingSpeed, float targetDistance, Action OnGoalReached, Action OnStuck, Action OnNoPath = null, int searchDepth = 10000, int mhdistanceTolerance = 0)
        {
            return NavigateTo(target, movingSpeed, targetDistance, OnGoalReached, OnStuck, true, searchDepth, mhdistanceTolerance);
        }

        private bool toggleDoor(BlockPos pos, bool shouldBeOpen)
        {
            if (pos.HorizontalManhattenDistance(entity.ServerPos.AsBlockPos) > 3)
            {
                return false;
            }

            var doorBehavior = BlockBehaviorDoor.getDoorAt(entity.World, pos);
            if (doorBehavior != null && doorBehavior.Opened == shouldBeOpen)
            {
                doorBehavior.ToggleDoorState(null, !shouldBeOpen);
                return true;
            }
            if (entity.World.BlockAccessor.GetBlock(pos) is BlockBaseDoor doorBlock && doorBlock.IsOpened() == shouldBeOpen)
            {
                doorBlock.OnBlockInteractStart(entity.World, null, new BlockSelection(pos, BlockFacing.UP, doorBlock));
                return true;
            }
            return false;
        }
        bool IsNearTarget(int waypointOffset, ref bool nearHorizontally)
        {
            if (waypoints.Count - 1 < waypointToReachIndex + waypointOffset) return false;

            int wayPointIndex = Math.Min(waypoints.Count - 1, waypointToReachIndex + waypointOffset);
            Vec3d target = waypoints[wayPointIndex];

            sqDistToTarget = Math.Min(
                Math.Min(target.SquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z), target.SquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Y - 1, entity.ServerPos.Z)),       // One block above is also ok
                target.SquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Y + 0.5f, entity.ServerPos.Z) // Half a block below is also okay
            );
            double horsqDistToTarget = target.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z);

            nearHorizontally |= horsqDistToTarget < 1;

            return sqDistToTarget < Math.Min(0.3, targetDistance * targetDistance);
        }

        public override void Stop()
        {
            for (int i = (waypoints?.Count ?? 0) - 1; i >= 0 && i >= waypointToReachIndex - 1; i--)
            {
                toggleDoor(waypoints[i].AsBlockPos, true);
            }
            Active = false;
            entity.Controls.Forward = false;
            entity.ServerControls.Forward = false;
            entity.Controls.WalkVector.Set(0, 0, 0);
            entity.Controls.Sneak = false;
            stuckCounter = 0;
        }

        public override void Retarget()
        {
            Active = true;

            waypointToReachIndex = waypoints.Count - 1;
        }
    }
}