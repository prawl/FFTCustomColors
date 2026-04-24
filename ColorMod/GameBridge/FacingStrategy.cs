using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    public static class FacingStrategy
    {
        public class UnitPosition
        {
            public int GridX, GridY;
            public int Team;
            public int Hp, MaxHp;
        }

        public class FacingResult
        {
            public int Dx, Dy;
            public int Front, Side, Back;
        }

        // Tiebreak: on equal scores, first facing in this array wins (East).
        private static readonly (int dx, int dy)[] CardinalFacings =
        {
            ( 1,  0),  // East
            (-1,  0),  // West
            ( 0,  1),  // North
            ( 0, -1),  // South
        };

        /// <summary>
        /// Facing screen arrow-to-direction table. Confirmed empirically in-game (2026-04-07).
        /// DIFFERENT from the movement cursor table (ArrowGridDelta in NavigationActions).
        /// Index: [rotation % 4, direction] where 0=Right, 1=Left, 2=Up, 3=Down.
        /// </summary>
        private static readonly (int dx, int dy)[,] FacingArrowDelta = {
            // rot=0: Right=E(+1,0)  Left=W(-1,0)  Up=N(0,+1)  Down=S(0,-1)
            { (1,0), (-1,0), (0,1), (0,-1) },
            // rot=1: Right=W(-1,0)  Left=E(+1,0)  Up=S(0,-1)  Down=N(0,+1)
            { (-1,0), (1,0), (0,-1), (0,1) },
            // rot=2: Right=S(0,-1)  Left=N(0,+1)  Up=E(+1,0)  Down=W(-1,0)
            { (0,-1), (0,1), (1,0), (-1,0) },
            // rot=3: Right=N(0,+1)  Left=S(0,-1)  Up=W(-1,0)  Down=E(+1,0)
            { (0,1), (0,-1), (-1,0), (1,0) },
        };

        private static readonly string[] ArrowNames = { "Right", "Left", "Up", "Down" };

        /// <summary>
        /// Maps a grid-space facing delta to the arrow key needed on the facing screen,
        /// given the current camera rotation. Returns "Right", "Left", "Up", "Down", or null.
        /// </summary>
        public static string? GetFacingArrowKey(int rotation, int faceDx, int faceDy)
        {
            int rot = ((rotation % 4) + 4) % 4;
            for (int d = 0; d < 4; d++)
            {
                var (dx, dy) = FacingArrowDelta[rot, d];
                if (dx == faceDx && dy == faceDy)
                    return ArrowNames[d];
            }
            return null;
        }

        /// <summary>
        /// Given that a specific arrow key (0=Right,1=Left,2=Up,3=Down) maps to a known
        /// cardinal direction, determine which rotation we're at. Returns 0-3, or -1 if
        /// no rotation matches.
        /// </summary>
        public static int DeriveRotation(int keyIndex, int dirDx, int dirDy)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                var (dx, dy) = FacingArrowDelta[rot, keyIndex];
                if (dx == dirDx && dy == dirDy)
                    return rot;
            }
            return -1;
        }

        /// <summary>
        /// Derive which direction a unit is facing based on where they moved from/to.
        /// Returns "North", "South", "East", "West", or null if no movement.
        /// For diagonal moves, the dominant axis determines the facing.
        /// </summary>
        public static string? DeriveFacingFromMovement(int prevX, int prevY, int curX, int curY)
        {
            int dx = curX - prevX;
            int dy = curY - prevY;
            if (dx == 0 && dy == 0) return null;

            if (Math.Abs(dx) > Math.Abs(dy))
                return dx > 0 ? "East" : "West";
            else
                return dy > 0 ? "South" : "North";  // +y = south in FFT grid
        }

        /// <summary>
        /// Scores each cardinal facing by weighted enemy threat and returns the one
        /// that minimizes back/side exposure. Falls back to East when no enemies exist
        /// (no meaningful facing preference without threats).
        /// </summary>
        public static (int dx, int dy) ComputeOptimalFacing(
            UnitPosition activeUnit,
            List<UnitPosition> enemies)
        {
            var result = ComputeOptimalFacingDetailed(activeUnit, enemies);
            return (result.Dx, result.Dy);
        }

        /// <summary>
        /// Same as ComputeOptimalFacing but returns arc counts for the chosen facing,
        /// so callers can include reasoning (e.g. "2 front, 1 side, 0 back").
        /// </summary>
        public static FacingResult ComputeOptimalFacingDetailed(
            UnitPosition activeUnit,
            List<UnitPosition> enemies)
        {
            if (enemies.Count == 0)
                return new FacingResult { Dx = 1, Dy = 0 }; // Default East — no threats to optimize against

            var bestFacing = CardinalFacings[0];
            float bestScore = float.MaxValue;
            int bestFront = 0, bestSide = 0, bestBack = 0;

            foreach (var facing in CardinalFacings)
            {
                float score = 0f;
                int front = 0, side = 0, back = 0;

                foreach (var enemy in enemies)
                {
                    int relX = enemy.GridX - activeUnit.GridX;
                    int relY = enemy.GridY - activeUnit.GridY;

                    // Dot product: positive = enemy in front, negative = behind
                    float dot = relX * facing.dx + relY * facing.dy;
                    float cross = MathF.Abs(relX * facing.dy - relY * facing.dx);
                    float mag = MathF.Abs(dot);

                    // Arc classification: front/back when |dot| >= |cross|, side otherwise.
                    // Exact diagonal (dot == cross) falls to side arc — consistent tiebreak.
                    float arcWeight;
                    if (cross <= mag && dot > 0)
                    {
                        arcWeight = 1f;   // Front arc
                        front++;
                    }
                    else if (cross <= mag && dot < 0)
                    {
                        arcWeight = 3f;   // Back arc
                        back++;
                    }
                    else
                    {
                        arcWeight = 2f;   // Side arc
                        side++;
                    }

                    // Sqrt decay: enemy 4 tiles away still has 50% weight (vs 25% with 1/d).
                    // Keeps distant threats relevant while still prioritizing adjacent ones.
                    int distance = Math.Abs(relX) + Math.Abs(relY);
                    float distWeight = 1f / MathF.Sqrt(Math.Max(distance, 1));

                    float hpWeight = enemy.MaxHp > 0
                        ? (float)enemy.Hp / enemy.MaxHp
                        : 1f;

                    score += arcWeight * distWeight * hpWeight;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestFacing = facing;
                    bestFront = front;
                    bestSide = side;
                    bestBack = back;
                }
            }

            return new FacingResult
            {
                Dx = bestFacing.dx,
                Dy = bestFacing.dy,
                Front = bestFront,
                Side = bestSide,
                Back = bestBack
            };
        }
    }
}
