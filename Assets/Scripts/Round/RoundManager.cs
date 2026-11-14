using System;
using System.Collections.Generic;
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
        [Tooltip("Optional. When set (together with Scores), 'Next Round' first runs a power-up draft (every player picks once, least territory first) and the round starts once the last player has picked. When empty, Next Round starts the round directly.")]
        [SerializeField] PowerUpSelection powerUps;

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
        int bonusRounds;

        public RoundState State { get; private set; } = RoundState.NotStarted;
        public RoundEndReason? EndReason { get; private set; }

        /// <summary>
        /// Total number of rounds the game consists of: the configured number plus one extra round for every Reset that was
        /// played (<see cref="AddRound"/>). The final results come after the last of them.
        /// </summary>
        public int MaxRounds => maxRounds + bonusRounds;

        /// <summary>Rounds added during the game (one per Reset).</summary>
        public int BonusRounds => bonusRounds;

        /// <summary>Number of the current (or just finished) round, starting at 1. 0 before the first round starts.</summary>
        public int CurrentRound { get; private set; }

        public bool IsFinalRound => CurrentRound >= MaxRounds;

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
            if (overlay != null) overlay.NextRoundRequested += OnNextRoundRequested;
        }

        void OnDisable()
        {
            if (overlay != null) overlay.NextRoundRequested -= OnNextRoundRequested;
        }

        // "Next Round" does not start the round itself: the power-up draft comes first. Every player gets one pick, in
        // ascending order of their share of the map that just ended (smallest first, ties -> lower player number). The
        // order is fixed here, once, and handed to the selection; the round starts as soon as the last player has picked,
        // so every power-up is in place before the recording and ghost snapshot of the new round are made.
        // Power-ups last one round only: the characters start without the previous round's ones. The finished round's
        // recording already holds its own snapshot, so its ghost keeps the boost it had.
        void OnNextRoundRequested()
        {
            if (State == RoundState.Playing || IsGameFinished) return;
            if (powerUps != null && powerUps.IsActive) return;

            spawner.ClearModifiers();

            var order = new List<Player>();
            if (powerUps != null && scores != null)
            {
                foreach (int index in scores.GetCurrentMapOrderAscending())
                    foreach (Player player in spawner.Players)
                        if (player.Index == index) order.Add(player);
            }

            if (order.Count == 0)
            {
                StartRound();
                return;
            }

            if (overlay != null) overlay.Hide();
            powerUps.Begin(order, StartRound);
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

        /// <summary>
        /// Adds one more round to the end of the game (used by Reset, which replaces the round it removes). Only meant to be
        /// called between rounds, before the next one starts; the new total is what <see cref="IsFinalRound"/> and the
        /// round counter on the end-of-round screen use from then on.
        /// </summary>
        public void AddRound() => bonusRounds++;

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
            if (overlay != null && scores != null) overlay.Show(scores, CurrentRound, MaxRounds);

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
