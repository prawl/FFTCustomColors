using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Loads pre-parsed FFT map terrain data from JSON files (fft-map-json format).
    /// Grid cursor coordinates map directly to tile array indices (identity mapping).
    /// Display height = tile.height + tile.slope_height / 2.0
    /// </summary>
    public class MapLoader
    {
        private string _mapDataDir;
        public string MapDataDir => _mapDataDir;
        private MapData? _currentMap;
        private int _currentMapNumber = -1;

        public MapData? CurrentMap => _currentMap;
        public int CurrentMapNumber => _currentMapNumber;

        /// <summary>Map ID → display name from MapTrapFormationData.xml.</summary>
        public static string? GetMapName(int mapId) =>
            MapNames.TryGetValue(mapId, out var name) ? name : null;

        private static readonly Dictionary<int, string> MapNames = new()
        {
            [1] = "Eagrose Castle Gate",
            [2] = "Lesalia Castle Postern",
            [3] = "Mullonde Cathedral Nave",
            [4] = "Lesalia Castle Study",
            [5] = "Riovanes Castle Roof",
            [6] = "Riovanes Castle Gate",
            [7] = "Riovanes Castle Keep",
            [8] = "Riovanes Castle",
            [9] = "Eagrose Castle Solar",
            [10] = "Eagrose Castle Keep",
            [11] = "Eagrose Castle Solar",
            [12] = "Lionel Castle Gate",
            [13] = "Lionel Castle Oratory",
            [14] = "Lionel Castle Parlor",
            [15] = "Limberry Castle Gate",
            [16] = "Limberry Castle Keep",
            [17] = "Limberry Castle Undercroft",
            [18] = "Limberry Castle Parlor",
            [19] = "Limberry Castle Gate",
            [20] = "Zeltennia Castle Keep",
            [21] = "Zeltennia Castle",
            [22] = "Gariland",
            [23] = "The Beoulve Manse",
            [24] = "The Royal Military Akademy",
            [25] = "Yardrow",
            [26] = "Yardrow Armory",
            [27] = "Gollund",
            [28] = "Gollund Colliery Ridge",
            [29] = "Gollund Colliery Slope",
            [30] = "Gollund Colliery Floor",
            [31] = "Dorter",
            [32] = "Dorter Slums",
            [33] = "Slum Infirmary",
            [34] = "The Sand Rat's Sietch",
            [35] = "Zaland",
            [36] = "Outlying Church",
            [37] = "Zaland Outskirts",
            [38] = "Goug",
            [39] = "Gollund Colliery Shaft",
            [40] = "Goug Lowtown",
            [41] = "Bunansa Residence",
            [42] = "Warjilis",
            [43] = "Warjilis Harbor",
            [44] = "Bervenia",
            [45] = "Zeltennia Castle Chapel Ruins",
            [46] = "The Tomb of Barbaneth Beoulve",
            [47] = "Sal Ghidos",
            [48] = "Sal Ghidos Slumtown",
            [49] = "Ziekden Fortress",
            [50] = "Mullonde Cathedral",
            [51] = "Mullonde Cathedral",
            [52] = "Mullonde Cathedral Sanctuary",
            [53] = "The Imperial Capitoline",
            [54] = "Lost Halidom",
            [55] = "Airship Graveyard",
            [56] = "Orbonne Monastery",
            [57] = "Monastery Vaults First Level",
            [58] = "Monastery Vaults Second Level",
            [59] = "Monastery Vaults Third Level",
            [60] = "Monastery Vaults Fourth Level",
            [61] = "Monastery Vaults Fifth Level",
            [62] = "Orbonne Monastery",
            [63] = "Golgollada Gallows",
            [64] = "Fort Besselat Sluice",
            [65] = "Fort Besselat Granary",
            [66] = "Fort Besselat South Wall",
            [67] = "Fort Besselat North Wall",
            [68] = "Fort Besselat",
            [69] = "The Necrohol of Mullonde",
            [70] = "Nelveska Temple",
            [71] = "Dorvauldar Marsh",
            [72] = "Fovoham Windflats",
            [73] = "Mill Interior",
            [74] = "The Siedge Weald",
            [75] = "Mount Bervenia",
            [76] = "Zeklaus Desert",
            [77] = "Lenalian Plateau",
            [78] = "Tchigolith Fenlands",
            [79] = "The Yuguewood",
            [80] = "Araguay Woods",
            [81] = "Grogh Heights",
            [82] = "Beddha Sandwaste",
            [83] = "Zeirchele Falls",
            [84] = "Balias Tor",
            [85] = "Mandalia Plain",
            [86] = "Dugeura Pass",
            [87] = "Balias Swale",
            [88] = "Finnath Creek",
            [89] = "Lake Poescas",
            [90] = "Mount Germinas",
            [91] = "Brigands' Den",
            [92] = "Eagrose The Beoulve Manse",
            [93] = "Rooftops Wood",
            [94] = "Rooftops Stone",
            [95] = "Church",
            [96] = "Tavern",
            [100] = "Cemetery",
            [103] = "Windflat Mill",
            [115] = "Abandoned Watchtower",
        };

        public MapLoader(string mapDataDir)
        {
            _mapDataDir = mapDataDir;
            // Always prefer the repo's FFTHandsFree/maps/ directory (has all 122 maps + location_maps.json)
            var dir = new DirectoryInfo(mapDataDir);
            while (dir?.Parent != null)
            {
                dir = dir.Parent;
                var repoMaps = Path.Combine(dir.FullName, "FFTHandsFree", "maps");
                if (Directory.Exists(repoMaps) && Directory.GetFiles(repoMaps, "MAP*.json").Length > 10)
                {
                    _mapDataDir = repoMaps;
                    ModLogger.Log($"[MapLoader] Using repo maps dir: {repoMaps}");
                    break;
                }
            }
        }

        /// <summary>
        /// Load a specific map by number (e.g., 74 for MAP074.json).
        /// Returns null if file not found or parse fails.
        /// </summary>
        public MapData? LoadMap(int mapNumber)
        {
            if (mapNumber == _currentMapNumber && _currentMap != null)
                return _currentMap;

            var path = Path.Combine(_mapDataDir, $"MAP{mapNumber:D3}.json");
            if (!File.Exists(path))
            {
                ModLogger.LogError($"[MapLoader] Map file not found: {path}");
                return null;
            }

            var map = LoadMapFromFile(path, mapNumber);
            if (map == null)
            {
                ModLogger.LogError($"[MapLoader] Failed to parse MAP{mapNumber:D3}");
                return null;
            }

            _currentMap = map;
            _currentMapNumber = mapNumber;
            ModLogger.Log($"[MapLoader] Loaded MAP{mapNumber:D3}: {map.Width}x{map.Height}");
            return _currentMap;
        }

        private readonly HashSet<int> _rejectedMaps = new();

        public void ClearMap()
        {
            _currentMap = null;
            _currentMapNumber = -1;
        }

        /// <summary>
        /// Reject the current map (wrong terrain detected during gameplay).
        /// Next scan_move will try a different candidate.
        /// </summary>
        public void RejectCurrentMap()
        {
            if (_currentMapNumber >= 0)
            {
                _rejectedMaps.Add(_currentMapNumber);
                ModLogger.Log($"[MapLoader] Rejected MAP{_currentMapNumber:D3} — will try alternatives next scan");
                ClearMap();
            }
        }

        /// <summary>Clear rejections (new battle at a new location).</summary>
        public void ClearRejections()
        {
            _rejectedMaps.Clear();
        }

        private Dictionary<int, MapData>? _allMaps;

        /// <summary>
        /// Load all maps from disk (one-time, ~12MB). Used for fingerprint detection.
        /// </summary>
        private void EnsureAllMapsLoaded()
        {
            if (_allMaps != null) return;
            _allMaps = new Dictionary<int, MapData>();
            var files = Directory.GetFiles(_mapDataDir, "MAP*.json");
            foreach (var file in files)
            {
                var match = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(file), @"MAP(\d+)\.json");
                if (!match.Success) continue;
                int num = int.Parse(match.Groups[1].Value);
                var map = LoadMapFromFile(file, num);
                if (map != null) _allMaps[num] = map;
            }
            ModLogger.Log($"[MapLoader] Loaded {_allMaps.Count} maps for fingerprinting");
        }

        /// <summary>
        /// Detect which map matches the current battle by checking unit positions + ally height.
        /// Returns the map number, or -1 if no unique match.
        /// </summary>
        /// <param name="unitPositions">Grid positions of all units (grid = map coords)</param>
        /// <param name="allyX">Ally grid X</param>
        /// <param name="allyY">Ally grid Y</param>
        /// <param name="allyDisplayHeight">Display height at ally position (from memory)</param>
        public int DetectMap(List<(int x, int y)> unitPositions, int allyX, int allyY, double allyDisplayHeight)
        {
            EnsureAllMapsLoaded();
            if (_allMaps == null) return -1;

            var log = new List<string>();
            log.Add($"DetectMap: ally=({allyX},{allyY}) h={allyDisplayHeight} units={unitPositions.Count}");
            log.Add($"  positions: {string.Join(" ", unitPositions.Select(p => $"({p.x},{p.y})"))}");

            // Step 1: Filter maps where ALL unit positions are in-bounds and walkable (skip rejected)
            var candidates = new List<int>();
            foreach (var kv in _allMaps)
            {
                if (_rejectedMaps.Contains(kv.Key)) continue;
                var map = kv.Value;
                bool valid = true;
                foreach (var (x, y) in unitPositions)
                {
                    if (!map.InBounds(x, y) || !map.IsWalkable(x, y))
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid) candidates.Add(kv.Key);
            }

            log.Add($"Step 1 (bounds+walk): {candidates.Count} candidates: {string.Join(",", candidates.Select(c => $"MAP{c:D3}"))}");

            if (candidates.Count == 1) { WriteDetectLog(log, candidates[0], "unique position match"); return candidates[0]; }
            if (candidates.Count == 0) { WriteDetectLog(log, -1, "no maps fit"); return -1; }

            // Step 2: Score by dimension tightness only (height address unreliable)
            int maxX = 0, maxY = 0;
            foreach (var (x, y) in unitPositions) { if (x > maxX) maxX = x; if (y > maxY) maxY = y; }

            int bestMap = -1;
            int bestScore = int.MaxValue;
            foreach (var num in candidates)
            {
                var map = _allMaps[num];
                int dimFit = (map.Width - maxX - 1) + (map.Height - maxY - 1);
                if (dimFit < 0) continue;

                double h = map.GetDisplayHeight(allyX, allyY);
                log.Add($"  MAP{num:D3} {map.Width}x{map.Height} fit={dimFit} h={h}");
                if (dimFit < bestScore) { bestScore = dimFit; bestMap = num; }
            }

            // If multiple maps tie on fit, log them all
            var ties = candidates.Where(n => {
                var m = _allMaps[n];
                return (m.Width - maxX - 1) + (m.Height - maxY - 1) == bestScore;
            }).ToList();
            if (ties.Count > 1)
                log.Add($"  TIE ({ties.Count}): {string.Join(",", ties.Select(t => $"MAP{t:D3}"))}");

            WriteDetectLog(log, bestMap, $"tightest fit={bestScore} ({candidates.Count} candidates, {ties.Count} ties)");
            return bestMap;
        }

        private void WriteDetectLog(List<string> log, int result, string reason)
        {
            log.Add($"RESULT: MAP{result:D3} ({reason})");
            try
            {
                var path = Path.Combine(_mapDataDir, "..", "detect_log.txt");
                File.WriteAllLines(path, log);
            }
            catch { }
            ModLogger.Log($"[MapLoader] DetectMap result: MAP{result:D3} ({reason})");
        }

        private MapData? LoadMapFromFile(string path, int mapNumber)
        {
            try
            {
                var json = File.ReadAllText(path);
                var raw = JsonSerializer.Deserialize<RawMapJson>(json);
                if (raw?.Lower == null || raw.Lower.Length == 0) return null;

                var rows = raw.Lower.Length;
                var cols = raw.Lower[0].Length;
                var tiles = new MapTile[cols, rows];

                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        var t = raw.Lower[y][x];
                        tiles[x, y] = new MapTile
                        {
                            Height = t.Height,
                            SlopeHeight = t.SlopeHeight,
                            Depth = t.Depth,
                            NoWalk = t.NoWalk,
                            NoCursor = t.NoCursor,
                            SurfaceType = t.SurfaceType ?? "",
                            SlopeType = t.SlopeType ?? ""
                        };
                    }
                }

                return new MapData { MapNumber = mapNumber, Width = cols, Height = rows, Tiles = tiles };
            }
            catch { return null; }
        }

        /// <summary>
        /// Look up MAP number from world map location ID using location_maps.json.
        /// Returns -1 if location not in table.
        /// </summary>
        public int GetMapNumberForLocation(int locationId)
        {
            var lookupPath = Path.Combine(_mapDataDir, "..", "location_maps.json");
            if (!File.Exists(lookupPath))
            {
                // Try repo path
                lookupPath = Path.Combine(_mapDataDir, "location_maps.json");
            }
            if (!File.Exists(lookupPath)) return -1;

            try
            {
                var json = File.ReadAllText(lookupPath);
                var lookup = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (lookup != null && lookup.TryGetValue(locationId.ToString(), out var val) && val.ValueKind == JsonValueKind.Number)
                {
                    return val.GetInt32();
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[MapLoader] Failed to read location_maps.json: {ex.Message}");
            }
            return -1;
        }

        /// <summary>
        /// Try to auto-load the correct map for a world map location ID.
        /// Returns the loaded map, or null if location not in lookup table.
        /// </summary>
        public MapData? LoadMapForLocation(int locationId)
        {
            int mapNumber = GetMapNumberForLocation(locationId);
            if (mapNumber < 0)
            {
                ModLogger.Log($"[MapLoader] No map mapping for location {locationId}");
                return null;
            }
            return LoadMap(mapNumber);
        }

        /// <summary>
        /// Look up MAP number for random encounters at a world map location.
        /// Uses random_encounter_maps.json (separate from story battle maps).
        /// Returns -1 if not in table.
        /// </summary>
        public int GetRandomEncounterMap(int locationId)
        {
            var path = Path.Combine(_mapDataDir, "..", "random_encounter_maps.json");
            if (!File.Exists(path))
                path = Path.Combine(_mapDataDir, "random_encounter_maps.json");
            if (!File.Exists(path))
            {
                ModLogger.Log($"[MapLoader] random_encounter_maps.json not found (tried {Path.Combine(_mapDataDir, "..", "random_encounter_maps.json")})");
                return -1;
            }

            try
            {
                var json = File.ReadAllText(path);
                var lookup = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (lookup != null && lookup.TryGetValue(locationId.ToString(), out var val) && val.ValueKind == JsonValueKind.Number)
                {
                    ModLogger.Log($"[MapLoader] Random encounter map for location {locationId}: MAP{val.GetInt32():D3}");
                    return val.GetInt32();
                }
                ModLogger.Log($"[MapLoader] No random encounter entry for location {locationId}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[MapLoader] Failed to read random_encounter_maps.json: {ex.Message}");
            }
            return -1;
        }

        // --- JSON deserialization models ---

        private class RawMapJson
        {
            [JsonPropertyName("lower")]
            public RawTile[][]? Lower { get; set; }

            [JsonPropertyName("upper")]
            public RawTile[][]? Upper { get; set; }

            [JsonPropertyName("num")]
            public int Num { get; set; }
        }

        private class RawTile
        {
            [JsonPropertyName("x")]
            public int X { get; set; }

            [JsonPropertyName("y")]
            public int Y { get; set; }

            [JsonPropertyName("height")]
            public int Height { get; set; }

            [JsonPropertyName("slope_height")]
            public int SlopeHeight { get; set; }

            [JsonPropertyName("depth")]
            public int Depth { get; set; }

            [JsonPropertyName("no_walk")]
            public bool NoWalk { get; set; }

            [JsonPropertyName("no_cursor")]
            public bool NoCursor { get; set; }

            [JsonPropertyName("surface_type")]
            public string? SurfaceType { get; set; }

            [JsonPropertyName("slope_type")]
            public string? SlopeType { get; set; }
        }
    }

    public class MapData
    {
        public int MapNumber { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>Tiles indexed as [x, y]. Grid cursor coords map directly.</summary>
        public MapTile[,] Tiles { get; set; } = new MapTile[0, 0];

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        /// <summary>
        /// Get the effective display height at a tile.
        /// Formula: height + slope_height / 2.0
        /// </summary>
        public double GetDisplayHeight(int x, int y)
        {
            if (!InBounds(x, y)) return -1;
            var t = Tiles[x, y];
            return t.Height + t.SlopeHeight / 2.0;
        }

        /// <summary>
        /// Check if a tile is walkable (not blocked by terrain).
        /// </summary>
        public bool IsWalkable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            return !Tiles[x, y].NoWalk;
        }
    }

    public struct MapTile
    {
        public int Height;
        public int SlopeHeight;
        public int Depth;
        public bool NoWalk;
        public bool NoCursor;
        public string SurfaceType;
        public string SlopeType;

        /// <summary>
        /// Movement cost to enter this tile. FFT terrain costs:
        /// - Swamp/Marsh/Poisoned marsh: 2
        /// - Lava: 2
        /// - Water tiles (River/Lake/Sea/Waterway/Waterfall) with depth: 1 + depth
        /// - All others: 1
        /// </summary>
        public int MoveCost
        {
            get
            {
                switch (SurfaceType)
                {
                    case "Swamp":
                    case "Marsh":
                    case "Poisoned marsh":
                    case "Lava":
                        return 2;
                    case "River":
                    case "Lake":
                    case "Sea":
                    case "Waterway":
                    case "Waterfall":
                        return Depth > 0 ? 1 + Depth : 1;
                    default:
                        return 1;
                }
            }
        }
    }
}
