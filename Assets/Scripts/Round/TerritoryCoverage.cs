namespace GameJam
{
    /// <summary>
    /// Measures how much of the map each player's color covers. A cell counts for a player when it shows that player's
    /// color - there is no difference between trail and captured area - exactly what the <see cref="TerritoryRenderer"/> draws.
    /// Plain C#, no Unity dependencies besides the grid. Call <see cref="Refresh"/> before reading; it only recounts
    /// when the grid has changed.
    /// </summary>
    public sealed class TerritoryCoverage
    {
        readonly TerritoryGrid grid;
        readonly int[] cells;
        int countedVersion = -1;

        public TerritoryCoverage(TerritoryGrid grid)
        {
            this.grid = grid;
            cells = new int[grid.PlayerCount];
        }

        public int PlayerCount => cells.Length;
        public int TotalCells => grid.CellCount;

        /// <summary>Cells covered by any player's color.</summary>
        public int CoveredCells { get; private set; }

        /// <summary>Fraction (0..1) of the whole map covered by any color.</summary>
        public float CoveredFraction => (float)CoveredCells / TotalCells;

        /// <summary>Cells showing this player's color.</summary>
        public int GetCells(int player) => cells[player];

        /// <summary>Fraction (0..1) of the whole map showing this player's color.</summary>
        public float GetFraction(int player) => (float)cells[player] / TotalCells;

        /// <summary>Re-reads the per-player counts if the grid changed since the last call.</summary>
        public void Refresh()
        {
            if (grid.Version == countedVersion) return;
            countedVersion = grid.Version;

            int covered = 0;
            for (int player = 0; player < cells.Length; player++)
            {
                cells[player] = grid.GetOwnedCount(player);
                covered += cells[player];
            }
            CoveredCells = covered;
        }
    }
}
