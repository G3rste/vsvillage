using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage;

// Self-defence seek for all villagers on AiTaskGotoAndInteract (so it shares the VillagerAStarNew pathfinder with chase/goto-work).
public class AiTaskVillagerSeekEntity : AiTaskGotoAndInteract
{
    // The entity we are currently pursuing.
    public Entity targetEntity;

    // Entity code patterns read from JSON "entityCodes".
    private readonly List<string> entityCodes = new List<string>();

    // Optional entity-suffix allow-list (usually empty - applies to all villagers).
    private readonly List<string> requiredSuffixes = new List<string>();

    // Pursuit configuration.
    private float seekingRange;
    private float minRange;
    private float maxFollowTimeSec;
    private long  pursuitStartedAtMs;

    // Repath as target moves.
    private long lastTargetUpdateMs;
    private const long TargetUpdateIntervalMs = 800;

    // Vertical detection cap. Ignores threats beyond this Y delta so civilians don't pursue cave hostiles with no surface path.
    private const float MaxVertDetection = 5f;

    // Ally call-for-help throttle.
    private long lastCallForHelp;

    public AiTaskVillagerSeekEntity(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
        : base(entity, taskConfig, aiConfig)
    {
        seekingRange     = taskConfig["seekingRange"].AsFloat(20f);
        minRange         = taskConfig["minRange"].AsFloat(0f);
        maxFollowTimeSec = taskConfig["maxFollowTime"].AsFloat(60f);

        JsonObject[] codes = taskConfig["entityCodes"].AsArray();
        if (codes != null)
            foreach (JsonObject c in codes)
            {
                string s = c.AsString();
                if (!string.IsNullOrEmpty(s)) entityCodes.Add(s);
            }

        JsonObject[] suffixes = taskConfig["requiredEntitySuffixes"].AsArray();
        if (suffixes != null)
            foreach (JsonObject s in suffixes)
            {
                string sfx = s.AsString();
                if (!string.IsNullOrEmpty(sfx)) requiredSuffixes.Add(sfx);
            }
    }

    // Target acquisition

    protected override Vec3d GetTargetPos()
    {
        if (!IsAllowedEntityType()) return null;

        // Keep existing live target if still valid AND still inside village.
        if (targetEntity != null && targetEntity.Alive && InRange(targetEntity))
        {
            if (!IsTargetInsideVillage(targetEntity)) { targetEntity = null; return null; }
            return targetEntity.Pos.XYZ;
        }

        targetEntity = null;

        // Scan for the nearest matching hostile. Vertical detection capped so we don't
        // acquire hostiles deep underground or up in the canopy with no surface path.
        targetEntity = entity.World.GetNearestEntity(
            entity.Pos.XYZ, seekingRange, MaxVertDetection,
            e => e != entity && e.Alive && e.IsInteractable && MatchesCode(e) && IsBeyondMinRange(e) && IsTargetInsideVillage(e));

        if (targetEntity != null)
        {
            pursuitStartedAtMs = entity.World.ElapsedMilliseconds;
            return targetEntity.Pos.XYZ;
        }
        return null;
    }

    // Execution

    public override void StartExecute()
    {
        pursuitStartedAtMs = entity.World.ElapsedMilliseconds;
        lastTargetUpdateMs = entity.World.ElapsedMilliseconds;
        base.StartExecute();
    }

    // 2.0 blocks - align with vanilla AiTaskMeleeAttack minDist=2.
    private const double MeleeHandoffDistSq = 4.0; // 2.0 blocks

    public override bool ContinueExecute(float dt)
    {
        if (targetEntity == null || !targetEntity.Alive || !InRange(targetEntity))
            return false;

        if (entity.World.ElapsedMilliseconds - pursuitStartedAtMs > maxFollowTimeSec * 1000f)
            return false;

        // Within melee/ranged strike range - stop and let the attack task take over.
        if (entity.Pos.SquareDistanceTo(targetEntity.Pos) <= MeleeHandoffDistSq)
            return false;

        // Periodically repath toward the moving target.
        long now = entity.World.ElapsedMilliseconds;
        if (now - lastTargetUpdateMs > TargetUpdateIntervalMs)
        {
            lastTargetUpdateMs = now;
            Vec3d approachPos = GetApproachPos(targetEntity.Pos.XYZ, 2.0);
            if (targetPos == null || approachPos.SquareDistanceTo(targetPos) > 4.0)
            {
                targetPos = approachPos;
                AttemptRepath();
            }
        }

        return base.ContinueExecute(dt);
    }

    private Vec3d GetApproachPos(Vec3d rawTarget, double stopDist)
    {
        Vec3d myPos = entity.Pos.XYZ;
        double dx = myPos.X - rawTarget.X;
        double dz = myPos.Z - rawTarget.Z;
        double dist = Math.Sqrt(dx * dx + dz * dz);
        if (dist < stopDist + 0.5) return rawTarget.Clone();
        double scale = stopDist / dist;
        return new Vec3d(rawTarget.X + dx * scale, rawTarget.Y, rawTarget.Z + dz * scale);
    }

    public override void FinishExecute(bool cancelled)
    {
        base.FinishExecute(cancelled);
        // Always apply cooldown - targetReached is never flagged on pursuit tasks.
        lastExecution = entity.World.ElapsedMilliseconds;
        targetEntity  = null;
    }

    // Seek just navigates, melee/ranged attack handles the strike. No arrival interaction.
    protected override bool InteractionPossible() => false;

    // Ally coordination (preserved from previous implementation)

    public override void OnEntityHurt(DamageSource source, float damage)
    {
        Entity cause = source.CauseEntity;
        if (cause != null && cause.HasBehavior<EntityBehaviorVillager>()) return;
        Entity src = source.SourceEntity;
        if (src   != null && src.HasBehavior<EntityBehaviorVillager>())   return;

        base.OnEntityHurt(source, damage);

        if (source.Type == EnumDamageType.Heal) return;
        if (cause == null && src == null) return;

        Entity attacker = src ?? cause;

        // Retaliate: if we have no target or it's dead, lock onto our attacker.
        if (attacker != null && attacker.Alive && (targetEntity == null || !targetEntity.Alive))
            targetEntity = attacker;

        // Throttled call-for-help to nearby soldier allies.
        if (entity.World.ElapsedMilliseconds - lastCallForHelp < 5000) return;
        lastCallForHelp = entity.World.ElapsedMilliseconds;

        Entity[] allies = entity.World.GetEntitiesAround(
            entity.Pos.XYZ, 15f, 4f,
            e =>
            {
                EntityBehaviorVillager bv = e.GetBehavior<EntityBehaviorVillager>();
                return bv != null && bv.Profession == EnumVillagerProfession.soldier;
            });

        foreach (Entity ally in allies)
        {
            AiTaskManager tm = ally.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (tm == null) continue;
            tm.GetTask<AiTaskVillagerSeekEntity>()?.OnAllyAttacked(attacker);
            tm.GetTask<AiTaskVillagerChaseEntity>()?.OnAllyAttacked(attacker);
            tm.GetTask<AiTaskVillagerMeleeAttack>()?.OnAllyAttacked(attacker);
            tm.GetTask<AiTaskVillagerRangedAttack>()?.OnAllyAttacked(attacker);
        }
    }

    public void OnAllyAttacked(Entity byEntity)
    {
        if (byEntity != null && byEntity.Alive && (targetEntity == null || !targetEntity.Alive))
            targetEntity = byEntity;
    }

    // Helpers

    private bool IsAllowedEntityType()
    {
        if (requiredSuffixes.Count == 0) return true;
        string path = entity.Code?.Path ?? "";
        foreach (string sfx in requiredSuffixes)
            if (path.EndsWith(sfx, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private bool InRange(Entity e)
    {
        // Horizontal squared + vertical clamp split, prevents tracking hostiles 20+ blocks underground via diagonal 3D distance.
        double dx = entity.Pos.X - e.Pos.X;
        double dz = entity.Pos.Z - e.Pos.Z;
        if (Math.Abs(entity.Pos.Y - e.Pos.Y) > MaxVertDetection) return false;
        double distSq = dx * dx + dz * dz;
        return distSq <= seekingRange * seekingRange * 2f && distSq >= minRange * minRange;
    }

    private bool IsBeyondMinRange(Entity e)
    {
        if (minRange <= 0f) return true;
        return entity.Pos.SquareDistanceTo(e.Pos) >= minRange * minRange;
    }

    // Threats outside the village aren't worth pursuing - civilians defend home,
    // they don't chase fleeing hostiles into the wilderness.
    private bool IsTargetInsideVillage(Entity target)
    {
        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        if (village?.Pos == null) return true;
        double radiusSq = village.Radius * (double)village.Radius;
        return village.EffectiveCenter().DistanceSqTo(target.Pos.X, target.Pos.Y, target.Pos.Z) <= radiusSq;
    }

    private bool MatchesCode(Entity e)
    {
        string path = e.Code?.Path ?? "";
        foreach (string pattern in entityCodes)
        {
            if (pattern.EndsWith("*"))
            {
                string prefix = pattern.Substring(0, pattern.Length - 1);
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
