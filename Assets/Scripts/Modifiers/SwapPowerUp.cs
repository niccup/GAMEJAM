using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Swap: the chooser trades the complete movement of one of their own ghosts for the movement of one ghost of an
    /// opponent. Picking runs in three steps - the opponent, the opponent's ghost, the chooser's own ghost - using the same
    /// player and ghost steps (and the same preview) as Mirror; confirming the own ghost swaps at once, and nothing changes
    /// before that.
    /// <para>
    /// A saved <see cref="MovementRecording"/> is everything a ghost does - its path against time (so its speed,
    /// acceleration, turns and inertia exactly as they were played) and the modifiers it was recorded with (Speed Boost) -
    /// so swapping the two recordings swaps all of it, and a ghost that was mirrored keeps its mirrored path. The ghosts stay
    /// with their players: colour and territory ownership follow the ghost's slot, only the movement changes hands. The
    /// swapped recordings are stored in each other's place and are the history every later round replays. Both are marked
    /// swapped, and a swapped ghost cannot take part in another Swap.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "GameJam/Power-Ups/Swap", fileName = "Swap")]
    public class SwapPowerUp : PowerUp
    {
        [Header("Preview")]
        [Tooltip("How fast the ghost previews play, in recorded seconds per real second (30 = a two-minute round in four seconds). " +
                 "Only the previews use it; the real ghost replays and the game are not affected.")]
        [SerializeField, Min(0.1f)] float previewSpeed = 30f;

        public float PreviewSpeed => previewSpeed;

        /// <summary>Available while the chooser and at least one opponent each have a ghost that has not been swapped yet.</summary>
        public override bool IsAvailable(PowerUpContext context)
        {
            if (context.Replay == null) return false;
            if (!HasSwappableGhost(context, context.Chooser.Index)) return false;

            foreach (Player player in context.Spawner.Players)
                if (IsValidOpponent(context, player)) return true;
            return false;
        }

        public override SelectionStep Use(PowerUpContext context) => new SelectPlayerStep(context,
            player => $"Swap - select an opponent: Player {player + 1}",
            "LEFT / RIGHT: choose opponent     ACTION: confirm     DOWN: back",
            player => IsValidOpponent(context, player),
            opponent => context.Push(new SelectGhostStep(context, opponent.Index,
                ghost => $"Swap - Player {opponent.Index + 1}: select their ghost (G{ghost + 1})",
                "LEFT / RIGHT: choose ghost     ACTION: confirm     DOWN: back to opponents",
                ghost => IsSwappable(context, opponent.Index, ghost),
                "already swapped",
                previewSpeed,
                opponentGhost => context.Push(new SelectGhostStep(context, context.Chooser.Index,
                    ghost => $"Swap - your ghost for Player {opponent.Index + 1}'s G{opponentGhost + 1}: G{ghost + 1}",
                    "LEFT / RIGHT: choose ghost     ACTION: swap now     DOWN: back to their ghost",
                    ghost => IsSwappable(context, context.Chooser.Index, ghost),
                    "already swapped",
                    previewSpeed,
                    ownGhost =>
                    {
                        Apply(context, context.Chooser.Index, ownGhost, opponent.Index, opponentGhost);
                        context.Complete();
                    })))));

        /// <summary>Another player (not the chooser) with at least one ghost that has not been swapped yet.</summary>
        public static bool IsValidOpponent(PowerUpContext context, Player player) =>
            player != context.Chooser && HasSwappableGhost(context, player.Index);

        public static bool HasSwappableGhost(PowerUpContext context, int player)
        {
            int count = context.Replay.GetSavedRoundCount(player);
            for (int ghost = 0; ghost < count; ghost++)
                if (IsSwappable(context, player, ghost)) return true;
            return false;
        }

        /// <summary>A saved ghost that has not been through a Swap yet.</summary>
        public static bool IsSwappable(PowerUpContext context, int player, int ghost) =>
            !context.Replay.GetRecording(player, ghost).IsSwapped;

        /// <summary>
        /// Exchanges the saved movements of two ghosts of different players and marks both as swapped. Each ghost keeps its
        /// slot - and with it its player's colour and territory - and gets the other one's complete movement, which every
        /// later round replays. Returns false (changing nothing) if the ghosts are not eligible.
        /// </summary>
        public bool Apply(PowerUpContext context, int playerA, int ghostA, int playerB, int ghostB)
        {
            ReplayManager replay = context.Replay;
            if (playerA == playerB || !IsSwappable(context, playerA, ghostA) || !IsSwappable(context, playerB, ghostB))
            {
                Debug.LogWarning("Swap refused: the ghosts are not eligible.", this);
                return false;
            }

            // Both copies are made before either is stored, so the exchange is all or nothing.
            MovementRecording movementA = replay.GetRecording(playerA, ghostA).MarkedAsSwapped();
            MovementRecording movementB = replay.GetRecording(playerB, ghostB).MarkedAsSwapped();
            replay.ReplaceRecording(playerA, ghostA, movementB);
            replay.ReplaceRecording(playerB, ghostB, movementA);

            Debug.Log($"Player {context.Chooser.Index + 1} swapped their ghost G{ghostA + 1} (Player {playerA + 1}) with G{ghostB + 1} of Player {playerB + 1}.", this);
            return true;
        }
    }
}
