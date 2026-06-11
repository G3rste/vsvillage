using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage;

public class EntityBehaviorTravellingTrader : EntityBehavior
{
    private const long LeavingTimeoutMs = 5 * 60 * 1000; // 5 minutes real time

    // Consistent with AiTaskTravellingTraderLeave.ExitDist, trader is considered
    // outside the village once beyond this distance from the village centre.
    private const float DespawnDist = 55f;

    private long _tickListenerId;

    // WatchedAttributes-backed so the client debug overlay sees real values and the timeout persists across chunk reload.
    public bool IsAtStall
    {
        get => entity.WatchedAttributes.GetBool("ttIsAtStall", false);
        set
        {
            entity.WatchedAttributes.SetBool("ttIsAtStall", value);
            entity.WatchedAttributes.MarkPathDirty("ttIsAtStall");
        }
    }

    // Set when IsLeaving becomes true. Fallback despawn timeout.
    public long LeavingStartedMs
    {
        get => entity.WatchedAttributes.GetLong("ttLeavingStartedMs", 0L);
        set
        {
            entity.WatchedAttributes.SetLong("ttLeavingStartedMs", value);
            entity.WatchedAttributes.MarkPathDirty("ttLeavingStartedMs");
        }
    }

    public string VillageId
    {
        get
        {
            return entity.WatchedAttributes.GetString("ttVillageId");
        }
        set
        {
            entity.WatchedAttributes.SetString("ttVillageId", value);
            entity.WatchedAttributes.MarkPathDirty("ttVillageId");
        }
    }

    public BlockPos MarketStallPos
    {
        get
        {
            return entity.WatchedAttributes.GetBlockPos("ttMarketStallPos");
        }
        set
        {
            if (value != null)
            {
                entity.WatchedAttributes.SetBlockPos("ttMarketStallPos", value);
            }
            entity.WatchedAttributes.MarkPathDirty("ttMarketStallPos");
        }
    }

    public long GuardEntityId
    {
        get
        {
            return entity.WatchedAttributes.GetLong("ttGuardEntityId", 0L);
        }
        set
        {
            entity.WatchedAttributes.SetLong("ttGuardEntityId", value);
            entity.WatchedAttributes.MarkPathDirty("ttGuardEntityId");
        }
    }

    public double VisitEndTotalHours
    {
        get
        {
            return entity.WatchedAttributes.GetDouble("ttVisitEndHours");
        }
        set
        {
            entity.WatchedAttributes.SetDouble("ttVisitEndHours", value);
            entity.WatchedAttributes.MarkPathDirty("ttVisitEndHours");
        }
    }

    public bool IsLeaving
    {
        get
        {
            return entity.WatchedAttributes.GetBool("ttIsLeaving");
        }
        set
        {
            entity.WatchedAttributes.SetBool("ttIsLeaving", value);
            entity.WatchedAttributes.MarkPathDirty("ttIsLeaving");
        }
    }

    public double SpawnedTotalHours
    {
        get
        {
            return entity.WatchedAttributes.GetDouble("ttSpawnedTotalHours");
        }
        set
        {
            entity.WatchedAttributes.SetDouble("ttSpawnedTotalHours", value);
            entity.WatchedAttributes.MarkPathDirty("ttSpawnedTotalHours");
        }
    }

    public EntityBehaviorTravellingTrader(Entity entity)
        : base(entity)
    {
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        if (entity.Api.Side == EnumAppSide.Server)
        {
            _tickListenerId = entity.World.RegisterGameTickListener(CheckDespawn, 10000);

            // Idempotent re-register with the manager (SaveGameLoaded already populated _active in the normal case).
            string vid = VillageId;
            if (!string.IsNullOrEmpty(vid))
            {
                entity.Api.ModLoader.GetModSystem<TravellingTraderManager>()
                    ?.RegisterExistingTrader(entity.EntityId, GuardEntityId, vid, SpawnedTotalHours);
            }

            Log("Initialized (server).");
        }
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        if (_tickListenerId != 0)
        {
            entity.World.UnregisterGameTickListener(_tickListenerId);
            _tickListenerId = 0L;
        }

        // Only run cleanup on genuine removals. OutOfRange/Unload/Disconnect respawn intact, cleanup would orphan the paired guard.
        if (despawn != null
            && despawn.Reason != EnumDespawnReason.Death
            && despawn.Reason != EnumDespawnReason.Removed
            && despawn.Reason != EnumDespawnReason.Combusted
            && despawn.Reason != EnumDespawnReason.Expire
            && despawn.Reason != EnumDespawnReason.PickedUp)
        {
            return;
        }

        entity.Api.ModLoader.GetModSystem<TravellingTraderManager>()?.OnTraderDespawned(entity.EntityId, VillageId);
        long guardId = GuardEntityId;
        if (guardId != 0)
        {
            Entity guard = entity.World.GetEntityById(guardId);
            if (guard != null && guard.Alive)
            {
                Log($"Despawning paired guard {guardId}.");
                (entity.Api as ICoreServerAPI)?.World.DespawnEntity(guard, new EntityDespawnData
                {
                    Reason = EnumDespawnReason.Removed
                });
            }
        }
    }

    public override string PropertyName()
    {
        return "TravellingTrader";
    }

    // Called by AiTaskTravellingTraderLeave when the trader has completed its
    // exit path. Despawns immediately if already outside DespawnDist from the
    // village centre, otherwise lets CheckDespawn handle it on the next poll.
    public void NotifyPathComplete()
    {
        if (entity.Api.Side != EnumAppSide.Server) return;

        string villageId = VillageId;
        if (string.IsNullOrEmpty(villageId)) { DespawnSelf(); return; }

        Village village = entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(villageId);
        if (village == null) { DespawnSelf(); return; }

        double dist = entity.Pos.XYZ.DistanceTo(village.EffectiveCenter().ToVec3d());
        if (dist > DespawnDist)
        {
            Log($"Path complete and outside village ({dist:F0} > {DespawnDist}) - despawning.");
            DespawnSelf();
        }
        // else: still inside, CheckDespawn will catch it within 10 seconds
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        base.GetInfoText(infotext);
        if (entity.Api is ICoreClientAPI capi && capi.Settings.Bool["showEntityDebugInfo"])
        {
            double hoursLeft = VisitEndTotalHours - entity.World.Calendar.TotalHours;
            StringBuilder stringBuilder = infotext;
            StringBuilder stringBuilder2 = stringBuilder;
            StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder);
            handler.AppendLiteral("[TT] Village : ");
            handler.AppendFormatted(VillageId ?? "unset");
            stringBuilder2.AppendLine(ref handler);
            stringBuilder = infotext;
            StringBuilder stringBuilder3 = stringBuilder;
            handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder);
            handler.AppendLiteral("[TT] Stall   : ");
            handler.AppendFormatted(MarketStallPos?.ToString() ?? "unset");
            stringBuilder3.AppendLine(ref handler);
            stringBuilder = infotext;
            StringBuilder stringBuilder4 = stringBuilder;
            handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder);
            handler.AppendLiteral("[TT] AtStall : ");
            handler.AppendFormatted(IsAtStall);
            stringBuilder4.AppendLine(ref handler);
            stringBuilder = infotext;
            StringBuilder stringBuilder5 = stringBuilder;
            handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder);
            handler.AppendLiteral("[TT] Leaving : ");
            handler.AppendFormatted(IsLeaving);
            stringBuilder5.AppendLine(ref handler);
            stringBuilder = infotext;
            StringBuilder stringBuilder6 = stringBuilder;
            handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder);
            handler.AppendLiteral("[TT] Guard   : ");
            handler.AppendFormatted(GuardEntityId);
            stringBuilder6.AppendLine(ref handler);
            stringBuilder = infotext;
            StringBuilder stringBuilder7 = stringBuilder;
            handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder);
            handler.AppendLiteral("[TT] HrsLeft : ");
            handler.AppendFormatted(hoursLeft, "F1");
            stringBuilder7.AppendLine(ref handler);
        }
    }

    private void CheckDespawn(float dt)
    {
        if (!entity.Alive || entity.Api.Side != EnumAppSide.Server)
            return;

        if (string.IsNullOrEmpty(VillageId) || VisitEndTotalHours <= 0)
            return;

        string villageId = VillageId;
        Village village = entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(villageId);
        if (village != null)
        {
            double distToVillage = entity.Pos.XYZ.DistanceTo(village.EffectiveCenter().ToVec3d());
            if (distToVillage > DespawnDist)
            {
                Log($"Left village area ({distToVillage:F0} > {DespawnDist}) - despawning.");
                DespawnSelf();
                return;
            }
        }

        if (SpawnedTotalHours > 0 && entity.World.Calendar.TotalHours - SpawnedTotalHours >= 18.0)
        {
            Log("18-hour timeout reached - despawning.");
            DespawnSelf();
            return;
        }

        // Fallback: if IsLeaving has been set for more than 5 real minutes and the
        // trader is still alive, something went wrong with the leave task - force despawn.
        if (IsLeaving && LeavingStartedMs > 0 &&
            entity.World.ElapsedMilliseconds - LeavingStartedMs > LeavingTimeoutMs)
        {
            Log("Departure timeout (5 min) reached - force despawning.");
            DespawnSelf();
            return;
        }

        if (!IsLeaving && entity.World.Calendar.TotalHours >= VisitEndTotalHours)
        {
            IsLeaving = true;
            IsAtStall = false;
            LeavingStartedMs = entity.World.ElapsedMilliseconds;
            Log("Visit timer expired - beginning departure walk.");
            if (!string.IsNullOrEmpty(villageId))
            {
                string traderName = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName ?? "the travelling trader";
                string villageName = !string.IsNullOrWhiteSpace(village?.Name) ? village.Name : Lang.Get("vsvillage:trader-village-unknown");
                (entity.Api as ICoreServerAPI)?.BroadcastMessageToAllGroups(Lang.Get("vsvillage:trader-departing", traderName, villageName), EnumChatType.Notification);
            }
        }
    }

    public void DespawnSelf()
    {
        (entity.Api as ICoreServerAPI)?.World.DespawnEntity(entity, new EntityDespawnData
        {
            Reason = EnumDespawnReason.Removed
        });
    }

    private void Log(string msg)
    {
        entity.World.Logger.Debug($"[TravellingTrader:{entity.EntityId}] {msg}");
    }
}