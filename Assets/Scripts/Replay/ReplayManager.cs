using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// The "previous rounds" mechanic. While a round runs it records every player's movement; when the round ends the
    /// recording is saved in that player's own history. Each new round then gives every player one ghost per saved
    /// round, all replaying their recorded path from the shared round clock next to the player's live character.
    /// <para>
    /// Ghosts draw through the <see cref="TerritorySystem"/> as extra emitters of their player, so their trails, loops and
    /// territory are the player's own and treated like the live character's by every system. Round 1 has no ghosts,
    /// round 2 one per player, and so on - the number of rounds is set on the <see cref="RoundManager"/>.
    /// </para>
    /// Runs before <see cref="TerritorySystem"/> in LateUpdate so ghosts are in place when trails are laid.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class ReplayManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] RoundManager round;
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] TerritorySystem territory;

        [Header("Recording")]
        [Tooltip("Minimum time between recorded position samples, in seconds. 0 = every frame (most exact; the data is small).")]
        [SerializeField, Min(0f)] float recordInterval;

        [Header("Ghost look")]
        [Tooltip("Ghosts are the player's color blended towards this color, so they stand out from the live character. Their trails keep the exact player color.")]
        [SerializeField] Color ghostTint = Color.white;

        [Tooltip("How far the ghost color moves from the player's color towards the tint color (0 = identical, 1 = tint color).")]
        [SerializeField, Range(0f, 1f)] float ghostTintAmount = 0.35f;

        [Tooltip("Opacity of the ghost character's sprite.")]
        [SerializeField, Range(0.1f, 1f)] float ghostAlpha = 0.85f;

        [Tooltip("Added to the player sprite's sorting order. Negative = ghosts are drawn behind live characters.")]
        [SerializeField] int ghostSortingOrderOffset = -1;

        [Header("Replay")]
        [Tooltip("Hide a ghost once it has replayed its whole path (it also stops drawing trail either way).")]
        [SerializeField] bool hideGhostWhenPathEnds = true;

        // history[player] = that player's saved recordings, oldest round first.
        readonly List<MovementRecording>[] history = new List<MovementRecording>[PlayersConfig.MaxPlayers];
        readonly MovementRecording[] current = new MovementRecording[PlayersConfig.MaxPlayers];
        readonly List<PlayerGhost> ghosts = new List<PlayerGhost>();

        public IReadOnlyList<PlayerGhost> Ghosts => ghosts;

        /// <summary>Number of saved previous rounds of a player (= ghosts that player gets in the next round).</summary>
        public int GetSavedRoundCount(int player) => history[player].Count;

        /// <summary>One saved round of a player (0-based, oldest first). This is what their ghost G{index + 1} replays.</summary>
        public MovementRecording GetRecording(int player, int index) => history[player][index];

        /// <summary>
        /// Swaps a saved recording for another one (e.g. its mirrored version). From the next round on the ghost replays the
        /// new one, so the replacement has to be the final movement - ghosts never transform anything while replaying.
        /// </summary>
        public void ReplaceRecording(int player, int index, MovementRecording recording)
        {
            if (recording == null) return;
            history[player][index] = recording;
        }

        void Awake()
        {
            for (int i = 0; i < history.Length; i++) history[i] = new List<MovementRecording>();
        }

        void OnEnable()
        {
            round.RoundStarted += OnRoundStarted;
            round.RoundEnded += OnRoundEnded;
        }

        void OnDisable()
        {
            round.RoundStarted -= OnRoundStarted;
            round.RoundEnded -= OnRoundEnded;
        }

        /// <summary>Forgets all saved rounds (e.g. before a brand new game). Ghosts disappear with the next round start.</summary>
        public void ClearHistory()
        {
            foreach (List<MovementRecording> list in history) list.Clear();
        }

        void OnRoundStarted()
        {
            // The map has just been reset by the RoundManager; old ghosts belong to the previous round.
            DestroyGhosts();
            System.Array.Clear(current, 0, current.Length);
            foreach (Player player in spawner.Players) EnsureRecording(player); // players are at their spawn corners right now
            SpawnGhosts();
        }

        void OnRoundEnded(RoundEndReason reason)
        {
            // Save each player's path from this round. Rounds are saved per player and never mixed.
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] != null && current[i].SampleCount > 0) history[i].Add(current[i]);
                current[i] = null;
            }
        }

        void LateUpdate()
        {
            if (round.State != RoundState.Playing) return;
            float time = round.Elapsed;

            RecordPlayers(time);
            foreach (PlayerGhost ghost in ghosts) ghost.Apply(time);
        }

        // Records exactly what the territory system will see this frame: the live emitters' final positions.
        void RecordPlayers(float time)
        {
            foreach (Player player in spawner.Players)
            {
                MovementRecording recording = EnsureRecording(player);
                if (recording != null && time - recording.Duration >= recordInterval)
                    recording.Add(time, player.transform.position, player.TrailEmitter.Emitting);
            }
        }

        // Starts a player's recording for this round, anchored at t = 0 where the character stands now (its spawn corner).
        MovementRecording EnsureRecording(Player player)
        {
            if (current[player.Index] != null) return current[player.Index];
            if (player.TrailEmitter == null) return null;

            var recording = new MovementRecording(player.Modifiers); // snapshot: the ghost keeps what the player had in this round
            recording.Add(0f, player.transform.position, player.TrailEmitter.Emitting);
            return current[player.Index] = recording;
        }

        void SpawnGhosts()
        {
            foreach (Player player in spawner.Players)
            {
                List<MovementRecording> saved = history[player.Index];
                Color ghostColor = GhostColor(player.Color);

                for (int i = 0; i < saved.Count; i++)
                {
                    var ghost = PlayerGhost.Create(player, saved[i], ghostColor, ghostSortingOrderOffset, hideGhostWhenPathEnds,
                        $"Ghost P{player.Index + 1} R{i + 1}", transform);
                    territory.AddEmitter(ghost.TrailEmitter, player.Index);
                    ghosts.Add(ghost);
                }
            }
        }

        void DestroyGhosts()
        {
            foreach (PlayerGhost ghost in ghosts)
            {
                territory.RemoveEmitter(ghost.TrailEmitter);
                Destroy(ghost.gameObject);
            }
            ghosts.Clear();
        }

        Color GhostColor(Color playerColor)
        {
            Color color = Color.Lerp(playerColor, ghostTint, ghostTintAmount);
            color.a = ghostAlpha;
            return color;
        }
    }
}
