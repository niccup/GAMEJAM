using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Loop capture eliminates ghosts: when a player's CURRENT character completes a loop, every ghost of another player that
    /// stands inside the enclosed area at that moment is eliminated for the rest of the round (it disappears and stops
    /// drawing, see <see cref="PlayerGhost.Eliminate"/>).
    /// <para>
    /// Deliberately small and separate from recording and replay: it listens to <see cref="TerritorySystem.LoopCaptured"/>,
    /// which fires inside the step that closes the loop, and tests each ghost's current position against the cells the loop
    /// actually captured. It never reads or writes a <see cref="MovementRecording"/>, so the ghost's saved path, Mirror
    /// and Swap are untouched; the ghost object is rebuilt from the unchanged history when the next round starts.
    /// Because a ghost is tested where it is right now, a mirrored or swapped movement needs no special handling. Whose
    /// ghost it is follows its colour (<see cref="PlayerGhost.PlayerIndex"/> is the player the ghost is drawn in), never
    /// whose movement it replays: a swapped ghost is an opponent's ghost to everyone but the player in whose colour it is shown.
    /// </para>
    /// Not affected: the closing player's own ghosts, other players' live characters, ghosts outside the area, ghosts that
    /// have already finished their path (they are hidden and no longer draw), and loops closed by a ghost.
    /// </summary>
    public class GhostElimination : MonoBehaviour
    {
        [SerializeField] TerritorySystem territory;
        [SerializeField] ReplayManager replay;
        [SerializeField] PlayerSpawner spawner;

        readonly List<(PlayerGhost ghost, int cell)> candidates = new List<(PlayerGhost, int)>();

        /// <summary>Raised when a ghost is eliminated: (the ghost, the player whose loop caught it).</summary>
        public event Action<PlayerGhost, int> GhostEliminated;

        void OnEnable() => territory.LoopCaptured += OnLoopCaptured;
        void OnDisable() => territory.LoopCaptured -= OnLoopCaptured;

        void OnLoopCaptured(TrailEmitter emitter, int player, IReadOnlyList<int> cells)
        {
            // Only a loop closed by a player's own, currently controlled character counts - not one a ghost closes.
            if (!IsLiveCharacter(emitter)) return;

            // Where each opponent ghost is right now, as a grid cell. Ghosts are placed before the territory step of this frame.
            TerritoryGrid grid = territory.Grid;
            candidates.Clear();
            foreach (PlayerGhost ghost in replay.Ghosts)
            {
                if (ghost.PlayerIndex == player || !ghost.IsOnArena) continue;

                Vector2Int cell = grid.WorldToCell(ghost.transform.position);
                if (grid.InBounds(cell.x, cell.y)) candidates.Add((ghost, grid.Index(cell.x, cell.y)));
            }
            if (candidates.Count == 0) return;

            // A ghost is caught if its cell is one of the enclosed cells the loop just captured.
            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                for (int c = candidates.Count - 1; c >= 0; c--)
                {
                    if (candidates[c].cell != cell) continue;

                    PlayerGhost caught = candidates[c].ghost;
                    candidates.RemoveAt(c);
                    Eliminate(caught, player);
                }
                if (candidates.Count == 0) return;
            }
        }

        void Eliminate(PlayerGhost ghost, int byPlayer)
        {
            ghost.Eliminate();
            Debug.Log($"{ghost.name} was eliminated by Player {byPlayer + 1}'s loop.", ghost);
            GhostEliminated?.Invoke(ghost, byPlayer);
        }

        bool IsLiveCharacter(TrailEmitter emitter)
        {
            foreach (Player player in spawner.Players)
                if (player.TrailEmitter == emitter) return true;
            return false;
        }
    }
}
