using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// The complete capture mechanic in plain C#: owns the grid, one <see cref="PlayerTrail"/> per player and the
    /// <see cref="LoopDetector"/>. No MonoBehaviour, no rendering, no input - drive it with <see cref="Step"/>.
    /// <para>
    /// Per step: lay trail -> if the emitter touches its own trail or territory again, ask the detector whether a closed
    /// loop exists -> if so, fill the enclosed area with the player's color and turn the player's trail into territory.
    /// Trail and territory look identical; no trail is ever erased by a capture. Other players' trails keep their
    /// own layer, so crossing a finished loop never affects the capture.
    /// </para>
    /// </summary>
    public sealed class TerritoryEngine
    {
        readonly PlayerTrail[] trails;
        readonly LoopDetector detector;
        readonly List<int> loopCells = new List<int>();

        public TerritoryGrid Grid { get; }

        /// <summary>Raised after a capture: (player index, number of cells filled).</summary>
        public event Action<int, int> LoopClosed;

        public TerritoryEngine(TerritoryGrid grid, float minLoopLength)
        {
            Grid = grid;
            detector = new LoopDetector(grid);
            trails = new PlayerTrail[grid.PlayerCount];
            for (int i = 0; i < trails.Length; i++) trails[i] = new PlayerTrail(grid, i, minLoopLength);
        }

        /// <summary>Moves a player's trail emitter to <paramref name="position"/>. Returns cells captured by this step (0 = none).</summary>
        public int Step(int player, Vector2 position, float radius)
        {
            if (!trails[player].Advance(position, radius)) return 0;

            int count = detector.FindLoopCells(player, loopCells);
            if (count == 0) return 0; // touching its own trail, but the loop is open or was interrupted

            // Fill the enclosed area. Other players' trails inside it are left alone (only the territory layer changes).
            for (int i = 0; i < loopCells.Count; i++) Grid.SetOwner(loopCells[i], player);

            // The player's own trail - loop and lead-in alike - becomes territory in place: nothing disappears.
            Grid.CommitTrails(player);

            LoopClosed?.Invoke(player, count);
            return count;
        }

        /// <summary>Stop drawing for now; the next <see cref="Step"/> starts a fresh segment instead of joining the old position.</summary>
        public void BreakPath(int player) => trails[player].BreakPath();
    }
}
