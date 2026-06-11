using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage;

// Fires when the farmer has no crops to tend.
// Farmer patrols to random village waypoints.
public class AiTaskVillagerFarmerHelp : AiTaskGotoAndInteract
{
    public AiTaskVillagerFarmerHelp(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
        : base(entity, taskConfig, aiConfig) { }

    protected override Vec3d GetTargetPos()
    {
        if (!IsFarmer()) return null;

        // Anchor the "any crops to tend?" check on the WORKSTATION so the farmer
        // doesn't think she's done just because she wandered far from her fields.
        BlockPos wsPos = entity.GetBehavior<EntityBehaviorVillager>()?.Workstation;
        Vec3d searchAnchor = wsPos != null
            ? wsPos.ToVec3d().Add(0.5, 0.0, 0.5)
            : entity.Pos.XYZ;

        // If any farmland near the workstation has a live crop or dead crop, farming tasks should fire instead.
        bool hasFarmingWork = false;
        entity.Api.ModLoader.GetModSystem<POIRegistry>().GetNearestPoi(searchAnchor, maxDistance, poi =>
        {
            if (hasFarmingWork || !(poi is BlockEntityFarmland bef)) return false;
            if (bef.GetCrop() != null) { hasFarmingWork = true; return false; }
            Block above = entity.World.BlockAccessor.GetBlock(bef.Pos.UpCopy());
            if (above?.Code?.Path?.StartsWith("deadcrop") == true) hasFarmingWork = true;
            return false;
        });

        if (hasFarmingWork) return null;

        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        if (village == null) return null;

        // Pick a random waypoint; fall back to a random spot within village radius.
        if (village.Waypoints.Count > 0)
        {
            var waypoints = new List<BlockPos>(village.Waypoints);
            BlockPos wp = waypoints[entity.World.Rand.Next(waypoints.Count)];
            return wp.ToVec3d().Add(0.5, 0, 0.5);
        }

        // No waypoints - wander within village radius.
        double angle = entity.World.Rand.NextDouble() * Math.PI * 2;
        double dist  = entity.World.Rand.NextDouble() * village.Radius * 0.8;
        return village.EffectiveCenter().ToVec3d().Add(Math.Cos(angle) * dist, 0, Math.Sin(angle) * dist);
    }

    private bool IsFarmer() => entity?.Code?.Path?.EndsWith("-farmer") == true;
}
