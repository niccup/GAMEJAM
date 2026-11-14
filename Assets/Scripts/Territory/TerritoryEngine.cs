using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// The complete capture mechanic in plain C#: owns the grid, one <see cref="PlayerTrail"/> per drawing character ("actor")
    /// and the <see cref="LoopDetector"/>. No MonoBehaviour, no rendering, no input - drive it with <see cref="Step"/>.
    /// <para>
    /// An actor is one drawing character belonging to a player. A player normally has one; replayed ghosts add more
    /// (<see cref="AddActor"/>). All actors of a player draw in the same color and share the same territory: they are
    /// treated identically, only their trails are tracked separately so they do not mix up each other's paths.
    /// </para>
    /// <para>
    /// There is a single kind of colored cell: a captured area is just a thick trail. Per step: lay trail (overwriting
    /// whatever is there, including other players' areas) -> if the emitter touches own color again, ask the detector
    /// whether a closed loop exists -> if so, fill the whole enclosed area with the player's color, overwriting
    /// everything inside it. Nothing is protected: opponents cut through any colored area the same way.
    /// </para>
    /// </summary>
    public sealed class TerritoryEngine
    {
        readonly Dictionary<int, PlayerTrail> trails = new Dictionary<int, PlayerTrail>();
        readonly LoopDetector detector;
        readonly List<int> loopCells = new List<int>();
        readonly float minLoopLength;
        int nextActor;

        public TerritoryGrid Grid { get; }

        /// <summary>Raised after a capture: (player index, number of cells filled).</summary>
        public event Action<int, int> LoopClosed;

        /// <summary>
        /// Raised at the moment a loop is completed, with the enclosed area: (actor that closed the loop, its player, the cells
        /// that were filled - every enclosed cell that did not show the player's color yet, see <see cref="LoopDetector"/>).
        /// The list is reused by the next capture, so read it during the call and do not keep it. Handlers must not step the engine.
        /// </summary>
        public event Action<int, int, IReadOnlyList<int>> LoopCaptured;

        public TerritoryEngine(TerritoryGrid grid, float minLoopLength)
        {
            Grid = grid;
            this.minLoopLength = minLoopLength;
            detector = new LoopDetector(grid);
        }

        /// <summary>Registers a new drawing character for a player. Returns its actor id for <see cref="Step"/>.</summary>
        public int AddActor(int player)
        {
            int actor = nextActor++;
            trails[actor] = new PlayerTrail(Grid, player, actor, minLoopLength);
            return actor;
        }

        /// <summary>Forgets an actor. The cells it colored stay on the grid like any other colored cell.</summary>
        public void RemoveActor(int actor) => trails.Remove(actor);

        /// <summary>Moves an actor's trail emitter to <paramref name="position"/>. Returns cells captured by this step (0 = none).</summary>
        public int Step(int actor, Vector2 position, float radius)
        {
            PlayerTrail trail = trails[actor];
            if (!trail.Advance(position, radius)) return 0;

            int player = trail.Player;
            int count = detector.FindLoopCells(player, loopCells);
            if (count == 0) return 0; // touching own color, but nothing is enclosed (the loop is open or was cut by an opponent)

            // Fill the enclosed area with the player's color, whoever's pixels were inside.
            for (int i = 0; i < loopCells.Count; i++) Grid.Fill(loopCells[i], player);

            LoopCaptured?.Invoke(actor, player, loopCells);
            LoopClosed?.Invoke(player, count);
            return count;
        }

        /// <summary>Clears the grid and every actor's trail state: back to an empty map. Actors stay registered.</summary>
        public void Reset()
        {
            Grid.Clear();
            foreach (PlayerTrail trail in trails.Values) trail.Reset();
        }

        /// <summary>Stop drawing for now; the next <see cref="Step"/> starts a fresh segment instead of joining the old position.</summary>
        public void BreakPath(int actor) => trails[actor].BreakPath();
    }
}
