using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage;

[ProtoContract(ImplicitFields = ImplicitFields.None)]
public class Village
{
    [ProtoMember(1)]
    public BlockPos Pos;

    [ProtoMember(2)]
    public int Radius;

    [ProtoMember(3)]
    public string Name;

    [ProtoMember(4)]
    public Dictionary<BlockPos, VillagerBed> Beds = new Dictionary<BlockPos, VillagerBed>();

    [ProtoMember(5)]
    public Dictionary<BlockPos, VillagerWorkstation> Workstations = new Dictionary<BlockPos, VillagerWorkstation>();

    [ProtoMember(6)]
    public HashSet<BlockPos> Gatherplaces = new HashSet<BlockPos>();

    [ProtoMember(7)]
    public Dictionary<long, VillagerData> VillagerSaveData = new Dictionary<long, VillagerData>();

    [ProtoMember(8)]
    public HashSet<BlockPos> Waypoints = new HashSet<BlockPos>();

    [ProtoMember(9)]
    public List<BlockPos> ConstructionQueue = new List<BlockPos>();

    public ICoreAPI Api;

    // Runtime-only flag - not persisted. True while a Gather is active.
    public bool IsGatherActive;

    // Callback ID for the gather auto-clear timer. -1 when no timer is running.
    public long GatherCallbackId = -1;

    public string Id => "village-" + Pos.ToString();

    // Cached EffectiveCenterY result. Structure positions barely move, but the helper
    // is called from per-tick AI code, so recompute at most every few seconds.
    private int effectiveCenterYCache;
    private long effectiveCenterYCacheMs = -1;

    // Naturally generated villages were saved with a bogus centre Y: worldgen passed a
    // chunk-column corner with Y=0 into getMiddle(), which adds a flat +20, so the stored
    // centre of every worldgen village sits ~90 blocks underground on default worlds.
    // Pos cannot be repaired in place because Id derives from it (and Id is persisted in
    // block entities and as the savegame data key), so vertical scans and 3D distance
    // checks must anchor on this instead: the average Y of registered structures, falling
    // back to the terrain height at the centre column, then to Pos.Y itself.
    public int EffectiveCenterY()
    {
        long now = Api?.World?.ElapsedMilliseconds ?? -1;
        if (now != -1 && effectiveCenterYCacheMs != -1 && now - effectiveCenterYCacheMs < 5000)
        {
            return effectiveCenterYCache;
        }

        long sum = 0;
        int count = 0;
        foreach (BlockPos pos in Workstations.Keys)
        {
            if (pos == null) continue;
            sum += pos.Y;
            count++;
        }
        foreach (BlockPos pos in Beds.Keys)
        {
            if (pos == null) continue;
            sum += pos.Y;
            count++;
        }

        int result;
        if (count > 0)
        {
            result = (int)(sum / count);
        }
        else
        {
            int terrainY = 0;
            if (Api != null && Pos != null)
            {
                terrainY = Api.World.BlockAccessor.GetTerrainMapheightAt(Pos);
            }
            result = terrainY > 0 ? terrainY : (Pos?.Y ?? 0);
        }

        if (now != -1)
        {
            effectiveCenterYCache = result;
            effectiveCenterYCacheMs = now;
        }
        return result;
    }

    // Copy of Pos with the Y replaced by EffectiveCenterY. Use for 3D distance checks
    // and goto targets; never as a dictionary key and never to derive Id.
    public BlockPos EffectiveCenter()
    {
        BlockPos center = Pos.Copy();
        center.Y = EffectiveCenterY();
        return center;
    }

    // Hot path. One list alloc instead of the prior chain (ToList -> ConvertAll -> Where -> ToList = 3 lists + enumerator).
    public List<EntityBehaviorVillager> Villagers
    {
        get
        {
            var list = new List<EntityBehaviorVillager>(VillagerSaveData.Count);
            foreach (VillagerData data in VillagerSaveData.Values)
            {
                var beh = Api.World.GetEntityById(data.Id)?.GetBehavior<EntityBehaviorVillager>();
                if (beh != null) list.Add(beh);
            }
            return list;
        }
    }

    public void Init(ICoreAPI api)
    {
        Api = api;
        ScrubNullKeys();
        if (api.Side == EnumAppSide.Server)
        {
            // Delay ghost sweep until initial chunks load. Scales with MaxChunkRadius, 15s default.
            int delayMs = 15000;
            try
            {
                var cfg = (api as Vintagestory.API.Server.ICoreServerAPI)?.Server?.Config;
                if (cfg != null) delayMs = Math.Max(8000, cfg.MaxChunkRadius * 1500);
            }
            catch { }
            api.World.RegisterCallback(delegate { ScrubGhostStructures(); BuildWaypointGraph(); }, delayMs);
        }
    }

    // Removes workstation/bed entries whose block entity no longer exists in the
    // world. Only acts on chunks that are currently loaded - safe to call on startup.
    private void ScrubGhostStructures()
    {
        if (Api == null) return;
        IBlockAccessor ba = Api.World.BlockAccessor;

        List<BlockPos> deadWorkstations = new List<BlockPos>();
        foreach (BlockPos pos in Workstations.Keys)
        {
            if (pos == null) continue;
            // Skip positions in unloaded chunks - null BE is ambiguous there.
            if (!ba.IsValidPos(pos)) continue;
            if (ba.GetChunkAtBlockPos(pos) == null) continue;
            if (ba.GetBlockEntity<BlockEntityVillagerWorkstation>(pos) == null)
            {
                deadWorkstations.Add(pos);
                Api.Logger.Warning("[VsVillage] Village " + Id + ": removing ghost workstation at " + pos);
            }
        }
        foreach (BlockPos pos in deadWorkstations) Workstations.Remove(pos);

        List<BlockPos> deadBeds = new List<BlockPos>();
        foreach (BlockPos pos in Beds.Keys)
        {
            if (pos == null) continue;
            if (!ba.IsValidPos(pos)) continue;
            if (ba.GetChunkAtBlockPos(pos) == null) continue;
            if (ba.GetBlockEntity<BlockEntityVillagerBed>(pos) == null)
            {
                deadBeds.Add(pos);
                Api.Logger.Warning("[VsVillage] Village " + Id + ": removing ghost bed at " + pos);
            }
        }
        foreach (BlockPos pos in deadBeds) Beds.Remove(pos);
    }

    private void ScrubNullKeys()
    {
        foreach (BlockPos key in Workstations.Keys.Where(k => k == null).ToList())
        {
            Workstations.Remove(key);
            Api.Logger.Warning("[VsVillage] Village " + Id + ": removed null-keyed Workstation entry during Init.");
        }

        foreach (BlockPos key in Beds.Keys.Where(k => k == null).ToList())
        {
            Beds.Remove(key);
            Api.Logger.Warning("[VsVillage] Village " + Id + ": removed null-keyed Bed entry during Init.");
        }

        Gatherplaces.RemoveWhere(k =>
        {
            if (k != null) return false;
            Api.Logger.Warning("[VsVillage] Village " + Id + ": removed null Gatherplace entry during Init.");
            return true;
        });

        Waypoints.RemoveWhere(k =>
        {
            if (k != null) return false;
            Api.Logger.Warning("[VsVillage] Village " + Id + ": removed null Waypoint entry during Init.");
            return true;
        });
    }

    public BlockPos FindFreeBed(long villagerId)
    {
        foreach (VillagerBed value in Beds.Values)
        {
            if (value.OwnerId == -1 || value.OwnerId == villagerId)
            {
                value.OwnerId = villagerId;
                string text = Api.World.GetEntityById(villagerId)?.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                BlockEntityVillagerBed blockEntity = Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBed>(value.Pos);
                if (blockEntity != null && !string.IsNullOrEmpty(text))
                {
                    blockEntity.OwnerName = text;
                    blockEntity.MarkDirty();
                }
                return value.Pos;
            }
        }
        return null;
    }

    public BlockPos FindFreeWorkstation(long villagerId, EnumVillagerProfession profession)
    {
        foreach (VillagerWorkstation value in Workstations.Values)
        {
            if (value.Profession == profession && (value.OwnerId == -1 || value.OwnerId == villagerId))
            {
                value.OwnerId = villagerId;
                string text = Api.World.GetEntityById(villagerId)?.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                BlockEntityVillagerWorkstation blockEntity = Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(value.Pos);
                if (blockEntity != null && !string.IsNullOrEmpty(text))
                {
                    blockEntity.OwnerName = text;
                    blockEntity.MarkDirty();
                }
                return value.Pos;
            }
        }
        return null;
    }

    public void ClearBedOwner(long villagerId)
    {
        foreach (VillagerBed value in Beds.Values)
        {
            if (value.OwnerId == villagerId)
            {
                value.OwnerId = -1L;
                BlockEntityVillagerBed blockEntity = Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBed>(value.Pos);
                if (blockEntity != null)
                {
                    blockEntity.OwnerName = null;
                    blockEntity.MarkDirty();
                }
            }
        }
    }

    public BlockPos FindRandomGatherplace()
    {
        if (Gatherplaces.Count == 0)
        {
            return null;
        }
        return Gatherplaces.ElementAt(Api.World.Rand.Next(Gatherplaces.Count));
    }

    public void RemoveVillager(long villagerId)
    {
        VillagerSaveData.Remove(villagerId);
        foreach (VillagerBed value in Beds.Values)
        {
            if (value.OwnerId == villagerId)
            {
                value.OwnerId = -1L;
                BlockEntityVillagerBed blockEntity = Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBed>(value.Pos);
                if (blockEntity != null)
                {
                    blockEntity.OwnerName = null;
                    blockEntity.MarkDirty();
                }
            }
        }
        foreach (VillagerWorkstation value2 in Workstations.Values)
        {
            if (value2.OwnerId == villagerId)
            {
                value2.OwnerId = -1L;
                // Only clear the workstation sign for the departing villager's own station.
                BlockEntityVillagerWorkstation blockEntity2 = Api.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(value2.Pos);
                if (blockEntity2 != null)
                {
                    blockEntity2.OwnerName = null;
                    blockEntity2.MarkDirty();
                }
            }
        }
    }

    public BlockPos FindNearesWaypoint(BlockPos pos)
    {
        BlockPos blockPos = null;
        foreach (BlockPos waypoint in Waypoints)
        {
            if (blockPos == null || waypoint.ManhattanDistance(pos) < blockPos.ManhattanDistance(pos))
            {
                blockPos = waypoint;
            }
        }
        return blockPos;
    }

    public void RemoveWaypoint(BlockPos pos)
    {
        if (Waypoints.Remove(pos)) BuildWaypointGraph();
    }

    public void EnqueueConstruction(BlockPos markerPos)
    {
        if (markerPos == null) return;
        if (ConstructionQueue.Contains(markerPos)) return;
        ConstructionQueue.Add(markerPos);
    }

    public void DequeueConstruction(BlockPos markerPos)
    {
        if (markerPos == null) return;
        ConstructionQueue.Remove(markerPos);
    }

    public BlockPos GetActiveConstruction()
    {
        if (ConstructionQueue.Count == 0) return null;
        return ConstructionQueue[0];
    }

    // Transient - not persisted. Rebuilt from Waypoints on load and on any add/remove.
    public Dictionary<BlockPos, VillageWaypoint> WaypointGraph = new Dictionary<BlockPos, VillageWaypoint>();

    // Rebuilds the waypoint graph from the current Waypoints set.
    // Runs short A* legs between nearby pairs to establish edges, then propagates
    // transitive reachability. Called on load (delayed) and whenever a waypoint is placed or removed.
    public void BuildWaypointGraph()
    {
        WaypointGraph.Clear();
        if (Api == null || Waypoints.Count < 2) return;

        int connectionRange = Math.Max(50, Radius / 4);

        foreach (BlockPos pos in Waypoints)
            WaypointGraph[pos] = new VillageWaypoint { Pos = pos.Copy() };

        ICachingBlockAccessor bac = Api.World.GetCachingBlockAccessor(synchronize: true, relight: false);
        var edgeTester = new WaypointAStar(bac, Api.World);

        var nodes = new List<BlockPos>(WaypointGraph.Keys);
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                BlockPos a = nodes[i];
                BlockPos b = nodes[j];
                if (a.ManhattanDistance(b) > connectionRange) continue;

                bac.Begin();
                var path = edgeTester.FindPath(a, b, searchDepth: 3000);
                bac.Commit();

                if (path != null && path.Count > 1)
                {
                    int dist = path.Count;
                    WaypointGraph[a].SetNeighbour(WaypointGraph[b], dist);
                    WaypointGraph[b].SetNeighbour(WaypointGraph[a], dist);
                }
            }
        }

        // Bellman-Ford style propagation - repeat until reachability is fully transitive.
        int passes = WaypointGraph.Count;
        for (int pass = 0; pass < passes; pass++)
            foreach (VillageWaypoint node in WaypointGraph.Values)
                node.UpdateReachableNodes();
    }
}