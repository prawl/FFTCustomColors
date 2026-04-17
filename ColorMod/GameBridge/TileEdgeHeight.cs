namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Cardinal direction a unit is moving toward when stepping from one
    /// tile to an adjacent one. Used to pick which edge of each tile
    /// matters for the height-delta check.
    /// </summary>
    public enum Direction { N, S, E, W }

    /// <summary>
    /// Canonical FFT tile edge-height lookup. When computing the height
    /// delta between two adjacent tiles, we don't want their flat display
    /// heights — we want the heights at the specific edge where the step
    /// happens. Slopes raise one edge by SlopeHeight while leaving the
    /// other at Height; convex/concave corners raise two adjacent edges.
    ///
    /// Convention: "N" = -Y edge (top of map), "S" = +Y edge (bottom),
    /// "E" = +X edge (right), "W" = -X edge (left).
    ///
    /// Ported from community canonical sources (FFHacktics / GameFAQs).
    /// </summary>
    public static class TileEdgeHeight
    {
        public static double Edge(MapTile t, Direction dir)
        {
            int h = t.Height;
            int sh = t.SlopeHeight;
            var st = t.SlopeType ?? "";

            // Flat tiles are trivial — all four edges = Height. The slope_height
            // field may be nonzero on a "Flat" tile in some map data (rare), but
            // canonical rule is: flat = all edges at Height.
            if (st.StartsWith("Flat"))
                return h;

            if (st.StartsWith("Incline "))
            {
                // "Incline N" rises toward the north — N edge is high, S edge low.
                // E/W edges span the slope; return the midpoint so an orthogonal
                // step through the incline uses the average.
                var side = st.Substring("Incline ".Length);
                switch (side)
                {
                    case "N": return dir == Direction.N ? h + sh
                                 : dir == Direction.S ? h
                                 : h + sh / 2.0;
                    case "S": return dir == Direction.S ? h + sh
                                 : dir == Direction.N ? h
                                 : h + sh / 2.0;
                    case "E": return dir == Direction.E ? h + sh
                                 : dir == Direction.W ? h
                                 : h + sh / 2.0;
                    case "W": return dir == Direction.W ? h + sh
                                 : dir == Direction.E ? h
                                 : h + sh / 2.0;
                }
            }

            if (st.StartsWith("Convex "))
            {
                // Convex NE means the NE CORNER is raised — the N edge and E edge
                // share that corner so both are high. S and W edges are low.
                var corner = st.Substring("Convex ".Length);
                bool highN = corner.Contains("N");
                bool highS = corner.Contains("S");
                bool highE = corner.Contains("E");
                bool highW = corner.Contains("W");
                switch (dir)
                {
                    case Direction.N: return highN ? h + sh : h;
                    case Direction.S: return highS ? h + sh : h;
                    case Direction.E: return highE ? h + sh : h;
                    case Direction.W: return highW ? h + sh : h;
                }
            }

            if (st.StartsWith("Concave "))
            {
                // Concave NE means the NE corner is DEPRESSED — N and E edges
                // are low, S and W edges are high. Opposite of Convex.
                var corner = st.Substring("Concave ".Length);
                bool lowN = corner.Contains("N");
                bool lowS = corner.Contains("S");
                bool lowE = corner.Contains("E");
                bool lowW = corner.Contains("W");
                switch (dir)
                {
                    case Direction.N: return lowN ? h : h + sh;
                    case Direction.S: return lowS ? h : h + sh;
                    case Direction.E: return lowE ? h : h + sh;
                    case Direction.W: return lowW ? h : h + sh;
                }
            }

            // Fallback for unknown slope labels — use the legacy display-height
            // approximation so we don't regress vs the current BFS.
            return h + sh / 2.0;
        }
    }
}
