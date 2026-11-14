using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Reset: the chooser picks one finished round and removes it from the game. Picking runs in two steps - browse the
    /// rounds (<see cref="SelectRoundStep"/>, with a preview of every ghost that came from the round) and confirm
    /// (<see cref="ResetConfirmStep"/>) - and nothing changes until the second one is confirmed.
    /// <para>
    /// Removing a round (see <see cref="Apply"/>): every saved movement that was played in it is dropped from the history,
    /// so none of those ghosts is ever spawned again; the movements of all other rounds are untouched. The game gets one
    /// extra round to replace it (<see cref="RoundManager.AddRound"/>), so it still has as many playable rounds as planned,
    /// and - unless switched off below - the round's scores leave the totals too, so the final results are the mean of the
    /// planned number of rounds.
    /// </para>
    /// <para>
    /// Reset is a game-wide power-up (<see cref="OncePerGame"/>): once any player has taken it, it is no longer offered in
    /// any later draft.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "GameJam/Power-Ups/Reset", fileName = "Reset")]
    public class ResetPowerUp : PowerUp
    {
        [Header("Effect")]
        [Tooltip("Also take the removed round out of the scores. On: the round is gone from the game completely, its replacement round " +
                 "is scored instead and the final result is the mean of the planned number of rounds. Off: the removed round's " +
                 "scores stay in the mean, which then covers one round more.")]
        [SerializeField] bool removeRoundScore = true;

        [Header("Preview")]
        [Tooltip("How fast the round previews play, in recorded seconds per real second (30 = a two-minute round in four seconds). " +
                 "Only the previews use it; the real ghost replays and the game are not affected.")]
        [SerializeField, Min(0.1f)] float previewSpeed = 30f;

        public float PreviewSpeed => previewSpeed;

        public override bool OncePerGame => true;

        /// <summary>Available while at least one finished round still has ghosts to remove.</summary>
        public override bool IsAvailable(PowerUpContext context) =>
            context.Replay != null && context.Round != null && context.Replay.GetSavedRounds().Count > 0;

        public override SelectionStep Use(PowerUpContext context) => new SelectRoundStep(context, this);

        /// <summary>Every ghost of <paramref name="roundNumber"/>, ready to be previewed in the colour of the player it belongs to now.</summary>
        public static List<PreviewTrack> CreateTracks(PowerUpContext context, int roundNumber)
        {
            var tracks = new List<PreviewTrack>();
            foreach (var (player, recording) in context.Replay.GetRecordingsOfRound(roundNumber))
            {
                Player owner = GhostLabels.PlayerFor(context, player);
                if (owner != null) tracks.Add(new PreviewTrack(recording, owner));
            }
            return tracks;
        }

        /// <summary>
        /// Removes round <paramref name="roundNumber"/> from the game and adds the extra round. Returns false (changing
        /// nothing) if the round has no ghosts left or the game has no round counter.
        /// </summary>
        public bool Apply(PowerUpContext context, int roundNumber)
        {
            if (context.Round == null || context.Replay == null || !context.Replay.GetSavedRounds().Contains(roundNumber))
            {
                Debug.LogWarning($"Reset refused: round {roundNumber} cannot be removed.", this);
                return false;
            }

            int ghosts = context.Replay.RemoveRound(roundNumber);
            if (removeRoundScore && context.Scores != null) context.Scores.RemoveRound(roundNumber);
            context.Round.AddRound();

            Debug.Log($"Player {context.Chooser.Index + 1} reset round {roundNumber}: {ghosts} ghost(s) removed, the game now has {context.Round.MaxRounds} rounds.", this);
            return true;
        }
    }
}
