using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Static layout of the JobSelection grid. Row widths are 6/7/6 cells
    /// (verified live 2026-04-15 on Ramza Ch4 + Agrias — see
    /// <c>project_job_grid_cursor.md</c>). The heap cursor byte is a FLAT
    /// LINEAR INDEX (0..N-1) row-major across the grid, NOT
    /// <c>row*6+col</c>.
    ///
    /// Per-character variations (confirmed live 2026-04-15):
    /// - <b>Story characters</b> (Ramza, Agrias, Mustadio, Orlandeau…) get
    ///   their UNIQUE class at (0,0) — Gallant Knight, Holy Knight,
    ///   Machinist, Thunder God respectively. All other cells are the
    ///   standard generic grid.
    /// - <b>Generic units</b> get "Squire" at (0,0).
    /// - <b>Gender</b> controls cell (2,4): Bard for males, Dancer for
    ///   females. Encoded in the job ID parity (odd=male, even=female
    ///   for generic IDs; story characters carry their own gender).
    ///
    /// Call <see cref="ForUnit"/> to build a layout tailored to a specific
    /// roster slot; the legacy <see cref="CharacterKind"/> enum overloads
    /// remain for tests + back-compat.
    /// </summary>
    public static class JobGridLayout
    {
        public enum CharacterKind
        {
            GenericMale,
            GenericFemale,
            Ramza,
        }

        // Standard per-row template used by everyone. Position (0,0) and
        // (2,4) are placeholders that ForUnit() patches per character.
        private const string PlaceholderZero = "__ZERO__";
        private const string PlaceholderBard = "__BARDLIKE__";
        private static readonly string?[][] StandardGrid =
        {
            new string?[] { PlaceholderZero, "Chemist", "Knight", "Archer", "Monk", "White Mage" },
            new string?[] { "Black Mage", "Time Mage", "Summoner", "Thief", "Orator", "Mystic", "Geomancer" },
            new string?[] { "Dragoon", "Samurai", "Ninja", "Arithmetician", PlaceholderBard, "Mime" },
        };

        /// <summary>
        /// Story-character display name → unique class at grid (0,0).
        /// Verified 2026-04-15: Ramza, Agrias. Others inferred from
        /// roster job-name data (<see cref="CharacterData.JobNameById"/>)
        /// — generally the story unit's starting job is their unique
        /// class, which appears at (0,0) in their JobSelection grid.
        ///
        /// Names use the WotL localizations surfaced by
        /// <c>NameTableLookup</c> — match exactly.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> StoryCharacterUniqueClass =
            new Dictionary<string, string>
            {
                ["Ramza"] = "Gallant Knight",
                ["Agrias"] = "Holy Knight",
                ["Mustadio"] = "Machinist",
                ["Rapha"] = "Skyseer",
                ["Marach"] = "Netherseer",
                ["Beowulf"] = "Templar",
                ["Construct 8"] = "Steel Giant",
                ["Orlandeau"] = "Thunder God",
                ["Meliadoul"] = "Divine Knight",
                ["Reis"] = "Dragonkin",
                ["Cloud"] = "Soldier",
                ["Luso"] = "Game Hunter",
                ["Balthier"] = "Sky Pirate",
                // Delita / Ovelia / Alma / Wiegraf etc — only appear as
                // NPCs or bosses, not in the party grid. Add when they
                // become player-controllable.
            };

        /// <summary>
        /// Per-character grid snapshot. Use <see cref="ForUnit"/> to build.
        /// Exposes the same IndexToRowCol / RowColToIndex / GetClassAt
        /// semantics as the legacy CharacterKind overloads.
        /// </summary>
        public readonly struct Layout
        {
            private readonly string?[][] _grid;
            internal Layout(string?[][] grid) { _grid = grid; }

            public int RowCount => _grid.Length;

            public int GetRowWidth(int row)
            {
                if (row < 0 || row >= _grid.Length) return 0;
                return _grid[row].Length;
            }

            public string? GetClassAt(int row, int col)
            {
                if (row < 0 || row >= _grid.Length) return null;
                var rowCells = _grid[row];
                if (col < 0 || col >= rowCells.Length) return null;
                return rowCells[col];
            }

            public (int Row, int Col)? IndexToRowCol(int index)
            {
                if (index < 0) return null;
                int remaining = index;
                for (int r = 0; r < _grid.Length; r++)
                {
                    int width = _grid[r].Length;
                    if (remaining < width) return (r, remaining);
                    remaining -= width;
                }
                return null;
            }

            public int RowColToIndex(int row, int col)
            {
                if (row < 0 || row >= _grid.Length) return -1;
                if (col < 0 || col >= _grid[row].Length) return -1;
                int idx = 0;
                for (int r = 0; r < row; r++) idx += _grid[r].Length;
                return idx + col;
            }

            public int TotalCells
            {
                get
                {
                    int total = 0;
                    for (int r = 0; r < _grid.Length; r++) total += _grid[r].Length;
                    return total;
                }
            }

            public IEnumerable<(int Row, int Col, string ClassName)> EnumerateCells()
            {
                for (int r = 0; r < _grid.Length; r++)
                {
                    for (int c = 0; c < _grid[r].Length; c++)
                    {
                        var name = _grid[r][c];
                        if (name != null)
                            yield return (r, c, name);
                    }
                }
            }
        }

        /// <summary>
        /// Build the JobSelection grid for a specific unit. <paramref
        /// name="unitName"/> is the name surfaced by the roster reader
        /// (null or unknown → generic Squire). <paramref name="isFemale"/>
        /// controls the Bard/Dancer cell at (2,4) — derive from the
        /// generic job ID's parity (odd=male, even=female) for generic
        /// units, or hard-code for known story characters.
        /// </summary>
        public static Layout ForUnit(string? unitName, bool isFemale)
        {
            string zeroClass = "Squire";
            if (unitName != null && StoryCharacterUniqueClass.TryGetValue(unitName, out var story))
            {
                zeroClass = story;
            }
            string bardLike = isFemale ? "Dancer" : "Bard";

            var grid = new string?[StandardGrid.Length][];
            for (int r = 0; r < StandardGrid.Length; r++)
            {
                var src = StandardGrid[r];
                var dst = new string?[src.Length];
                for (int c = 0; c < src.Length; c++)
                {
                    dst[c] = src[c] switch
                    {
                        PlaceholderZero => zeroClass,
                        PlaceholderBard => bardLike,
                        _ => src[c],
                    };
                }
                grid[r] = dst;
            }
            return new Layout(grid);
        }

        /// <summary>
        /// Infer gender from a generic job ID. Generic human jobs use
        /// odd=male, even=female pairs. The pairs live in two ranges:
        /// 0x01-0x24 (Chemist → Mime) and 0x4A-0x4B (Squire), plus the
        /// WotL-exclusive 0xA4-0xA7 (Dark Knight, Onion Knight). Returns
        /// false for any ID outside those pairs (story-character jobs
        /// handle gender via the unique-class lookup).
        /// </summary>
        public static bool IsGenericFemale(int jobId)
        {
            bool inPair =
                (jobId >= 0x01 && jobId <= 0x24) ||
                (jobId >= 0x4A && jobId <= 0x4B) ||
                (jobId >= 0xA4 && jobId <= 0xA7);
            if (!inPair) return false;
            return (jobId & 1) == 0;
        }

        // ================================================================
        // Legacy CharacterKind API — kept for existing tests and
        // back-compat. New callers should use ForUnit instead.
        // ================================================================

        private static Layout LayoutForKind(CharacterKind kind) => kind switch
        {
            CharacterKind.Ramza => ForUnit("Ramza", isFemale: false),
            CharacterKind.GenericFemale => ForUnit(null, isFemale: true),
            _ => ForUnit(null, isFemale: false),
        };

        public static int GetRowCount(CharacterKind kind) => LayoutForKind(kind).RowCount;
        public static int GetRowWidth(CharacterKind kind, int row) => LayoutForKind(kind).GetRowWidth(row);
        public static string? GetClassAt(CharacterKind kind, int row, int col) => LayoutForKind(kind).GetClassAt(row, col);
        public static (int Row, int Col)? IndexToRowCol(CharacterKind kind, int index) => LayoutForKind(kind).IndexToRowCol(index);
        public static int RowColToIndex(CharacterKind kind, int row, int col) => LayoutForKind(kind).RowColToIndex(row, col);
        public static int TotalCells(CharacterKind kind) => LayoutForKind(kind).TotalCells;
        public static IEnumerable<(int Row, int Col, string ClassName)> EnumerateCells(CharacterKind kind) => LayoutForKind(kind).EnumerateCells();
    }
}
