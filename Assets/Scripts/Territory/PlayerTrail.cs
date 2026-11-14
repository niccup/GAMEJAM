using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Draws one character's trail into the <see cref="TerritoryGrid"/> as its trail emitter moves. A player normally has
    /// one character; in later rounds replayed ghosts add more, each with its own <see cref="PlayerTrail"/> (own last
    /// position and path length) but the same <see cref="Player"/> index, so they all draw in the player's color.
    /// Feed it the emitter position every frame; it interpolates between positions so fast movement never leaves gaps.
    /// <para>
    /// There is only one kind of colored cell. Moving over a free cell or over <i>any other player's color</i> overwrites it
    /// with this player's color (that is how an opponent cuts through a captured area). Moving over own color changes
    /// nothing, but can close a loop - see <see cref="Advance"/>.
    /// </para>
    /// </summary>
    public sealed class PlayerTrail
    {
        readonly TerritoryGrid grid;
        readonly int player;
        readonly int source;
        readonly float minLoopLength;

        Vector2 lastPosition;
        bool hasLastPosition;
        float travelled;
        bool laidNewCells;

        /// <summary>The player this trail is drawn for (colors and ownership).</summary>
        public int Player => player;

        public PlayerTrail(TerritoryGrid grid, int player, int source, float minLoopLength)
        {
            this.grid = grid;
            this.player = player;
            this.source = source;
            this.minLoopLength = minLoopLength;
        }

        /// <summary>
        /// Extends the trail to <paramref name="position"/>. Returns true when a loop might have been closed: the character
        /// has laid new color and touches own color that is not just the trail directly behind it (laid less than the
        /// minimum loop length ago). Any other cell of the player's color counts - old trail, captured area, trail of
        /// another character of the same player - they are all the same.
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
            bool candidate = false;
            for (int i = 1; i <= steps; i++)
            {
                travelled += segment;
                candidate |= Stamp(Vector2.Lerp(lastPosition, position, i / (float)steps), radius);
            }

            lastPosition = position;
            return candidate;
        }

        /// <summary>Forgets all progress (position, travelled distance). The grid itself is not touched.</summary>
        public void Reset()
        {
            hasLastPosition = false;
            travelled = 0f;
            laidNewCells = false;
        }

        /// <summary>Forget the last position, e.g. after a teleport or pause, so no line is drawn across the jump.</summary>
        public void BreakPath() => hasLastPosition = false;

        bool Stamp(Vector2 center, float radius)
        {
            Vector2Int home = grid.WorldToCell(center);
            Vector2Int min = grid.WorldToCell(center - Vector2.one * radius);
            Vector2Int max = grid.WorldToCell(center + Vector2.one * radius);
            float radiusSqr = radius * radius;
            bool touchedOwnColor = false;

            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(grid.Height - 1, max.y); y++)
            for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(grid.Width - 1, max.x); x++)
            {
                // The cell under the emitter always counts, even if the circle is smaller than a cell.
                bool covered = (x == home.x && y == home.y) || (grid.CellCenter(x, y) - center).sqrMagnitude <= radiusSqr;
                if (!covered) continue;

                int index = grid.Index(x, y);
                if (grid.GetColorOwner(index) == player)
                {
                    // Already our color: nothing to draw. It can serve as one end of a loop, unless it is the trail
                    // this very character laid a moment ago (that would "close" on the bit directly behind it).
                    bool justLaidByUs = grid.GetStamper(index) == source && travelled - grid.GetStampDistance(index) <= minLoopLength;
                    if (!justLaidByUs) touchedOwnColor = true;
                }
                else
                {
                    // Free cell or ANY other player's color (trail or captured area alike): take it over.
                    grid.Stamp(index, player, source, travelled);
                    laidNewCells = true;
                }
            }

            // A loop can only appear when we added color, so there is nothing to look for while just moving inside own color.
            if (!touchedOwnColor || !laidNewCells) return false;
            laidNewCells = false;
            return true;
        }
    }
}
