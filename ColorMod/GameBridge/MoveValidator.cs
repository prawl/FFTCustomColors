using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Validates battle_move targets against the last computed valid tile set.
    /// Prevents Claude from attempting moves to unreachable tiles.
    /// </summary>
    public static class MoveValidator
    {
        /// <summary>
        /// Returns true if the target tile is in the valid set, or if no valid tiles are cached (allow by default).
        /// </summary>
        public static bool IsValidTile(int x, int y, HashSet<(int, int)>? validTiles)
        {
            if (validTiles == null) return true; // no data — allow and let game decide
            return validTiles.Contains((x, y));
        }

        /// <summary>
        /// Returns a descriptive error message for an invalid tile move attempt.
        /// </summary>
        public static string GetInvalidTileError(int x, int y, HashSet<(int, int)>? validTiles)
        {
            int count = validTiles?.Count ?? 0;
            return $"Tile ({x},{y}) is not in the valid move range. Run scan_move first and pick from ValidMoveTiles ({count} valid tiles available).";
        }
    }
}
