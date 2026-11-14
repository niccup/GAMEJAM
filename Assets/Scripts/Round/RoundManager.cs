using System;
using UnityEngine;
using UnityEngine.Events;

namespace GameJam
{
    public enum RoundState { NotStarted, Playing, Ended }

    public enum RoundEndReason { TimeUp, MapCovered }

    /// <summary>
    /// Runs the game as a fixed number of consecutive rounds: counts the round timer down, watches the map coverage,
    /// ends the round when a condition is met and starts the next one on request. After the last round no further round
    /// can start (<see cref="IsGameFinished"/>). While a round is not <see cref="RoundState.Playing"/> the players and the
    /// territory are frozen. Everything that happens on a state change is delivered as events, so UI, sound or scoring
    /// can hook in without this class knowing about them (see <see cref="RoundEndOverlay"/>).
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] TerritorySystem territory;
        [SerializeField] PlayerSpawner spawner;
        [Tooltip("Optional. Shown when the round ends; its Next Round button starts the next round.")]
        [SerializeField] RoundEndOverlay overlay;
        [Tooltip("Provides the scores the end-of-round screen shows. Required for the overlay.")]
        [SerializeField] ScoreManager scores;

        [Header("Game")]
        [Tooltip("Number of consecutive rounds the game consists of. After the last one the final results are shown and no new round starts.")]
        [SerializeField, Min(1)] int maxRounds = 4;

        [Header("Timer")]
        [Tooltip("End the round when the timer reaches zero.")]
        [SerializeField] bool endOnTimeUp = true;
        [Tooltip("Round length in seconds.")]
        [SerializeField, Min(1f)] float roundDuration = 120f;

        [Header("Map coverage")]
        [Tooltip("End the round when enough of the map is covered by players' colors (territory or trail).")]
        [SerializeField] bool endOnCoverage = true;
        [Tooltip("Percentage of the whole map that has to be covered (by any players, territory or trail) to end the round.")]
        [SerializeField, Range(1f, 100f)] float coverageEndPercent = 90f;

        [Header("Flow")]
        [Tooltip("Start the first round automatically when the scene starts.")]
        [SerializeField] bool startOnAwake = true;

        [Header("Events")]
        public UnityEvent onRoundStarted;
        public UnityEvent<RoundEndReason> onRoundEnded;
        public UnityEvent onGameFinished;

        TerritoryCoverage coverage;

        public RoundState State { get; private set; } = RoundState.NotStarted;
        public RoundEndReason? EndReason { get; private set; }

        public int MaxRounds => maxRounds;

        /// <summary>Number of the current (or just finished) round, starting at 1. 0 before the first round starts.</summary>
        public int CurrentRound { get; private set; }

        public bool IsFinalRound => CurrentRound >= maxRounds;

        /// <summary>True once the last round has ended; no further round can be started.</summary>
        public bool IsGameFinished => State == RoundState.Ended && IsFinalRound;

        public float RoundDuration => roundDuration;
        public float TimeRemaining { get; private set; }

        /// <summary>Seconds since the current round started. Replays are synchronized to this clock.</summary>
        public float Elapsed { get; private set; }
        public TerritoryCoverage Coverage => coverage;

        /// <summary>Raised after the map and players have been reset and play begins.</summary>
        public event Action RoundStarted;

        /// <summary>Raised once when a round ends (coverage is final at this point).</summary>
        public event Action<RoundEndReason> RoundEnded;

        /// <summary>Raised after the last round has ended (right after <see cref="RoundEnded"/>).</summary>
        public event Action GameFinished;

        void Awake() => TimeRemaining = roundDuration;

        void OnEnable()
        {
            if (overlay != null) overlay.NextRoundRequested += StartRound;
        }

        void OnDisable()
        {
            if (overlay != null) overlay.NextRoundRequested -= StartRound;
        }

        void Start()
        {
            // The grid is created in TerritorySystem.Awake, so it is safe to use from Start on.
            coverage = new TerritoryCoverage(territory.Grid);
            if (overlay != null) overlay.Hide();
            if (startOnAwake) StartRound();
            else SetFrozen(true);
        }

        void Update()
        {
            if (State != RoundState.Playing) return;

            Elapsed += Time.deltaTime;
            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            coverage.Refresh();

            if (endOnCoverage && coverage.CoveredFraction * 100f >= coverageEndPercent) EndRound(RoundEndReason.MapCovered);
            else if (endOnTimeUp && TimeRemaining <= 0f) EndRound(RoundEndReason.TimeUp);
        }

        /// <summary>
        /// Resets map, players, trails and timer and begins the next round. Settings are untouched.
        /// Ignored while a round is running or once the last round is over.
        /// </summary>
        public void StartRound()
        {
            if (State == RoundState.Playing || IsGameFinished) return;

            CurrentRound++;
            territory.ResetTerritory();
            spawner.ResetPlayers();
            coverage.Refresh();

            TimeRemaining = roundDuration;
            Elapsed = 0f;
            EndReason = null;
            State = RoundState.Playing;
            SetFrozen(false);

            if (overlay != null) overlay.Hide();
            RoundStarted?.Invoke();
            onRoundStarted?.Invoke();
        }

        /// <summary>Ends the current round right now (also usable from other scripts, e.g. a debug key).</summary>
        public void EndRound(RoundEndReason reason)
        {
            if (State != RoundState.Playing) return;

            coverage.Refresh();
            State = RoundState.Ended;
            EndReason = reason;
            SetFrozen(true);

            // Listeners first (the ScoreManager stores this round's result), then the screen that displays the scores.
            RoundEnded?.Invoke(reason);
            onRoundEnded?.Invoke(reason);
            if (overlay != null && scores != null) overlay.Show(scores, CurrentRound, maxRounds);

            if (IsFinalRound)
            {
                GameFinished?.Invoke();
                onGameFinished?.Invoke();
            }
        }

        void SetFrozen(bool frozen)
        {
            territory.Running = !frozen;
            spawner.SetPlayersMovable(!frozen);
        }
    }
}
