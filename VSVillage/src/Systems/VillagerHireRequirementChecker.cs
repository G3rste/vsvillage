using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Newtonsoft.Json;

namespace VsVillage;

public static class VillagerHireRequirementChecker
{
    private const int ProximityRadius = 20;

    private const int VillageScanYPad = 10;

    private const int FarmlandPerFarmer = 20;
    private const int AnimalsPerShepherd = 5;

    private const int MaxSmithsPerRoom = 2;

    private const int RoomScanCap = 35;

    private const int RoomHeightCap = 15;

    private const int MaxPenRadius = 15;
    private const int PenYScan = 3;

    // Cylinder scans are O(r^3) and hire/tooltip calls hit them repeatedly. 5s TTL keeps
    // the result fresh enough that players tilling soil see updates within a click or two.
    private const long ScanCacheTtlMs = 5000;
    private struct CountEntry { public long Stamp; public int Count; }
    private struct FarmlandEntry { public long Stamp; public int Total; public List<string> Keys; public List<int> Values; }
    private static readonly Dictionary<(string villageId, string fragment), CountEntry> _countCache = new Dictionary<(string, string), CountEntry>();
    private static readonly Dictionary<string, FarmlandEntry> _farmlandCache = new Dictionary<string, FarmlandEntry>();

    public static string CheckRequirements(EnumVillagerProfession profession, BlockPos workstationPos, Village village, ICoreAPI api)
    {
        VillagerBed freeBed = village.Beds.Values.FirstOrDefault((VillagerBed b) => b.OwnerId == -1);
        if (freeBed != null)
        {
            string bedError = CheckBedIndoors(freeBed.Pos, api);
            if (bedError != null)
                return bedError;
        }
        return profession switch
        {
            EnumVillagerProfession.farmer => CheckFarmer(workstationPos, village, api),
            EnumVillagerProfession.shepherd => CheckShepherd(workstationPos, village, api),
            EnumVillagerProfession.smith => CheckSmith(workstationPos, village, api),
            EnumVillagerProfession.herbalist => CheckHerbalist(workstationPos, village, api),
            EnumVillagerProfession.trader => CheckTrader(workstationPos, village, api),
            EnumVillagerProfession.soldier => CheckSoldier(workstationPos, village, api),
            EnumVillagerProfession.baker => CheckBaker(workstationPos, village, api),
            EnumVillagerProfession.builder => CheckBuilder(workstationPos, village, api),
            _ => null,
        };
    }

    public static string CheckRequirementsForAssignment(EnumVillagerProfession profession, BlockPos wsPos, Village village, ICoreAPI api)
    {
        return profession switch
        {
            EnumVillagerProfession.farmer => CheckFarmerBase(wsPos, api),
            EnumVillagerProfession.shepherd => CheckShepherdBase(wsPos, api),
            EnumVillagerProfession.smith => CheckSmith(wsPos, village, api),
            EnumVillagerProfession.herbalist => CheckHerbalist(wsPos, village, api),
            EnumVillagerProfession.trader => CheckTrader(wsPos, village, api),
            EnumVillagerProfession.soldier => CheckSoldier(wsPos, village, api),
            EnumVillagerProfession.baker => CheckBaker(wsPos, village, api),
            EnumVillagerProfession.builder => CheckBuilder(wsPos, village, api),
            _ => null
        };
    }

    public static string CheckBedIndoors(BlockPos bedPos, ICoreAPI api)
    {
        Room room = GetRoom(bedPos.UpCopy(), api);
        if (room == null)
            return "The assigned bed must be placed inside a building. Build walls and a roof around it first.";
        return null;
    }

    // === Farmer ===

    private static string CheckFarmerBase(BlockPos wsPos, ICoreAPI api)
    {
        if (!HasBlockNearby(wsPos, ProximityRadius, "farmland", api.World))
            return $"Farmer workstation must be within {ProximityRadius} blocks of a farmland block. Till some soil nearby.";
        return null;
    }

    // === Shepherd ===

    private static string CheckShepherdBase(BlockPos wsPos, ICoreAPI api)
    {
        PenScanResult pen = ScanPen(wsPos, api);

        if (!pen.IsEnclosed)
            return $"Shepherd workstation must be placed inside a pen enclosed by fence or gate blocks " +
                   $"(no larger than {MaxPenRadius * 2 + 1}x{MaxPenRadius * 2 + 1} blocks). " +
                   $"Place the workstation inside the fence line.";

        if (!pen.HasTrough)
            return "The pen must contain at least one animal trough.";

        if (pen.AnimalCount == 0)
            return "The pen must contain at least one livestock animal.";

        return null;
    }

    private static string CheckFarmer(BlockPos wsPos, Village village, ICoreAPI api)
    {
        if (!HasBlockNearby(wsPos, ProximityRadius, "farmland", api.World))
        {
            // Surface the village-wide farmland count so players who have plenty of fields
            // elsewhere don't think the mod is broken - the workstation just isn't close
            // enough to any of them. The farmer is meant to live next to her fields.
            int villageFarmland = CountBlocksInVillage(village, "farmland", api.World);
            return $"Farmer workstation must be within {ProximityRadius} blocks of a farmland block. " +
                   $"Till some soil nearby, or move the workstation closer to your fields. " +
                   $"(Your village contains {villageFarmland} farmland tile(s) total - you may have plenty, just none near this workstation.)";
        }

        int existingFarmers = village.Workstations.Values
            .Count(ws => ws.Profession == EnumVillagerProfession.farmer && ws.OwnerId != -1);
        int required = (existingFarmers + 1) * FarmlandPerFarmer;

        int found = CountBlocksInVillage(village, "farmland", api.World);
        if (found < required)
            return $"Your village needs at least {required} farmland blocks to support {existingFarmers + 1} farmer(s). " +
                   $"Found {found} within the village boundary. Till more soil or expand your fields.";

        return null;
    }

    private static string CheckShepherd(BlockPos wsPos, Village village, ICoreAPI api)
    {
        // === Local check: this pen must be valid, have a trough, and at least one animal ===
        PenScanResult localPen = ScanPen(wsPos, api);

        if (!localPen.IsEnclosed)
            return $"Shepherd workstation must be placed inside a pen enclosed by fence or gate blocks " +
                   $"(no larger than {MaxPenRadius * 2 + 1}x{MaxPenRadius * 2 + 1} blocks). " +
                   $"Place the workstation inside the fence line.";

        if (!localPen.HasTrough)
            return "The pen must contain at least one animal trough.";

        if (localPen.AnimalCount == 0)
            return "The pen must contain at least one livestock animal.";

        // === Village-wide aggregate ===
        // Collect all shepherd workstation positions including this one.
        var shepherdPositions = village.Workstations.Values
            .Where(ws => ws.Profession == EnumVillagerProfession.shepherd)
            .Select(ws => ws.Pos)
            .ToList();

        if (!shepherdPositions.Any(p => p.Equals(wsPos)))
            shepherdPositions.Add(wsPos);

        int existingShepherds = village.Workstations.Values
            .Count(ws => ws.Profession == EnumVillagerProfession.shepherd && ws.OwnerId != -1);

        IBlockAccessor ba = api.World.BlockAccessor;
        int totalTroughs = 0;
        var mergedCells = new HashSet<(int x, int z)>();

        foreach (BlockPos pos in shepherdPositions)
        {
            HashSet<(int x, int z)> cells = GetPenCells(pos, ba);
            if (cells.Count == 0) continue; // unenclosed - skip

            if (PenHasTrough(cells, pos, ba)) totalTroughs++;

            foreach (var cell in cells)
                mergedCells.Add(cell);
        }

        // Single deduplicated animal count across all pen cells.
        int totalAnimals = CountAnimalsInCells(mergedCells, wsPos, village, api);

        int requiredTroughs = existingShepherds + 1;
        if (totalTroughs < requiredTroughs)
            return $"Your village needs at least {requiredTroughs} pen(s) with a trough to support " +
                   $"{existingShepherds + 1} shepherd(s). Found {totalTroughs} valid pen(s) with troughs.";

        int requiredAnimals = (existingShepherds + 1) * AnimalsPerShepherd;
        if (totalAnimals < requiredAnimals)
            return $"Your village needs at least {requiredAnimals} livestock animals across all shepherd pens " +
                   $"to support {existingShepherds + 1} shepherd(s). Found {totalAnimals}. " +
                   $"Each pen is scanned from its workstation outward to fence/gate walls.";

        return null;
    }

    // Pen scanning

    private readonly struct PenScanResult
    {
        public readonly bool IsEnclosed;
        public readonly bool HasTrough;
        public readonly int AnimalCount;

        public PenScanResult(bool isEnclosed, bool hasTrough, int animalCount)
        {
            IsEnclosed = isEnclosed;
            HasTrough = hasTrough;
            AnimalCount = animalCount;
        }
    }

    // True if animalPos is inside the pen footprint reachable from wsPos via the same BFS the hire check validated.
    public static bool IsAnimalInShepherdPen(BlockPos wsPos, BlockPos animalPos, IBlockAccessor ba)
    {
        if (wsPos == null || animalPos == null) return false;
        HashSet<(int x, int z)> cells = GetPenCells(wsPos, ba);
        if (cells.Count == 0) return false;
        return cells.Contains((animalPos.X, animalPos.Z));
    }

    // BFS from wsPos, stops at fence/gate barriers or MaxPenRadius.
    // Returns pen state including trough presence and animal count within the reached cells.
    private static PenScanResult ScanPen(BlockPos wsPos, ICoreAPI api)
    {
        IBlockAccessor ba = api.World.BlockAccessor;
        HashSet<(int x, int z)> cells = GetPenCells(wsPos, ba);

        if (cells.Count == 0)
            return new PenScanResult(false, false, 0);

        bool hasTrough = PenHasTrough(cells, wsPos, ba);
        int animalCount = CountAnimalsInCells(cells, wsPos, null, api);

        return new PenScanResult(true, hasTrough, animalCount);
    }

    // Returns the XZ cell footprint of the pen reachable from wsPos by BFS,
    // bounded by fence/gate blocks and MaxPenRadius.
    // Returns an empty set if the pen is unenclosed (BFS escapes the radius).
    private static HashSet<(int x, int z)> GetPenCells(BlockPos wsPos, IBlockAccessor ba)
    {
        var visited = new HashSet<(int x, int z)>();
        var queue = new Queue<(int x, int z)>();

        visited.Add((wsPos.X, wsPos.Z));
        queue.Enqueue((wsPos.X, wsPos.Z));

        int[] dxs = { 1, -1, 0, 0 };
        int[] dzs = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var (cx, cz) = queue.Dequeue();

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dxs[i];
                int nz = cz + dzs[i];

                if (Math.Abs(nx - wsPos.X) > MaxPenRadius ||
                    Math.Abs(nz - wsPos.Z) > MaxPenRadius)
                    return new HashSet<(int x, int z)>(); // unenclosed

                if (visited.Contains((nx, nz))) continue;
                visited.Add((nx, nz));

                bool blocked = false;
                for (int dy = -1; dy <= 1 && !blocked; dy++)
                {
                    int by = wsPos.Y + dy;
                    Block body = ba.GetBlock(new BlockPos(nx, by, nz));
                    Block head = ba.GetBlock(new BlockPos(nx, by + 1, nz));
                    if (IsPenBarrier(body) || IsPenBarrier(head))
                        blocked = true;
                }

                if (blocked) continue;
                queue.Enqueue((nx, nz));
            }
        }

        return visited;
    }

    // Checks whether any block in the Y column (wsPos.Y +/- PenYScan) at any
    // cell in the set contains a trough.
    private static bool PenHasTrough(HashSet<(int x, int z)> cells, BlockPos wsPos, IBlockAccessor ba)
    {
        foreach (var (cx, cz) in cells)
        {
            for (int y = wsPos.Y - PenYScan; y <= wsPos.Y + PenYScan; y++)
            {
                Block b = ba.GetBlock(new BlockPos(cx, y, cz));
                if (b?.Code?.Path?.Contains("trough") == true)
                    return true;
            }
        }
        return false;
    }

    // Counts livestock animals whose XZ position falls within the cell set.
    // Uses the village centre for the broad entity query when available, wsPos otherwise.
    private static int CountAnimalsInCells(HashSet<(int x, int z)> cells, BlockPos wsPos, Village village, ICoreAPI api)
    {
        if (cells.Count == 0) return 0;

        BlockPos searchCenter = village?.Pos ?? wsPos;
        float searchRadius = (village?.Radius ?? MaxPenRadius) + 2f;
        float searchHeight = PenYScan + 14f;

        // Anchor the query vertically on the workstation - the count filter below is
        // wsPos.Y-relative, and village.Pos.Y is unreliable (worldgen stores ~20).
        Vec3d centerVec = new Vec3d(searchCenter.X + 0.5, wsPos.Y + 0.5, searchCenter.Z + 0.5);
        Entity[] nearby = api.World.GetEntitiesAround(centerVec, searchRadius, searchHeight);

        int count = 0;
        foreach (Entity e in nearby)
        {
            if (!IsLivestockEntity(e)) continue;
            BlockPos ePos = e.Pos.XYZ.AsBlockPos;
            if (!cells.Contains((ePos.X, ePos.Z))) continue;
            if (Math.Abs(ePos.Y - wsPos.Y) > PenYScan + 2) continue;
            count++;
        }
        return count;
    }

    // Livestock entity identification

    private static readonly string[] LorePrefixes =
    {
        "bell-", "bellmini-", "bowtorn-", "drifter-", "locust-", "shiver-"
    };
    private static readonly string[] LoreExact = { "mechhelper" };

    private static readonly HashSet<string> KnownLivestockSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "chicken", "duck", "sheep", "yak", "goat", "pig", "swan", "cow"
    };

    private static bool IsLoreEntity(string path)
    {
        if (path == null) return false;
        foreach (string e in LoreExact)
            if (path == e) return true;
        foreach (string p in LorePrefixes)
            if (path.StartsWith(p)) return true;
        return false;
    }

    private static bool IsLivestockEntity(Entity entity)
    {
        if (!entity.Alive) return false;
        if (entity is EntityPlayer) return false;

        string path = entity.Code?.Path;
        if (string.IsNullOrEmpty(path)) return false;
        if (entity.Code.Domain == "vsvillage") return false;
        if (IsLoreEntity(path)) return false;

        if (path.EndsWith("-male") || path.EndsWith("-female") || path.EndsWith("-baby"))
            return true;

        string firstSegment = path.Split('-')[0];
        return KnownLivestockSpecies.Contains(firstSegment);
    }

    // Public stat methods

    public static (int total, List<string> keys, List<int> values) GetFarmlandStats(Village village, ICoreAPI api)
    {
        if (village?.Pos == null) return (0, new List<string>(), new List<int>());

        long now = api.World.ElapsedMilliseconds;
        if (_farmlandCache.TryGetValue(village.Id, out FarmlandEntry hit) && now - hit.Stamp < ScanCacheTtlMs)
            return (hit.Total, hit.Keys, hit.Values);

        BlockPos center = village.Pos;
        int r = village.Radius;
        // Y-pad scales with village radius capped at 75. Matches marketstall scan.
        // Prior hardcoded 10 missed terraced fields and mountain villages.
        int yPad = Math.Min(village.Radius, 75);
        // Worldgen villages store Pos.Y ~20 (see Village.EffectiveCenterY) - anchoring
        // the Y band there scans bedrock instead of the fields.
        int centerY = village.EffectiveCenterY();
        IBlockAccessor ba = api.World.BlockAccessor;
        BlockPos tmp = new BlockPos(0);
        BlockPos above = new BlockPos(0);
        int total = 0;
        var cropCounts = new Dictionary<string, int>();

        for (int x = center.X - r; x <= center.X + r; x++)
        {
            int dx = x - center.X;
            for (int z = center.Z - r; z <= center.Z + r; z++)
            {
                int dz = z - center.Z;
                if (dx * dx + dz * dz > r * r) continue;
                for (int y = centerY - yPad; y <= centerY + yPad; y++)
                {
                    tmp.Set(x, y, z);
                    Block b = ba.GetBlock(tmp);
                    if (b?.Code?.Path?.Contains("farmland") != true) continue;
                    total++;

                    above.Set(x, y + 1, z);
                    Block cropBlock = ba.GetBlock(above);
                    string cropPath = cropBlock?.Code?.Path;
                    if (cropPath != null && cropPath.StartsWith("crop-"))
                    {
                        string[] parts = cropPath.Split('-');
                        if (parts.Length >= 2 && parts[1].Length > 0)
                        {
                            string name = parts[1];
                            name = char.ToUpper(name[0]) + name.Substring(1);
                            cropCounts.TryGetValue(name, out int existing);
                            cropCounts[name] = existing + 1;
                        }
                    }
                }
            }
        }

        var keys = new List<string>(cropCounts.Keys);
        var values = new List<int>();
        foreach (string k in keys) values.Add(cropCounts[k]);
        _farmlandCache[village.Id] = new FarmlandEntry { Stamp = now, Total = total, Keys = keys, Values = values };
        return (total, keys, values);
    }

    // Returns total enclosed-animal count across all shepherd workstation pens,
    // deduplicated via cell union so overlapping or shared pens don't double-count.
    public static (int total, List<string> keys, List<int> values) GetLivestockStats(Village village, ICoreAPI api)
    {
        if (village?.Pos == null) return (0, new List<string>(), new List<int>());

        IBlockAccessor ba = api.World.BlockAccessor;

        // Union all pen cell footprints first - overlapping pens share cells, not animals.
        var mergedCells = new HashSet<(int x, int z)>();
        BlockPos anyWsPos = null;

        foreach (VillagerWorkstation ws in village.Workstations.Values)
        {
            if (ws.Profession != EnumVillagerProfession.shepherd || ws.Pos == null) continue;
            HashSet<(int x, int z)> cells = GetPenCells(ws.Pos, ba);
            if (cells.Count == 0) continue;
            foreach (var cell in cells)
                mergedCells.Add(cell);
            anyWsPos ??= ws.Pos;
        }

        if (mergedCells.Count == 0 || anyWsPos == null)
            return (0, new List<string>(), new List<int>());

        // Single entity query over the village boundary. Anchored vertically on a real
        // pen workstation - the count filter below is anyWsPos.Y-relative, and
        // village.Pos.Y is unreliable (worldgen stores ~20).
        float searchRadius = village.Radius + 2f;
        float searchHeight = VillageScanYPad + 14f;
        Vec3d centerVec = new Vec3d(village.Pos.X + 0.5, anyWsPos.Y + 0.5, village.Pos.Z + 0.5);
        Entity[] nearby = api.World.GetEntitiesAround(centerVec, searchRadius, searchHeight);

        var animalCounts = new Dictionary<string, int>();
        int total = 0;

        foreach (Entity e in nearby)
        {
            if (!IsLivestockEntity(e)) continue;
            BlockPos ePos = e.Pos.XYZ.AsBlockPos;
            if (!mergedCells.Contains((ePos.X, ePos.Z))) continue;
            if (Math.Abs(ePos.Y - anyWsPos.Y) > PenYScan + 2) continue;

            total++;
            string epath = e.Code?.Path ?? "unknown";
            string first = epath.Split('-')[0];
            if (first.Length == 0) first = "unknown";
            string name = char.ToUpper(first[0]) + first.Substring(1);
            animalCounts.TryGetValue(name, out int cnt);
            animalCounts[name] = cnt + 1;
        }

        var keys = new List<string>(animalCounts.Keys);
        var values = new List<int>();
        foreach (string k in keys) values.Add(animalCounts[k]);
        return (total, keys, values);
    }

    // Pen barrier check

    private static bool IsPenBarrier(Block block)
    {
        if (block?.Code == null) return false;
        // isFence is vanilla's canonical fence attribute. Wattle fence sets it but
        // its code path is "wattle-..." with no "fence" substring, which the old
        // check missed - the BFS then walked right past wattle pens.
        if (block.Attributes?["isFence"].AsBool(false) == true) return true;
        string path = block.Code.Path;
        return path.Contains("fence") || path.Contains("gate");
    }

    // Other profession checks

    // === Smith ===

    private static string CheckSmith(BlockPos wsPos, Village village, ICoreAPI api)
    {
        Room room = GetRoom(wsPos, api);
        if (room == null)
            return "Smith workstation must be inside a building.";

        List<Block> roomBlocks = GetBlocksInRoom(room, api.World);
        foreach (Block b in roomBlocks)
        {
            if (IsVsWorkstation(b) && !IsWorkstationOfProfession(b, "smith"))
                return "Smith room cannot contain workstations of other professions. Move other workstations out first.";
        }
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("anvil") == true))
            return "Smith room requires an anvil (place game:anvil-* inside the room).";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("forge") == true))
            return "Smith room requires a forge (place game:forge inside the room).";
        if (!HasLightSource(roomBlocks))
            return "Smith room requires a light source (oil lamp or wall torch).";

        int smithsInRoom = CountWorkstationsOfProfessionInRoom(room, EnumVillagerProfession.smith, village);
        if (smithsInRoom > MaxSmithsPerRoom)
            return $"This room already has the maximum number of smiths ({MaxSmithsPerRoom}). Build a separate smithy.";

        return null;
    }

    // === Herbalist ===

    private static string CheckHerbalist(BlockPos wsPos, Village village, ICoreAPI api)
    {
        Room room = GetRoom(wsPos, api);
        if (room == null)
            return "Herbalist workstation must be inside a building.";

        List<Block> roomBlocks = GetBlocksInRoom(room, api.World);
        foreach (Block b in roomBlocks)
        {
            if (IsVsWorkstation(b) && !IsWorkstationOfProfession(b, "herbalist"))
                return "Herbalist room cannot share space with workstations of other professions.";
        }
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("table") == true))
            return "Herbalist room requires a table.";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("barrel") == true))
            return "Herbalist room requires a barrel.";
        if (!roomBlocks.Any(b =>
                b.Code?.Path?.Contains("reedchest") == true ||
                b.Code?.Path?.Contains("reedbasket") == true ||
                b.Code?.Path?.Contains("basket") == true))
            return "Herbalist room requires a reed basket chest.";
        if (!HasLightSource(roomBlocks))
            return "Herbalist room requires a light source (oil lamp or wall torch).";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("cabinet") == true))
            return "Herbalist room requires a cabinet.";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("flowerpot") == true))
            return "Herbalist room requires a flower pot (the herbalist will plant horsetail in it themselves).";

        return null;
    }

    // === Trader ===

    private static string CheckTrader(BlockPos wsPos, Village village, ICoreAPI api)
    {
        Room room = GetRoom(wsPos, api);
        if (room == null)
            return "Trader workstation must be inside a building.";

        List<Block> roomBlocks = GetBlocksInRoom(room, api.World);
        foreach (Block b in roomBlocks)
        {
            if (IsVsWorkstation(b) && !IsWorkstationOfProfession(b, "trader"))
                return "Trader room cannot share space with workstations of other professions.";
        }
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("table") == true))
            return "Trader room requires a table.";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("crate") == true))
            return "Trader room requires a crate.";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("chest") == true))
            return "Trader room requires a chest.";
        if (!HasLightSource(roomBlocks))
            return "Trader room requires a light source (oil lamp or wall torch).";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("shelf") == true))
            return "Trader room requires a shelf (place a wood shelf inside the room).";

        return null;
    }

    // === Soldier / Archer ===

    private static string CheckSoldier(BlockPos wsPos, Village village, ICoreAPI api)
    {
        Room room = GetRoom(wsPos, api);
        if (room == null)
            return "Soldier/Archer workstation must be inside a building.";

        List<Block> roomBlocks = GetBlocksInRoom(room, api.World);
        foreach (Block b in roomBlocks)
        {
            if (IsVsWorkstation(b) && !IsWorkstationOfProfession(b, "soldier"))
                return "Soldier/Archer room cannot contain civilian workstations. Dedicate this room to combat professions.";
        }
        // Hire requires a tool rack with at least one spear or bow in its inventory.
        // Block-presence alone would pass empty racks.
        if (!HasLoadedToolrack(room, api.World))
            return "Soldier/Archer room requires a tool rack loaded with a spear or bow.";

        Cuboidi barrackLoc = room.Location;
        if (barrackLoc.X2 - barrackLoc.X1 < 6 || barrackLoc.Z2 - barrackLoc.Z1 < 6)
            return "Soldier/Archer barracks must be at least 7x7 blocks in floor area to house and train combat personnel.";

        return null;
    }

    // === Baker ===

    private static string CheckBaker(BlockPos wsPos, Village village, ICoreAPI api)
    {
        Room room = GetRoom(wsPos, api);
        if (room == null)
            return "Baker workstation must be inside a building.";

        List<Block> roomBlocks = GetBlocksInRoom(room, api.World);
        foreach (Block b in roomBlocks)
        {
            if (IsVsWorkstation(b) && !IsWorkstationOfProfession(b, "baker"))
                return "Baker room can only contain baker workstations. Move other workstations out first.";
        }
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("clayoven") == true))
            return "Baker room requires a clay oven.";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("firepit") == true))
            return "Baker room requires a firepit.";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("barrel") == true))
            return "Baker room requires a barrel (the baker will fill it with water themselves).";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("storagevessel") == true))
            return "Baker room requires a storage vessel for ingredients.";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("table") == true))
            return "Baker room requires a table for kneading.";

        return null;
    }

    // Shared block/room helpers

    private static Room GetRoom(BlockPos pos, ICoreAPI api)
    {
        try
        {
            Room room = api.ModLoader.GetModSystem<RoomRegistry>()?.GetRoomForPosition(pos);
            if (room == null) return null;
            if (!room.Contains(pos)) return null;
            if (room.ExitCount > 0) return null;
            return room;
        }
        catch
        {
            return null;
        }
    }

    // True if the room contains at least one toolrack with a spear or bow in any of its 4 inventory slots.
    // Walks the same cells GetBlocksInRoom does but keeps positions so BlockEntityToolrack can be resolved.
    private static bool HasLoadedToolrack(Room room, IWorldAccessor world)
    {
        IBlockAccessor ba = world.BlockAccessor;
        Cuboidi loc = room.Location;
        int x1 = loc.X1;
        int x2 = Math.Min(loc.X2, x1 + RoomScanCap);
        int y1 = loc.Y1;
        int y2 = Math.Min(loc.Y2, y1 + RoomHeightCap);
        int z1 = loc.Z1;
        int z2 = Math.Min(loc.Z2, z1 + RoomScanCap);
        BlockPos tmp = new BlockPos(0);
        for (int i = x1; i <= x2; i++)
            for (int j = y1; j <= y2; j++)
                for (int k = z1; k <= z2; k++)
                {
                    tmp.Set(i, j, k);
                    Block b = ba.GetBlock(tmp);
                    string path = b?.Code?.Path;
                    if (string.IsNullOrEmpty(path)) continue;
                    if (!path.Contains("toolrack") && !path.Contains("tool-rack")) continue;
                    BlockEntityToolrack rack = ba.GetBlockEntity<BlockEntityToolrack>(tmp);
                    if (rack?.inventory == null) continue;
                    for (int s = 0; s < rack.inventory.Count; s++)
                    {
                        string itemPath = rack.inventory[s]?.Itemstack?.Collectible?.Code?.Path;
                        if (string.IsNullOrEmpty(itemPath)) continue;
                        if (itemPath.StartsWith("bow-") || itemPath.StartsWith("spear-"))
                            return true;
                    }
                }
        return false;
    }

    private static List<Block> GetBlocksInRoom(Room room, IWorldAccessor world)
    {
        List<Block> blocks = new List<Block>();
        IBlockAccessor ba = world.BlockAccessor;
        Cuboidi loc = room.Location;
        int x1 = loc.X1;
        int x2 = Math.Min(loc.X2, x1 + RoomScanCap);
        int y1 = loc.Y1;
        int y2 = Math.Min(loc.Y2, y1 + RoomHeightCap);
        int z1 = loc.Z1;
        int z2 = Math.Min(loc.Z2, z1 + RoomScanCap);
        BlockPos tmp = new BlockPos(0);
        for (int i = x1; i <= x2; i++)
        {
            for (int j = y1; j <= y2; j++)
            {
                for (int k = z1; k <= z2; k++)
                {
                    tmp.Set(i, j, k);
                    Block b = ba.GetBlock(tmp);
                    if (b?.Code != null)
                        blocks.Add(b);
                }
            }
        }
        return blocks;
    }

    private static bool HasBlockNearby(BlockPos center, int radius, string codeFragment, IWorldAccessor world)
    {
        IBlockAccessor ba = world.BlockAccessor;
        BlockPos tmp = new BlockPos(0);
        for (int x = center.X - radius; x <= center.X + radius; x++)
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
                for (int z = center.Z - radius; z <= center.Z + radius; z++)
                {
                    tmp.Set(x, y, z);
                    Block b = ba.GetBlock(tmp);
                    if (b?.Code?.Path?.Contains(codeFragment) == true)
                        return true;
                }
        return false;
    }

    private static bool HasLightSource(List<Block> roomBlocks)
    {
        return roomBlocks.Any(b =>
            b.Code?.Path?.Contains("oillamp") == true ||
            b.Code?.Path?.Contains("torchholder") == true ||
            b.Code?.Path?.Contains("torch") == true ||
            b.Code?.Path?.Contains("lantern") == true ||
            // Vanilla-compliant fallback: any block with intrinsic light emission.
            // LightHsv is byte[3]; index [2] is brightness (0..32). Any positive value
            // means the block emits light - catches mod blocks (Better Ruins / NDL torch
            // holders / etc.) whose code paths don't match our hardcoded patterns. Also
            // correctly excludes empty torchholders that would have matched "torch" but
            // emit no light.
            (b != null && b.LightHsv[2] > 0));
    }

    private static int CountBlocksInVillage(Village village, string codeFragment, IWorldAccessor world)
    {
        long now = world.ElapsedMilliseconds;
        var key = (village.Id, codeFragment);
        if (_countCache.TryGetValue(key, out CountEntry hit) && now - hit.Stamp < ScanCacheTtlMs)
            return hit.Count;

        BlockPos center = village.Pos;
        int r = village.Radius;
        // Y-pad scales with village radius capped at 75. Matches marketstall scan.
        // Prior hardcoded 10 missed terraced fields and mountain villages.
        int yPad = Math.Min(village.Radius, 75);
        // Worldgen villages store Pos.Y ~20 (see Village.EffectiveCenterY) - anchoring
        // the Y band there scans bedrock instead of the fields.
        int centerY = village.EffectiveCenterY();
        IBlockAccessor ba = world.BlockAccessor;
        BlockPos tmp = new BlockPos(0);
        int count = 0;

        for (int x = center.X - r; x <= center.X + r; x++)
        {
            int dx = x - center.X;
            for (int z = center.Z - r; z <= center.Z + r; z++)
            {
                int dz = z - center.Z;
                if (dx * dx + dz * dz > r * r) continue;
                for (int y = centerY - yPad; y <= centerY + yPad; y++)
                {
                    tmp.Set(x, y, z);
                    Block b = ba.GetBlock(tmp);
                    if (b?.Code?.Path?.Contains(codeFragment) == true)
                        count++;
                }
            }
        }

        _countCache[key] = new CountEntry { Stamp = now, Count = count };
        return count;
    }

    private static bool IsVsWorkstation(Block b)
    {
        return b.Code?.Domain == "vsvillage" && b.Code.Path.StartsWith("workstation-");
    }

    private static bool IsWorkstationOfProfession(Block b, string profession)
    {
        return b.Code?.Path?.Contains("workstation-" + profession) == true;
    }

    // === Builder ===

    private static string CheckBuilder(BlockPos wsPos, Village village, ICoreAPI api)
    {
        Room room = GetRoom(wsPos, api);
        if (room == null)
            return "Builder workstation must be inside a building.";

        List<Block> roomBlocks = GetBlocksInRoom(room, api.World);
        foreach (Block b in roomBlocks)
        {
            if (IsVsWorkstation(b) && !IsWorkstationOfProfession(b, "builder"))
                return "Builder room cannot contain workstations of other professions.";
        }
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("crate") == true))
            return "Builder room requires a crate.";
        if (!roomBlocks.Any(b => b.Code?.Path?.Contains("table") == true))
            return "Builder room requires a table.";

        (bool hasHammer, bool hasSaw) = ScanBuilderToolrack(room, api.World);
        if (!hasHammer)
            return "Builder room requires a tool rack loaded with a hammer.";
        if (!hasSaw)
            return "Builder room requires a tool rack loaded with a saw.";

        return null;
    }

    // Scans all tool racks in the room. Returns whether any rack holds a hammer and any holds a saw.
    private static (bool hasHammer, bool hasSaw) ScanBuilderToolrack(Room room, IWorldAccessor world)
    {
        bool hasHammer = false;
        bool hasSaw = false;
        IBlockAccessor ba = world.BlockAccessor;
        Cuboidi loc = room.Location;
        int x1 = loc.X1, x2 = Math.Min(loc.X2, x1 + RoomScanCap);
        int y1 = loc.Y1, y2 = Math.Min(loc.Y2, y1 + RoomHeightCap);
        int z1 = loc.Z1, z2 = Math.Min(loc.Z2, z1 + RoomScanCap);
        BlockPos tmp = new BlockPos(0);
        for (int i = x1; i <= x2; i++)
            for (int j = y1; j <= y2; j++)
                for (int k = z1; k <= z2; k++)
                {
                    tmp.Set(i, j, k);
                    Block b = ba.GetBlock(tmp);
                    string path = b?.Code?.Path;
                    if (string.IsNullOrEmpty(path)) continue;
                    if (!path.Contains("toolrack") && !path.Contains("tool-rack")) continue;
                    BlockEntityToolrack rack = ba.GetBlockEntity<BlockEntityToolrack>(tmp);
                    if (rack?.inventory == null) continue;
                    for (int s = 0; s < rack.inventory.Count; s++)
                    {
                        string itemPath = rack.inventory[s]?.Itemstack?.Collectible?.Code?.Path;
                        if (string.IsNullOrEmpty(itemPath)) continue;
                        if (itemPath.StartsWith("hammer-")) hasHammer = true;
                        if (itemPath.StartsWith("saw-")) hasSaw = true;
                    }
                    if (hasHammer && hasSaw) return (true, true);
                }
        return (hasHammer, hasSaw);
    }

    private static int CountWorkstationsOfProfessionInRoom(Room room, EnumVillagerProfession profession, Village village)
    {
        Cuboidi loc = room.Location;
        int count = 0;
        foreach (VillagerWorkstation ws in village.Workstations.Values)
        {
            if (ws.Profession == profession)
            {
                BlockPos p = ws.Pos;
                if (p.X >= loc.X1 && p.X <= loc.X2 && p.Y >= loc.Y1 && p.Y <= loc.Y2 && p.Z >= loc.Z1 && p.Z <= loc.Z2)
                    count++;
            }
        }
        return count;
    }
}