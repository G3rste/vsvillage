using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage;

// Soldier/guard chase task on AiTaskGotoAndInteract (fences, gates, stuck recovery). Yields to the melee/ranged attack task at HandoffDistSq.
public class AiTaskVillagerChaseEntity : AiTaskGotoAndInteract
{
    // The entity we are currently chasing.
    public Entity targetEntity;

    // Entity code patterns read from JSON "entityCodes".
    private readonly List<string> entityCodes = new List<string>();

    // Optional entity-suffix allow-list (e.g. ["-soldier", "-archer"]).
    private readonly List<string> requiredSuffixes = new List<string>();

    // Chase configuration.
    private float seekingRange;
    private float maxFollowTimeSec;
    private long chaseStartedAtMs;

    // Repath as target moves.
    private long lastTargetUpdateMs;
    private const long TargetUpdateIntervalMs = 800;

    // Vertical detection cap. Ignores threats beyond this Y delta so guards don't chase cave drifters with no path.
    private const float MaxVertDetection = 5f;

    // Call-for-help throttle: 5s gate, OnEntityHurt.
    private long lastCallForHelpMs;

    // Alarm-rally state: when true, this task is pathing to an allied soldier's position rather than a hostile.
    private bool _followingAlarm;

    public AiTaskVillagerChaseEntity(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
        : base(entity, taskConfig, aiConfig)
    {
        seekingRange = taskConfig["seekingRange"].AsFloat(20f);
        maxFollowTimeSec = taskConfig["maxFollowTime"].AsFloat(120f);

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
        // Honour entity-suffix allow-list (e.g. soldier-only tasks).
        if (!IsAllowedEntityType()) return null;

        // Keep existing live target. Yield to attack task if it's already within HandoffDistSq (otherwise we preempt mid-strike).
        if (targetEntity != null && targetEntity.Alive && InRange(targetEntity))
        {
            // Drop chase if target crossed outside village radius. Soldiers defend home,
            // they don't sprint into the wilderness after a fleeing drifter.
            if (!IsTargetInsideVillage(targetEntity)) { targetEntity = null; return null; }
            if (entity.Pos.SquareDistanceTo(targetEntity.Pos) <= HandoffDistSq)
                return null;
            return targetEntity.Pos.XYZ;
        }

        targetEntity = null;

        // Scan for the nearest matching hostile.
        targetEntity = entity.World.GetNearestEntity(
            entity.Pos.XYZ, seekingRange, MaxVertDetection,
            e => e != entity && e.Alive && e.IsInteractable && MatchesCode(e) && IsTargetInsideVillage(e));

        if (targetEntity != null)
        {
            // Hostile already in attack range, yield to attack task.
            // BroadcastContactReport(targetEntity); // TODO: re-implement with village-radius scoped broadcast
            if (entity.Pos.SquareDistanceTo(targetEntity.Pos) <= HandoffDistSq)
            {
                targetEntity = null;
                return null;
            }
            chaseStartedAtMs = entity.World.ElapsedMilliseconds;
            // BroadcastContactReport(targetEntity); // TODO: re-implement with village-radius scoped broadcast
            _followingAlarm = false;
            return targetEntity.Pos.XYZ;
        }

        // No hostile within seekingRange. If the village raised an alarm, path toward the engaging ally
        // so we can join the fight even if the hostile is well beyond our regular scan radius.
        Vec3d rally = TryAlarmRally();
        if (rally != null)
        {
            _followingAlarm = true;
            chaseStartedAtMs = entity.World.ElapsedMilliseconds;
            return rally;
        }

        _followingAlarm = false;
        return null;
    }

    // Threats outside the village radius aren't worth chasing - return to defend
    // instead. Villages without a Pos set (shouldn't happen) default to "allow".
    private bool IsTargetInsideVillage(Entity target)
    {
        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        if (village?.Pos == null) return true;
        double radiusSq = village.Radius * (double)village.Radius;
        return village.EffectiveCenter().DistanceSqTo(target.Pos.X, target.Pos.Y, target.Pos.Z) <= radiusSq;
    }

    private Vec3d TryAlarmRally()
    {
        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        if (village == null) return null;
        if (!AiTaskVillagerMeleeAttack.VillageAlarms.TryGetValue(village.Id, out long atMs)) return null;
        long elapsed = entity.World.ElapsedMilliseconds - atMs;
        if (elapsed < 0 || elapsed >= AiTaskVillagerMeleeAttack.AlarmDurationMs) return null;
        if (!AiTaskVillagerMeleeAttack.VillageAlarmEngagers.TryGetValue(village.Id, out long engagerId)) return null;
        if (engagerId == entity.EntityId) return null;
        Entity engager = entity.World.GetEntityById(engagerId);
        if (engager == null || !engager.Alive) return null;
        if (entity.Pos.SquareDistanceTo(engager.Pos) <= HandoffDistSq) return null;
        // Only rally while a live hostile is still near the engager. Without this gate the
        // alarm window keeps pulling allies in long after combat ended (sticky tail).
        if (!HostilePresentNear(engager)) return null;
        return engager.Pos.XYZ;
    }

    private bool HostilePresentNear(Entity engager)
    {
        return entity.World.GetNearestEntity(
            engager.Pos.XYZ, seekingRange, MaxVertDetection,
            e => e != engager && e.Alive && e.IsInteractable && MatchesCode(e)) != null;
    }

    // Execution

    public override void StartExecute()
    {
        chaseStartedAtMs = entity.World.ElapsedMilliseconds;
        lastTargetUpdateMs = entity.World.ElapsedMilliseconds;
        base.StartExecute();
    }

    // Handoff distance squared, per profession. Soldier 2 blocks (matches vanilla minDist=2), archer 8 blocks (comfortable bow stand-off vs maxDist 15-16).
    private const double MeleeHandoffDistSq = 4.0;
    private const double RangedHandoffDistSq = 64.0;

    // Per-entity handoff distance² - archers stop sooner so they can shoot from safety.
    private double HandoffDistSq =>
        entity.Code?.Path?.EndsWith("-archer", StringComparison.OrdinalIgnoreCase) == true
            ? RangedHandoffDistSq
            : MeleeHandoffDistSq;

    public override bool ContinueExecute(float dt)
    {
        if (_followingAlarm)
            return ContinueAlarmRally(dt);

        // Drop target if dead or fled out of range.
        if (targetEntity == null || !targetEntity.Alive || !InRange(targetEntity))
            return false;

        // Enforce max follow time.
        if (entity.World.ElapsedMilliseconds - chaseStartedAtMs > maxFollowTimeSec * 1000f)
            return false;

        // Within attack range (smaller for soldiers, larger for archers), stop chasing
        // and let the attack task take over.
        if (entity.Pos.SquareDistanceTo(targetEntity.Pos) <= HandoffDistSq)
            return false;

        // Periodically repath toward the moving target.
        long now = entity.World.ElapsedMilliseconds;
        if (now - lastTargetUpdateMs > TargetUpdateIntervalMs)
        {
            lastTargetUpdateMs = now;
            // Path to 2 blocks short of the target to avoid pathing into an occupied or wall-adjacent tile.
            Vec3d approachPos = GetApproachPos(targetEntity.Pos.XYZ, 2.0);
            if (targetPos == null || approachPos.SquareDistanceTo(targetPos) > 4.0)
            {
                targetPos = approachPos;
                AttemptRepath();
            }
        }

        return base.ContinueExecute(dt);
    }

    private bool ContinueAlarmRally(float dt)
    {
        // Bail if the alarm cleared, the engager died, or we cleared rally mode externally.
        Village village = entity.GetBehavior<EntityBehaviorVillager>()?.Village;
        if (village == null) { _followingAlarm = false; return false; }
        if (!AiTaskVillagerMeleeAttack.VillageAlarms.TryGetValue(village.Id, out long atMs)) { _followingAlarm = false; return false; }
        long elapsed = entity.World.ElapsedMilliseconds - atMs;
        if (elapsed < 0 || elapsed >= AiTaskVillagerMeleeAttack.AlarmDurationMs) { _followingAlarm = false; return false; }
        if (!AiTaskVillagerMeleeAttack.VillageAlarmEngagers.TryGetValue(village.Id, out long engagerId)) { _followingAlarm = false; return false; }
        Entity engager = entity.World.GetEntityById(engagerId);
        if (engager == null || !engager.Alive) { _followingAlarm = false; return false; }
        if (!HostilePresentNear(engager)) { _followingAlarm = false; return false; }

        if (entity.World.ElapsedMilliseconds - chaseStartedAtMs > maxFollowTimeSec * 1000f)
        {
            _followingAlarm = false;
            return false;
        }

        // Close enough to the fight: drop rally mode so the next GetTargetPos scan picks up the actual hostile.
        double handoffSq = HandoffDistSq * 2.0;
        if (entity.Pos.SquareDistanceTo(engager.Pos) <= handoffSq)
        {
            _followingAlarm = false;
            return false;
        }

        long now = entity.World.ElapsedMilliseconds;
        if (now - lastTargetUpdateMs > TargetUpdateIntervalMs)
        {
            lastTargetUpdateMs = now;
            Vec3d approachPos = GetApproachPos(engager.Pos.XYZ, 2.0);
            if (targetPos == null || approachPos.SquareDistanceTo(targetPos) > 4.0)
            {
                targetPos = approachPos;
                AttemptRepath();
            }
        }

        return base.ContinueExecute(dt);
    }

    // Point stopDist blocks short of rawTarget toward this entity, so the goal is never the target's tile.
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
        // Apply cooldown even when targetReached was never set (it never is).
        lastExecution = entity.World.ElapsedMilliseconds;
        targetEntity = null;
        _followingAlarm = false;
    }

    // Chase just navigates; melee-attack handles the strike. Skip InteractionPossible too (chase has no arrival "interaction").
    protected override bool InteractionPossible() => false;

    // Ally coordination (mirrors AiTaskVillagerSeekEntity)

    public override void OnEntityHurt(DamageSource source, float damage)
    {
        Entity cause = source.CauseEntity;
        if (cause != null && cause.HasBehavior<EntityBehaviorVillager>()) return;
        Entity src = source.SourceEntity;
        if (src != null && src.HasBehavior<EntityBehaviorVillager>()) return;

        if (source.Type == EnumDamageType.Heal) return;
        if (cause == null && src == null) return;
        if (entity.World.ElapsedMilliseconds - lastCallForHelpMs < 5000) return;

        lastCallForHelpMs = entity.World.ElapsedMilliseconds;
        Entity attacker = src ?? cause;

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

    // Returns true if this entity is in the requiredSuffixes allow-list (or the list is empty).
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
        return (dx * dx + dz * dz) <= seekingRange * seekingRange * 2f;
    }

    /*
    // TODO: Re-implement BroadcastContactReport with village-radius scoped player filtering.
    // Requires verifying Village.Pos and Village.Radius property names against the Village class.
    private void BroadcastContactReport(Entity target)
    {
        if (entity.World.ElapsedMilliseconds - lastContactReportMs < 10000) return;
        lastContactReportMs = entity.World.ElapsedMilliseconds;

        ICoreServerAPI sapi = entity.World.Api as ICoreServerAPI;
        if (sapi == null) return;

        EntityBehaviorVillager bv = entity.GetBehavior<EntityBehaviorVillager>();
        string villageName = bv?.Village?.Name ?? Lang.Get("vsvillage:trader-village-unknown");

        string langKey = (entity.Code?.Path?.EndsWith("-archer") == true)
            ? "vsvillage:archer-spotted-enemy"
            : "vsvillage:soldier-spotted-enemy";

        sapi.BroadcastMessageToAllGroups(Lang.Get(langKey, villageName, target.GetName()), EnumChatType.Notification);
    }
    */

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