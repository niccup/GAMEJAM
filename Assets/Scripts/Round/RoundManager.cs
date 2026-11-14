using System;
using UnityEngine;
using UnityEngine.Events;

namespace GameJam
{
    public enum RoundState { NotStarted, Playing, Ended }

    public enum RoundEndReason { TimeUp, MapCovered }

    /// <summary>
    /// Runs the round: counts the timer down, watches the map coverage, ends the round when a condition is met and
    /// starts the next one on request. While a round is not <see cref="RoundState.Playing"/> the players and the
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

        TerritoryCoverage coverage;

        public RoundState State { get; private set; } = RoundState.NotStarted;
        public RoundEndReason? EndReason { get; private set; }

        public float RoundDuration => roundDuration;
        public float TimeRemaining { get; private set; }
        public TerritoryCoverage Coverage => coverage;

        /// <summary>Raised after the map and players have been reset and play begins.</summary>
        public event Action RoundStarted;

        /// <summary>Raised once when a round ends (coverage is final at this point).</summary>
        public event Action<RoundEndReason> RoundEnded;

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

            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            coverage.Refresh();

            if (endOnCoverage && coverage.CoveredFraction * 100f >= coverageEndPercent) EndRound(RoundEndReason.MapCovered);
            else if (endOnTimeUp && TimeRemaining <= 0f) EndRound(RoundEndReason.TimeUp);
        }

        /// <summary>Resets map, players, trails and timer and begins a new round. Settings are untouched.</summary>
        public void StartRound()
        {
            territory.ResetTerritory();
            spawner.ResetPlayers();
            coverage.Refresh();

            TimeRemaining = roundDuration;
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

            if (overlay != null) overlay.Show(coverage, spawner.Config);
            RoundEnded?.Invoke(reason);
            onRoundEnded?.Invoke(reason);
        }

        void SetFrozen(bool frozen)
        {
            territory.Running = !frozen;
            spawner.SetPlayersMovable(!frozen);
        }
    }
}
