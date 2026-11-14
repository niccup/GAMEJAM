using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Pure data model of the arena: a uniform grid of square cells. A cell has exactly one thing that matters: the
    /// color (player) it currently shows, or <see cref="None"/>. There is no separate "trail" and "territory" - a captured
    /// area is just a thick trail, and every colored cell follows the same rules: anyone can overwrite it.
    /// <para>
    /// Next to the color, each cell remembers who last <i>stamped</i> it while moving and how far along that path, only so
    /// a character can tell "the trail I laid a moment ago right behind me" from older colored cells (see
    /// <see cref="PlayerTrail"/>). Cells filled by a captured loop carry no stamp.
    /// </para>
    /// Knows nothing about rendering, physics or input. Cells are addressed by a flat index (<c>y * Width + x</c>);
    /// cell (0, 0) is the bottom-left of the map. <see cref="Version"/> changes whenever any cell changes.
    /// </summary>
    public sealed class TerritoryGrid
    {
        public const int None = -1;

        readonly int[] color;
        readonly int[] stamper;
        readonly float[] stampDistance;
        readonly int[] coloredCount;

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

            color = new int[CellCount];
            stamper = new int[CellCount];
            stampDistance = new float[CellCount];
            coloredCount = new int[playerCount];
            for (int i = 0; i < color.Length; i++) color[i] = stamper[i] = None;
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

        // ---- color layer -------------------------------------------------------------------------------------

        /// <summary>Whose color the cell shows, or <see cref="None"/>. The single source of truth for rendering, loops and scoring.</summary>
        public int GetColorOwner(int index) => color[index];

        /// <summary>Number of cells showing a player's color (trail and captured area alike).</summary>
        public int GetOwnedCount(int player) => coloredCount[player];

        /// <summary>
        /// Colors a cell while a character moves over it, overwriting whatever was there (free cell or any other player's
        /// color). Remembers who stamped it and how far along its path, see <see cref="GetStamper"/>.
        /// </summary>
        public void Stamp(int index, int player, int source, float pathDistance)
        {
            stamper[index] = source;
            stampDistance[index] = pathDistance;
            Paint(index, player);
        }

        /// <summary>Colors a cell as part of a captured area, overwriting whatever was there. Carries no stamp.</summary>
        public void Fill(int index, int player)
        {
            stamper[index] = None;
            Paint(index, player);
        }

        /// <summary>Which trail source (one character) last stamped this cell, or <see cref="None"/> for free and filled cells.</summary>
        public int GetStamper(int index) => stamper[index];

        /// <summary>Path length (world units) the stamping source had travelled when it stamped this cell.</summary>
        public float GetStampDistance(int index) => stampDistance[index];

        /// <summary>Empties the whole grid. Used to start a new round.</summary>
        public void Clear()
        {
            for (int i = 0; i < color.Length; i++)
            {
                color[i] = stamper[i] = None;
                stampDistance[i] = 0f;
            }
            System.Array.Clear(coloredCount, 0, coloredCount.Length);
            Version++;
        }

        void Paint(int index, int player)
        {
            int previous = color[index];
            if (previous == player) return;
            if (previous != None) coloredCount[previous]--;
            coloredCount[player]++;
            color[index] = player;
            Version++;
        }
    }
}
