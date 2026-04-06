using System;
using System.Collections.Generic;
using System.IO;
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

            ModLogger.Log($"[MapLoader] DetectMap: {candidates.Count} candidates after position filter");

            if (candidates.Count == 1) return candidates[0];
            if (candidates.Count == 0) return -1;

            // Step 2: Filter by ally's display height
            var heightMatches = new List<int>();
            foreach (var num in candidates)
            {
                var map = _allMaps[num];
                double h = map.GetDisplayHeight(allyX, allyY);
                if (Math.Abs(h - allyDisplayHeight) < 0.01)
                    heightMatches.Add(num);
            }

            ModLogger.Log($"[MapLoader] DetectMap: {heightMatches.Count} candidates after height filter (ally h={allyDisplayHeight})");

            if (heightMatches.Count == 1) return heightMatches[0];

            // Step 3: Tightest dimension fit (prefer map whose dimensions best match unit spread)
            var remaining = heightMatches.Count > 0 ? heightMatches : candidates;

            int maxX = 0, maxY = 0;
            foreach (var (x, y) in unitPositions) { if (x > maxX) maxX = x; if (y > maxY) maxY = y; }

            int bestMap = remaining[0];
            int bestFit = int.MaxValue;
            foreach (var num in remaining)
            {
                var map = _allMaps[num];
                int fit = (map.Width - maxX - 1) + (map.Height - maxY - 1);
                if (fit >= 0 && fit < bestFit) { bestFit = fit; bestMap = num; }
            }

            ModLogger.Log($"[MapLoader] DetectMap: {remaining.Count} candidates, tightest fit MAP{bestMap:D3} (fit={bestFit})");
            return bestMap;
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
        /// Movement cost to enter this tile. Swamp/Marsh = 2, most others = 1.
        /// </summary>
        public int MoveCost
        {
            get
            {
                switch (SurfaceType)
                {
                    case "Swamp":
                    case "Marsh":
                        return 2;
                    default:
                        return 1;
                }
            }
        }
    }
}
