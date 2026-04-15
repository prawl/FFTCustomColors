using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Static layout of the JobSelection grid per character type. Row widths
    /// VARY: for Ramza Ch4 the grid is 6/7/6 cells per row (confirmed
    /// live 2026-04-15 via cursor wrap tests — see verify notes in
    /// <c>project_job_grid_cursor.md</c>). The heap cursor byte is a FLAT
    /// LINEAR INDEX (0..N-1) across the enumerated order, NOT <c>row*6+col</c>
    /// — that earlier hypothesis only held within row 0 because its width is
    /// 6. Row 1's 7th cell (Geomancer for Ramza) makes the uniform grid
    /// formula incorrect.
    ///
    /// Cell names match the class labels rendered in-game — used directly
    /// as <c>ui=&lt;hovered job&gt;</c>.
    /// </summary>
    public static class JobGridLayout
    {
        public enum CharacterKind
        {
            GenericMale,
            GenericFemale,
            Ramza,
        }

        // Ramza Ch4 grid, 6/7/6. Verified live 2026-04-15:
        // - 6 Rights from Gallant Knight wraps back to Gallant Knight.
        // - 6 Rights from Black Mage lands on Geomancer (7th cell).
        // - 7 Rights from Black Mage wraps back to Black Mage.
        // - Down from Black Mage → Dragoon. 6 Rights on row 2 wraps.
        // Cell names read directly from in-game labels.
        private static readonly string?[][] RamzaGrid =
        {
            new string?[] { "Gallant Knight", "Chemist", "Knight", "Archer", "Monk", "White Mage" },
            new string?[] { "Black Mage", "Time Mage", "Summoner", "Thief", "Orator", "Mystic", "Geomancer" },
            new string?[] { "Dragoon", "Samurai", "Ninja", "Arithmetician", "Bard", "Mime" },
        };

        // Generic male grid — inferred from Ramza's pattern, with Squire
        // replacing Gallant Knight at (0,0) and Bard (male only) at row 2.
        // NOT YET LIVE-VERIFIED — expect tweaks once confirmed on a generic
        // unit.
        private static readonly string?[][] GenericMaleGrid =
        {
            new string?[] { "Squire", "Chemist", "Knight", "Archer", "Monk", "White Mage" },
            new string?[] { "Black Mage", "Time Mage", "Summoner", "Thief", "Orator", "Mystic", "Geomancer" },
            new string?[] { "Dragoon", "Samurai", "Ninja", "Arithmetician", "Bard", "Mime" },
        };

        // Generic female grid — Dancer replaces Bard at the same cell.
        // NOT YET LIVE-VERIFIED.
        private static readonly string?[][] GenericFemaleGrid =
        {
            new string?[] { "Squire", "Chemist", "Knight", "Archer", "Monk", "White Mage" },
            new string?[] { "Black Mage", "Time Mage", "Summoner", "Thief", "Orator", "Mystic", "Geomancer" },
            new string?[] { "Dragoon", "Samurai", "Ninja", "Arithmetician", "Dancer", "Mime" },
        };

        private static string?[][] GetGrid(CharacterKind kind) => kind switch
        {
            CharacterKind.Ramza => RamzaGrid,
            CharacterKind.GenericFemale => GenericFemaleGrid,
            _ => GenericMaleGrid,
        };

        /// <summary>
        /// Number of rows in the grid for this character kind.
        /// </summary>
        public static int GetRowCount(CharacterKind kind) => GetGrid(kind).Length;

        /// <summary>
        /// Number of columns in the given row (row widths vary — row 1 is 7
        /// for Ramza, rows 0/2 are 6).
        /// </summary>
        public static int GetRowWidth(CharacterKind kind, int row)
        {
            var grid = GetGrid(kind);
            if (row < 0 || row >= grid.Length) return 0;
            return grid[row].Length;
        }

        /// <summary>
        /// Returns the class name at (row, col), or null if out of range.
        /// </summary>
        public static string? GetClassAt(CharacterKind kind, int row, int col)
        {
            var grid = GetGrid(kind);
            if (row < 0 || row >= grid.Length) return null;
            var rowCells = grid[row];
            if (col < 0 || col >= rowCells.Length) return null;
            return rowCells[col];
        }

        /// <summary>
        /// Converts a flat linear cursor index (the heap cursor byte's value)
        /// to a (row, col) pair using the character's row widths. Returns
        /// null if the index is out of range.
        /// </summary>
        public static (int Row, int Col)? IndexToRowCol(CharacterKind kind, int index)
        {
            if (index < 0) return null;
            var grid = GetGrid(kind);
            int remaining = index;
            for (int r = 0; r < grid.Length; r++)
            {
                int width = grid[r].Length;
                if (remaining < width) return (r, remaining);
                remaining -= width;
            }
            return null;
        }

        /// <summary>
        /// Inverse of <see cref="IndexToRowCol"/> — converts (row, col) to
        /// the flat linear index. Returns -1 if (row, col) is out of range.
        /// </summary>
        public static int RowColToIndex(CharacterKind kind, int row, int col)
        {
            var grid = GetGrid(kind);
            if (row < 0 || row >= grid.Length) return -1;
            if (col < 0 || col >= grid[row].Length) return -1;
            int idx = 0;
            for (int r = 0; r < row; r++) idx += grid[r].Length;
            return idx + col;
        }

        /// <summary>
        /// Flat enumeration of all populated cells in grid order.
        /// </summary>
        public static IEnumerable<(int Row, int Col, string ClassName)> EnumerateCells(CharacterKind kind)
        {
            var grid = GetGrid(kind);
            for (int r = 0; r < grid.Length; r++)
            {
                for (int c = 0; c < grid[r].Length; c++)
                {
                    var name = grid[r][c];
                    if (name != null)
                        yield return (r, c, name);
                }
            }
        }

        /// <summary>
        /// Total count of populated cells in the grid.
        /// </summary>
        public static int TotalCells(CharacterKind kind)
        {
            int total = 0;
            var grid = GetGrid(kind);
            for (int r = 0; r < grid.Length; r++) total += grid[r].Length;
            return total;
        }
    }
}
