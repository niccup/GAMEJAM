using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Lays one player's trail into the <see cref="TerritoryGrid"/> as the player's trail emitter moves.
    /// Feed it the emitter position every frame; it interpolates between positions so fast movement never leaves gaps.
    /// <para>
    /// Crossing rule: a cell holds one trail owner. When another player's trail passes over a cell it takes the cell over,
    /// which punches a hole in the previous owner's trail - exactly what "interrupting" a loop means.
    /// </para>
    /// </summary>
    public sealed class PlayerTrail
    {
        readonly TerritoryGrid grid;
        readonly int player;
        readonly float minLoopLength;

        Vector2 lastPosition;
        bool hasLastPosition;
        float travelled;
        float lastTerritoryContact = float.NegativeInfinity;

        public PlayerTrail(TerritoryGrid grid, int player, float minLoopLength)
        {
            this.grid = grid;
            this.player = player;
            this.minLoopLength = minLoopLength;
        }

        /// <summary>
        /// Extends the trail to <paramref name="position"/>. Returns true when the emitter now touches the player's
        /// own color in a way that could close a loop: either a part of its own trail laid long enough ago (not just the
        /// trail directly behind it), or its own territory after an excursion of at least the minimum loop length.
        /// That is only a <i>candidate</i>: whether a loop is really closed is decided by <see cref="LoopDetector"/>.
        /// </summary>
        public bool Advance(Vector2 position, float radius)
        {
            if (!hasLastPosition)
            {
                hasLastPosition = true;
                lastPosition = position;
                return Stamp(position, radius);
            }

            float distance = Vector2.Distance(lastPosition, position);
            if (distance < 1e-5f) return false;

            // Sub-sample the move so consecutive stamps are never more than half a cell apart.
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (grid.CellSize * 0.5f)));
            float segment = distance / steps;
            bool touchedOwnTrail = false;
            for (int i = 1; i <= steps; i++)
            {
                travelled += segment;
                touchedOwnTrail |= Stamp(Vector2.Lerp(lastPosition, position, i / (float)steps), radius);
            }

            lastPosition = position;
            return touchedOwnTrail;
        }

        /// <summary>Forget the last position, e.g. after a teleport or pause, so no line is drawn across the jump.</summary>
        public void BreakPath() => hasLastPosition = false;

        bool Stamp(Vector2 center, float radius)
        {
            Vector2Int home = grid.WorldToCell(center);
            Vector2Int min = grid.WorldToCell(center - Vector2.one * radius);
            Vector2Int max = grid.WorldToCell(center + Vector2.one * radius);
            float radiusSqr = radius * radius;
            bool touchedOldTrail = false;
            bool touchedTerritory = false;

            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(grid.Height - 1, max.y); y++)
            for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(grid.Width - 1, max.x); x++)
            {
                // The cell under the emitter always counts, even if the circle is smaller than a cell.
                bool covered = (x == home.x && y == home.y) || (grid.CellCenter(x, y) - center).sqrMagnitude <= radiusSqr;
                if (!covered) continue;

                int index = grid.Index(x, y);
                int trail = grid.GetTrailOwner(index);
                if (trail == player)
                {
                    // Own trail is never re-stamped (it would look "recent"); it just may close a loop.
                    if (travelled - grid.GetTrailDistance(index) > minLoopLength) touchedOldTrail = true;
                }
                else if (trail == TerritoryGrid.None && grid.GetOwner(index) == player)
                {
                    // Already our color: nothing to draw, but it can serve as one end of a loop.
                    touchedTerritory = true;
                }
                else
                {
                    // Free, foreign territory or another player's trail (which this interrupts): lay our trail.
                    grid.SetTrail(index, player, travelled);
                }
            }

            bool returnedToTerritory = false;
            if (touchedTerritory)
            {
                returnedToTerritory = travelled - lastTerritoryContact > minLoopLength;
                lastTerritoryContact = travelled;
            }

            return touchedOldTrail || returnedToTerritory;
        }
    }
}
