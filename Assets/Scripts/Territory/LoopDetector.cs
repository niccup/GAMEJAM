using System;
using System.Collections.Generic;

namespace GameJam
{
    /// <summary>
    /// Decides whether a player's color encloses an area and which cells that area consists of.
    /// <para>
    /// There is one kind of colored cell, so a "wall" is simply any cell currently showing the player's color, however
    /// it came to be (fresh trail, old trail, a previously captured area). That lets a new loop be built from any
    /// existing piece of the player's own color. A cell that an opponent has overwritten no longer shows the player's
    /// color, so an opponent's line cuts the wall; the pieces on either side can't form a loop together until the player
    /// reconnects them with their own color.
    /// </para>
    /// Method: flood-fill (4-connected) from every cell on the grid's border over non-wall cells. Whatever the flood
    /// cannot reach is enclosed. A 4-connected flood cannot squeeze through a diagonal line of cells, so thin lines are
    /// watertight. Works purely on grid data; it does not care how anything is drawn.
    /// </summary>
    public sealed class LoopDetector
    {
        readonly TerritoryGrid grid;
        readonly bool[] reachable;
        readonly int[] stack;

        public LoopDetector(TerritoryGrid grid)
        {
            this.grid = grid;
            reachable = new bool[grid.CellCount];
            stack = new int[grid.CellCount];
        }

        /// <summary>
        /// Fills <paramref name="result"/> with every enclosed cell that does not show the player's color yet: free cells
        /// and cells of other players alike, since a loop takes over everything inside it.
        /// Returns the number of cells (0 = nothing to capture).
        /// </summary>
        public int FindLoopCells(int player, List<int> result)
        {
            result.Clear();
            int w = grid.Width, h = grid.Height;
            Array.Clear(reachable, 0, reachable.Length);

            int top = 0;
            for (int x = 0; x < w; x++)
            {
                Push(grid.Index(x, 0), player, ref top);
                Push(grid.Index(x, h - 1), player, ref top);
            }
            for (int y = 0; y < h; y++)
            {
                Push(grid.Index(0, y), player, ref top);
                Push(grid.Index(w - 1, y), player, ref top);
            }

            while (top > 0)
            {
                int i = stack[--top];
                int x = i % w, y = i / w;
                if (x > 0) Push(i - 1, player, ref top);
                if (x < w - 1) Push(i + 1, player, ref top);
                if (y > 0) Push(i - w, player, ref top);
                if (y < h - 1) Push(i + w, player, ref top);
            }

            for (int i = 0; i < reachable.Length; i++)
                if (!reachable[i] && grid.GetColorOwner(i) != player) result.Add(i);

            return result.Count;
        }

        void Push(int index, int player, ref int top)
        {
            if (reachable[index] || grid.GetColorOwner(index) == player) return;
            reachable[index] = true;
            stack[top++] = index;
        }
    }
}
