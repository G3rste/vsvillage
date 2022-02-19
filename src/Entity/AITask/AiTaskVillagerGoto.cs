using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.Essentials;
using Vintagestory.API.Server;
using System.Collections.Generic;
using Vintagestory.API.Client;

namespace VsVillage
{
    public class AiTaskVillagerGoto : AiTaskBase
    {
        public Vec3d MainTarget;

        bool done;
        float moveSpeed = 0.03f;
        float targetDistance = 0.5f;

        int searchDepth = 5000;

        protected VillagerWaypointsTraverser villagerPathTraverser;

        public AiTaskVillagerGoto(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }
            villagerPathTraverser = entity.GetBehavior<EntityBehaviorAlternatePathtraverser>().villagerWaypointsTraverser;
        }

        private double moveDownToFloor(int x, double y, int z)
        {
            int tries = 5;
            while (tries-- > 0)
            {
                Block block = world.BlockAccessor.GetBlock(x, (int)y, z);
                if (block.SideSolid[BlockFacing.UP.Index]) return y + 1;
                y--;
            }

            return -1;
        }


        public override bool ShouldExecute()
        {
            return MainTarget != null;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            done = false;
            bool ok = villagerPathTraverser.NavigateTo(MainTarget, moveSpeed, targetDistance, OnGoalReached, OnStuck, true, searchDepth);
            
            var sapi = entity.Api as ICoreServerAPI;
            var stolentraverser = villagerPathTraverser as VillagerWaypointsTraverser;
        }

        public override bool ContinueExecute(float dt)
        {
            if (MainTarget == null) { return false; }
            if (entity.Controls.IsClimbing && entity.Properties.CanClimbAnywhere && entity.ClimbingOnFace != null)
            {
                BlockFacing facing = entity.ClimbingOnFace;

                if (Math.Sign(facing.Normali.X) == Math.Sign(villagerPathTraverser.CurrentTarget.X - entity.ServerPos.X))
                {
                    villagerPathTraverser.CurrentTarget.X = entity.ServerPos.X;
                }

                if (Math.Sign(facing.Normali.Z) == Math.Sign(villagerPathTraverser.CurrentTarget.Z - entity.ServerPos.Z))
                {
                    villagerPathTraverser.CurrentTarget.Z = entity.ServerPos.Z;
                }
            }

            if (MainTarget.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z) < 0.5)
            {
                villagerPathTraverser.Stop();
                MainTarget = null;
                return false;
            }

            return !done;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            if (cancelled)
            {
                villagerPathTraverser.Stop();
            }
            else
            {
                MainTarget = null;
            }
        }

        private void OnStuck()
        {
            done = true;
            MainTarget = null;
        }

        private void OnGoalReached()
        {
            done = true;
            MainTarget = null;
        }
    }
}
