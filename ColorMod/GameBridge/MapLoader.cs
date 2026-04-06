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
        private readonly string _mapDataDir;
        private MapData? _currentMap;
        private int _currentMapNumber = -1;

        public MapData? CurrentMap => _currentMap;
        public int CurrentMapNumber => _currentMapNumber;

        public MapLoader(string mapDataDir)
        {
            _mapDataDir = mapDataDir;
            // If the bridge maps dir is empty, try the repo's FFTHandsFree/maps/ directory
            if (!Directory.Exists(_mapDataDir) || Directory.GetFiles(_mapDataDir, "MAP*.json").Length == 0)
            {
                // Walk up from mapDataDir to find the repo root (has FFTHandsFree/)
                var dir = new DirectoryInfo(mapDataDir);
                while (dir?.Parent != null)
                {
                    dir = dir.Parent;
                    var repoMaps = Path.Combine(dir.FullName, "FFTHandsFree", "maps");
                    if (Directory.Exists(repoMaps) && Directory.GetFiles(repoMaps, "MAP*.json").Length > 0)
                    {
                        _mapDataDir = repoMaps;
                        ModLogger.Log($"[MapLoader] Using repo maps dir: {repoMaps}");
                        break;
                    }
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

            try
            {
                var json = File.ReadAllText(path);
                var raw = JsonSerializer.Deserialize<RawMapJson>(json);
                if (raw?.Lower == null || raw.Lower.Length == 0)
                {
                    ModLogger.LogError($"[MapLoader] MAP{mapNumber:D3} has no lower level data");
                    return null;
                }

                var rows = raw.Lower.Length;
                var cols = raw.Lower[0].Length;
                var tiles = new MapTile[cols, rows]; // tiles[x, y]

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

                _currentMap = new MapData
                {
                    MapNumber = mapNumber,
                    Width = cols,
                    Height = rows,
                    Tiles = tiles
                };
                _currentMapNumber = mapNumber;

                ModLogger.Log($"[MapLoader] Loaded MAP{mapNumber:D3}: {cols}x{rows}");
                return _currentMap;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[MapLoader] Failed to load MAP{mapNumber:D3}: {ex.Message}");
                return null;
            }
        }

        public void ClearMap()
        {
            _currentMap = null;
            _currentMapNumber = -1;
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
