using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Pure data model of the arena: a uniform grid of square cells. Every cell stores two independent layers:
    /// <list type="bullet">
    /// <item><b>owner</b> - who captured the cell (territory).</item>
    /// <item><b>trail</b> - whose trail currently covers the cell, plus how far along that player's path it was laid.</item>
    /// </list>
    /// Knows nothing about rendering, physics or input. Cells are addressed by a flat index (<c>y * Width + x</c>);
    /// cell (0, 0) is the bottom-left of the map. <see cref="Version"/> changes whenever any cell changes.
    /// </summary>
    public sealed class TerritoryGrid
    {
        public const int None = -1;

        readonly int[] owner;
        readonly int[] trailOwner;
        readonly float[] trailDistance;
        readonly int[] ownedCount;

        public int Width { get; }
        public int Height { get; }
        public int CellCount => Width * Height;
        public float CellSize { get; }

        /// <summary>World position of the bottom-left corner of cell (0, 0).</summary>
        public Vector2 Origin { get; }
        public int PlayerCount { get; }

        /// <summary>Incremented on every write. Views compare it to know when to refresh.</summary>
        public int Version { get; private set; }

        public TerritoryGrid(Vector2 origin, Vector2 size, float cellSize, int playerCount)
        {
            CellSize = Mathf.Max(0.001f, cellSize);
            Origin = origin;
            Width = Mathf.Max(1, Mathf.CeilToInt(size.x / CellSize));
            Height = Mathf.Max(1, Mathf.CeilToInt(size.y / CellSize));
            PlayerCount = playerCount;

            owner = new int[CellCount];
            trailOwner = new int[CellCount];
            trailDistance = new float[CellCount];
            ownedCount = new int[playerCount];
            for (int i = 0; i < owner.Length; i++) owner[i] = trailOwner[i] = None;
        }

        // ---- coordinates -------------------------------------------------------------------------------------

        public int Index(int x, int y) => y * Width + x;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Cell containing a world position. May lie outside the grid - check <see cref="InBounds"/>.</summary>
        public Vector2Int WorldToCell(Vector2 world) => new Vector2Int(
            Mathf.FloorToInt((world.x - Origin.x) / CellSize),
            Mathf.FloorToInt((world.y - Origin.y) / CellSize));

        public Vector2 CellCenter(int x, int y) =>
            new Vector2(Origin.x + (x + 0.5f) * CellSize, Origin.y + (y + 0.5f) * CellSize);

        // ---- territory layer ---------------------------------------------------------------------------------

        public int GetOwner(int index) => owner[index];

        public void SetOwner(int index, int player)
        {
            int previous = owner[index];
            if (previous == player) return;
            if (previous != None) ownedCount[previous]--;
            if (player != None) ownedCount[player]++;
            owner[index] = player;
            Version++;
        }

        /// <summary>Number of captured cells of a player (handy for scoring).</summary>
        public int GetOwnedCount(int player) => ownedCount[player];

        // ---- trail layer -------------------------------------------------------------------------------------

        public int GetTrailOwner(int index) => trailOwner[index];

        /// <summary>
        /// Whose color the cell shows: the trail owner if a trail covers it, otherwise the territory owner.
        /// Trails and territory share one color per player, so this is the cell's single "colored by" answer
        /// for both rendering and loop detection.
        /// </summary>
        public int GetColorOwner(int index) => trailOwner[index] != None ? trailOwner[index] : owner[index];

        /// <summary>Path length (world units) the owning player had travelled when this trail cell was laid.</summary>
        public float GetTrailDistance(int index) => trailDistance[index];

        public void SetTrail(int index, int player, float pathDistance)
        {
            trailOwner[index] = player;
            trailDistance[index] = pathDistance;
            Version++;
        }

        /// <summary>
        /// Turns every trail cell of a player into territory of the same player. Nothing disappears: the cells keep
        /// their color, they just stop being "trail" (so other players' trails no longer interrupt them).
        /// Returns how many cells were converted.
        /// </summary>
        public int CommitTrails(int player)
        {
            int converted = 0;
            for (int i = 0; i < trailOwner.Length; i++)
            {
                if (trailOwner[i] != player) continue;
                trailOwner[i] = None;
                SetOwner(i, player);
                converted++;
            }
            if (converted > 0) Version++;
            return converted;
        }

        /// <summary>Removes every trail cell of one player without capturing them (e.g. if a player is eliminated). Returns how many were removed.</summary>
        public int ClearTrailsOf(int player)
        {
            int removed = 0;
            for (int i = 0; i < trailOwner.Length; i++)
            {
                if (trailOwner[i] != player) continue;
                trailOwner[i] = None;
                removed++;
            }
            if (removed > 0) Version++;
            return removed;
        }
    }
}
