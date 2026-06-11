using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VsVillage;

public class TravellingTraderManager : ModSystem
{
    // Persisted via SaveGame.StoreData. Explicit ProtoMember numbers - never
    // reorder or renumber without a save migration. Public for protobuf-net.
    [ProtoContract(ImplicitFields = ImplicitFields.None)]
    public sealed class TraderEntry
    {
        [ProtoMember(1)]
        public long TraderId;

        [ProtoMember(2)]
        public long GuardId;

        [ProtoMember(3)]
        public string VillageId;

        // Stale-entry timeout for entries whose entity can't be queried (chunk unloaded, admin purge, etc.).
        [ProtoMember(4)]
        public double SpawnedTotalHours;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.None)]
    public sealed class PersistedState
    {
        [ProtoMember(1)]
        public Dictionary<string, TraderEntry> Active;
    }

    private const string SaveDataKey = "vsvillage_travellingtraders";

    // Max in-game hours an active entry survives without a live trader entity. Visit caps at 18h; 30h covers detection slack.
    private const double StaleEntryTimeoutHours = 30.0;

    private static readonly string[] Specialties = new string[9] { "agriculture", "artisan", "buildmaterials", "clothing", "commodities", "furniture", "luxuries", "survivalgoods", "treasurehunter" };

    private static readonly string[] Sexes = new string[2] { "male", "female" };

    private const float SpawnChancePerTick = 0.2f;

    private const int TickIntervalMs = 180000;

    private const float SpawnHourMin = 5f;

    private const float SpawnHourMax = 10f;

    private const float DepartureHour = 20f;

    // GetActiveStallPos reads from the AI thread while OnTick/TrySpawn/etc write on main thread, so use ConcurrentDictionary.
    private readonly ConcurrentDictionary<string, TraderEntry> _active = new ConcurrentDictionary<string, TraderEntry>();

    private ICoreServerAPI _sapi;

    private const int MarketStallScanRadius = 75;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Server;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        api.Event.RegisterGameTickListener(OnTick, 180000);

        // Persist _active so OnTick after restart doesn't dup-spawn before the trader's Initialize re-registers itself.
        api.Event.SaveGameLoaded += OnSaveGameLoaded;
        api.Event.GameWorldSave  += OnGameWorldSave;

        api.Logger.Debug("[TravellingTraderManager] Started.");
    }

    private void OnSaveGameLoaded()
    {
        try
        {
            byte[] data = _sapi.WorldManager.SaveGame.GetData(SaveDataKey);
            if (data == null || data.Length < 2) return;
            PersistedState state = SerializerUtil.Deserialize<PersistedState>(data);
            if (state?.Active == null) return;
            int restored = 0;
            double nowHours = _sapi.World.Calendar.TotalHours;
            foreach (var kvp in state.Active)
            {
                if (kvp.Value == null || string.IsNullOrEmpty(kvp.Key)) continue;
                // Pre-persistence entries deserialize with SpawnedTotalHours=0 which would
                // disable the stale-entry timeout entirely. Stamp at load so the 30h window
                // starts now; if the trader entity is still alive it'll re-register with its
                // real spawn time, which is bounded by the same 30h cap anyway.
                if (kvp.Value.SpawnedTotalHours <= 0.0) kvp.Value.SpawnedTotalHours = nowHours;
                _active[kvp.Key] = kvp.Value;
                restored++;
            }
            if (restored > 0)
                _sapi.Logger.Notification($"[TravellingTraderManager] SaveGameLoaded: restored {restored} active trader entry/entries.");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[TravellingTraderManager] SaveGameLoaded: failed to deserialize active state: {ex.Message}");
            _active.Clear();
        }
    }

    private void OnGameWorldSave()
    {
        try
        {
            // Snapshot the dict so concurrent OnTick mutations don't corrupt the serialized bytes.
            PersistedState state = new PersistedState
            {
                Active = new Dictionary<string, TraderEntry>(_active)
            };
            _sapi.WorldManager.SaveGame.StoreData(SaveDataKey, SerializerUtil.Serialize(state));
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[TravellingTraderManager] GameWorldSave: failed to serialize active state: {ex.Message}");
        }
    }

    public void OnTraderDespawned(long traderId, string villageId)
    {
        if (villageId == null) return;

        // Only drop the entry if it matches THIS trader's id. If a duplicate
        // trader is somehow tracked (race during reload re-registration) and
        // the OLD one despawns, we don't want to clear the entry for the new
        // one.
        if (_active.TryGetValue(villageId, out var entry) && entry.TraderId == traderId)
        {
            _active.TryRemove(villageId, out _);
            _sapi.Logger.Debug($"[TravellingTraderManager] Trader {traderId} removed from village {villageId}.");
        }
        else
        {
            _sapi.Logger.Debug($"[TravellingTraderManager] Stale despawn for trader {traderId} in village {villageId}; active entry holds different trader (ignoring).");
        }
    }

    // Re-register a trader after chunk reload. On TraderId mismatch the in-memory entry wins (OnTick uses it for dup-spawn suppression).
    public void RegisterExistingTrader(long traderId, long guardId, string villageId, double spawnedTotalHours = 0.0)
    {
        if (string.IsNullOrEmpty(villageId)) return;
        if (_active.TryGetValue(villageId, out var existing))
        {
            if (existing.TraderId == traderId)
            {
                if (existing.SpawnedTotalHours <= 0.0 && spawnedTotalHours > 0.0)
                    existing.SpawnedTotalHours = spawnedTotalHours;
                return;
            }
            _sapi?.Logger.Warning(
                $"[TravellingTraderManager] Village {villageId} already has tracked trader {existing.TraderId}; " +
                $"loading trader {traderId} is likely a leftover duplicate. Keeping the tracked entry.");
            return;
        }
        _active[villageId] = new TraderEntry
        {
            TraderId = traderId,
            GuardId = guardId,
            VillageId = villageId,
            SpawnedTotalHours = spawnedTotalHours
        };
        _sapi?.Logger.Debug($"[TravellingTraderManager] Re-registered existing trader {traderId} for village {villageId} (post-reload).");
    }

    public BlockPos GetActiveStallPos(string villageId)
    {
        if (string.IsNullOrEmpty(villageId))
        {
            return null;
        }
        if (!_active.TryGetValue(villageId, out var entry))
        {
            return null;
        }
        EntityBehaviorTravellingTrader beh = _sapi.World.GetEntityById(entry.TraderId)?.GetBehavior<EntityBehaviorTravellingTrader>();
        if (beh == null || beh.IsLeaving)
        {
            return null;
        }
        return beh.MarketStallPos;
    }

    private void OnTick(float dt)
    {
        VillageManager vm = _sapi.ModLoader.GetModSystem<VillageManager>();
        if (vm == null)
        {
            return;
        }
        List<string> dead = new List<string>();
        double nowHours = _sapi.World.Calendar.TotalHours;
        foreach (KeyValuePair<string, TraderEntry> kvp in _active)
        {
            Entity e = _sapi.World.GetEntityById(kvp.Value.TraderId);
            if (e != null && !e.Alive)
            {
                // Entity loaded and confirmed dead, prune immediately.
                dead.Add(kvp.Key);
            }
            else if (e == null)
            {
                // Entity not currently loaded. Pre-persistence we treated
                // this as "dead and prune" which corrupted state on restart
                // (chunks not loaded yet at first OnTick = false positive
                // dead-prune = double-spawn). Now we keep the entry and only
                // prune via the stale timeout - 30 in-game hours past spawn
                // is well beyond the 18-hour visit cap, so any entry that
                // hasn't been confirmed by a live entity by then is genuinely
                // orphaned (entity purged out-of-band).
                if (kvp.Value.SpawnedTotalHours > 0
                    && nowHours - kvp.Value.SpawnedTotalHours > StaleEntryTimeoutHours)
                {
                    dead.Add(kvp.Key);
                }
            }
        }
        foreach (string k in dead)
        {
            _sapi.Logger.Debug("[TravellingTraderManager] Pruning dead/stale entry for village " + k + ".");
            _active.TryRemove(k, out _);
        }
        float hour = _sapi.World.Calendar.HourOfDay;
        bool isMorning = hour >= 5f && hour <= 10f;
        foreach (Village village in vm.Villages.Values)
        {
            if (!_active.ContainsKey(village.Id) && village.Beds.Count != 0 && village.Workstations.Count != 0 && isMorning && _sapi.World.Rand.NextDouble() <= 0.20)
            {
                TrySpawn(village);
            }
        }
    }

    // Admin-command entry point - bypasses the RNG roll and morning-time check,
    // but still requires a market stall and valid spawn position.
    public void TryForceSpawn(Village village) => TrySpawn(village);

    private void TrySpawn(Village village)
    {
        _sapi.Logger.Debug("[TravellingTraderManager] Attempting spawn for village " + village.Id + ".");
        BlockPos stallPos = FindMarketStallPos(village);
        if (stallPos == null)
        {
            _sapi.Logger.Debug("[TravellingTraderManager] No outdoor stall position for " + village.Id + " - skipping.");
            return;
        }
        Vec3d spawnPos = FindSpawnPos(village);
        if (spawnPos == null)
        {
            _sapi.Logger.Debug("[TravellingTraderManager] No valid spawn pos for " + village.Id + " - skipping.");
            return;
        }
        string sex = Sexes[_sapi.World.Rand.Next(Sexes.Length)];
        string specialty = Specialties[_sapi.World.Rand.Next(Specialties.Length)];
        string traderCode = "vsvillage:travelling-trader-" + sex + "-" + specialty;
        string guardCode = "vsvillage:travelling-guard-" + sex;
        EntityProperties traderType = _sapi.World.GetEntityType(new AssetLocation(traderCode));
        EntityProperties guardType = _sapi.World.GetEntityType(new AssetLocation(guardCode));
        if (traderType == null)
        {
            _sapi.Logger.Warning("[TravellingTraderManager] Entity type not found: " + traderCode);
            return;
        }
        if (guardType == null)
        {
            _sapi.Logger.Warning("[TravellingTraderManager] Entity type not found: " + guardCode);
            return;
        }

        // Calculate visit window
        float hoursPerDay = _sapi.World.Calendar.HoursPerDay;
        float currentHour = _sapi.World.Calendar.HourOfDay;
        double hoursUntilDepart = (20f - currentHour + hoursPerDay) % hoursPerDay;
        if (hoursUntilDepart < 1.0)
        {
            hoursUntilDepart += hoursPerDay;
        }
        double visitEnd = _sapi.World.Calendar.TotalHours + hoursUntilDepart;

        Entity traderEntity = _sapi.ClassRegistry.CreateEntity(traderType);
        traderEntity.Pos.SetPos(spawnPos);
        _sapi.World.SpawnEntity(traderEntity);

        Vec3d guardPos = spawnPos.Clone().Add(3.0, 0.0, 0.0);
        Entity guardEntity = _sapi.ClassRegistry.CreateEntity(guardType);
        guardEntity.Pos.SetPos(guardPos);
        _sapi.World.SpawnEntity(guardEntity);

        // Set behavior properties after SpawnEntity - behaviors are live now
        EntityBehaviorTravellingTrader traderBeh = traderEntity.GetBehavior<EntityBehaviorTravellingTrader>();
        if (traderBeh != null)
        {
            traderBeh.VillageId = village.Id;
            traderBeh.MarketStallPos = stallPos;
            traderBeh.GuardEntityId = guardEntity.EntityId;
            traderBeh.VisitEndTotalHours = visitEnd;
            traderBeh.SpawnedTotalHours = _sapi.World.Calendar.TotalHours;
        }
        else
        {
            _sapi.Logger.Warning($"[TravellingTraderManager] Trader {traderEntity.EntityId} missing EntityBehaviorTravellingTrader.");
        }

        EntityBehaviorTravellingGuard guardBeh = guardEntity.GetBehavior<EntityBehaviorTravellingGuard>();
        if (guardBeh != null)
        {
            guardBeh.TraderEntityId = traderEntity.EntityId;
            guardBeh.MarketStallPos = stallPos;
        }
        else
        {
            _sapi.Logger.Warning($"[TravellingTraderManager] Guard {guardEntity.EntityId} missing EntityBehaviorTravellingGuard.");
        }

        _active[village.Id] = new TraderEntry
        {
            TraderId = traderEntity.EntityId,
            GuardId = guardEntity.EntityId,
            VillageId = village.Id,
            SpawnedTotalHours = _sapi.World.Calendar.TotalHours
        };

        _sapi.Logger.Notification($"[TravellingTraderManager] Spawned {traderCode} (id {traderEntity.EntityId}) + guard (id {guardEntity.EntityId}) for village {village.Id}. Stall: {stallPos}. Visit ends at {visitEnd:F1} h.");
        string traderName = traderEntity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName ?? "a travelling trader";
        string villageName = ((!string.IsNullOrWhiteSpace(village.Name)) ? village.Name : Lang.Get("vsvillage:trader-village-unknown"));
        _sapi.BroadcastMessageToAllGroups(Lang.Get("vsvillage:trader-arriving", traderName, villageName), EnumChatType.Notification);
    }

    private BlockPos FindMarketStallPos(Village village)
    {
        BlockPos stall = ScanForMarketStallBlock(village, _sapi.World.BlockAccessor);
        if (stall != null)
            _sapi.Logger.Debug($"[TravellingTraderManager] Found marketstall block at {stall} for village {village.Id}.");
        return stall;
    }

    private BlockPos ScanForMarketStallBlock(Village village, IBlockAccessor ba)
    {
        int r = Math.Min(village.Radius, MarketStallScanRadius);
        int dyMax = Math.Min(village.Radius, MarketStallScanRadius);
        // Worldgen villages store Pos.Y ~20 (see Village.EffectiveCenterY).
        int cy = village.EffectiveCenterY();
        BlockPos tmp = new BlockPos(0);
        for (int dx = -r; dx <= r; dx++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                int wx = village.Pos.X + dx;
                int wz = village.Pos.Z + dz;
                for (int dy = dyMax; dy >= -dyMax; dy--)
                {
                    tmp.Set(wx, cy + dy, wz);
                    // Skip unloaded chunks rather than reading bogus air from them.
                    if (!ba.IsValidPos(tmp) || ba.GetChunkAtBlockPos(tmp) == null) continue;
                    Block b = ba.GetBlock(tmp);
                    if (b != null && b.Code?.Domain == "vsvillage" && b.Code?.Path?.StartsWith("marketstall") == true)
                    {
                        return tmp.Copy();
                    }
                }
            }
        }
        return null;
    }

    private Vec3d FindSpawnPos(Village village)
    {
        IBlockAccessor ba = _sapi.World.BlockAccessor;
        const double baseRadius = 55.0;
        int centerY = village.EffectiveCenterY();
        for (int attempt = 0; attempt < 20; attempt++)
        {
            double angle = _sapi.World.Rand.NextDouble() * Math.PI * 2.0;
            double dist = baseRadius + (_sapi.World.Rand.NextDouble() * 4.0 - 2.0);
            int x = village.Pos.X + (int)(Math.Cos(angle) * dist);
            int z = village.Pos.Z + (int)(Math.Sin(angle) * dist);
            // FindSurface only probes +/-5 blocks, so it must start near the actual
            // surface - village.Pos.Y is unreliable (worldgen stores ~20).
            BlockPos candidate = new BlockPos(x, centerY, z, 0);
            candidate = FindSurface(ba, candidate);
            if (candidate != null)
            {
                return candidate.ToVec3d().Add(0.5, 1.0, 0.5);
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
            // Visually-solid blocks with no collision (leaves, vines, cobwebs) would let
            // the spawn pass the original CollisionBoxes test and the trader would appear
            // with their head poking through tree foliage. Reject those as well. Tall grass
            // and snow layers are intentionally still allowed.
            bool spaceClear = (space.CollisionBoxes == null || space.CollisionBoxes.Length == 0) && !IsHeadObstruction(space);
            bool headClear  = (head.CollisionBoxes  == null || head.CollisionBoxes.Length  == 0) && !IsHeadObstruction(head);
            if (hasFloor && spaceClear && headClear)
            {
                return check.UpCopy();
            }
        }
        return null;
    }

    private static bool IsHeadObstruction(Block block)
    {
        string p = block?.Code?.Path;
        if (string.IsNullOrEmpty(p)) return false;
        return p.Contains("leaves") || p.Contains("vine") || p.Contains("cobweb");
    }

    private static bool IsOutdoors(IBlockAccessor ba, BlockPos pos)
    {
        BlockPos check = pos.UpCopy();
        for (int i = 0; i < 30; i++)
        {
            Block b = ba.GetBlock(check);
            if (b?.CollisionBoxes != null && b.CollisionBoxes.Length != 0)
            {
                return false;
            }
            check = check.UpCopy();
        }
        return true;
    }
}