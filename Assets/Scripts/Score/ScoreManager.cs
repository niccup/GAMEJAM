using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Keeps score across rounds. At the end of every round it stores each player's share of the map (percent of all
    /// cells showing their color - their character and all their ghosts together, every cell counted once).
    /// <para>
    /// Cumulative score = (percentages of all completed rounds + the current map's percentage while a round is running)
    /// divided by the number of rounds counted. Example: 25% in round 1 and 50% in round 2 gives 37.5% after round 2.
    /// The winner is the highest cumulative score once all rounds are done; equal top scores are a tie.
    /// </para>
    /// Pure scorekeeping: values are computed live during play but only the between-round screen (<see cref="RoundEndOverlay"/>) shows them.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [SerializeField] RoundManager round;
        [SerializeField] PlayerSpawner spawner;

        [Tooltip("Cumulative scores closer together than this (in percentage points) count as equal. Keep it tiny: it only " +
                 "absorbs floating point noise, a single cell of difference is already a win.")]
        [SerializeField, Min(0f)] float tiePercentTolerance = 0.0001f;

        // completed[player] = that player's percentage at the end of each finished round, oldest first.
        readonly List<float>[] completed = new List<float>[PlayersConfig.MaxPlayers];

        public int PlayerCount => PlayersConfig.MaxPlayers;

        /// <summary>Players that actually take part (the rest, e.g. in keyboard test mode, never win).</summary>
        public int ActivePlayerCount => spawner.Config != null ? Mathf.Min(spawner.Config.ActivePlayerCount, PlayerCount) : PlayerCount;

        public PlayersConfig PlayersConfig => spawner.Config;

        /// <summary>Number of rounds whose result is stored.</summary>
        public int CompletedRounds => completed[0].Count;

        void Awake()
        {
            for (int i = 0; i < completed.Length; i++) completed[i] = new List<float>();
        }

        void OnEnable() => round.RoundEnded += OnRoundEnded;
        void OnDisable() => round.RoundEnded -= OnRoundEnded;

        /// <summary>Forgets all stored rounds (new game).</summary>
        public void ResetScores()
        {
            foreach (List<float> list in completed) list.Clear();
        }

        /// <summary>The stored percentage (0-100) of a player in a finished round (0-based round index).</summary>
        public float GetCompletedRoundPercent(int player, int roundIndex) => completed[player][roundIndex];

        /// <summary>The player's share (0-100) of the map in the current round. Updates live.</summary>
        public float GetCurrentMapPercent(int player)
        {
            TerritoryCoverage coverage = round.Coverage;
            if (coverage == null) return 0f;
            coverage.Refresh();
            return coverage.GetFraction(player) * 100f;
        }

        /// <summary>
        /// Cumulative score (0-100): the mean of the percentages of every round played so far. While a round is running the
        /// current map counts as one more round, so the value moves live; once it has ended it is part of the stored rounds.
        /// </summary>
        public float GetCumulativePercent(int player)
        {
            float sum = 0f;
            foreach (float percent in completed[player]) sum += percent;
            int rounds = completed[player].Count;

            if (round.State == RoundState.Playing)
            {
                sum += GetCurrentMapPercent(player);
                rounds++;
            }

            return rounds == 0 ? 0f : sum / rounds;
        }

        /// <summary>
        /// The players with the highest cumulative score. One entry = a clear winner, several = a tie.
        /// Only active players are considered.
        /// </summary>
        public List<int> GetLeaders()
        {
            var leaders = new List<int>();
            float best = float.NegativeInfinity;
            int active = ActivePlayerCount;

            for (int i = 0; i < active; i++) best = Mathf.Max(best, GetCumulativePercent(i));
            for (int i = 0; i < active; i++)
                if (GetCumulativePercent(i) >= best - tiePercentTolerance) leaders.Add(i);
            return leaders;
        }

        /// <summary>
        /// The active player with the smallest share of the current map (not the cumulative score). On equal shares the
        /// lower player number is the loser.
        /// </summary>
        public int GetCurrentMapLoser()
        {
            int loser = 0;
            float worst = float.PositiveInfinity;
            int active = ActivePlayerCount;

            for (int i = 0; i < active; i++)
            {
                float percent = GetCurrentMapPercent(i);
                if (percent < worst - tiePercentTolerance)
                {
                    worst = percent;
                    loser = i;
                }
            }
            return loser;
        }

        /// <summary>
        /// The active players ordered by their share of the current map, smallest first (the power-up draft order). Equal
        /// shares are ordered by player number, so the result is always the same for the same map. Cumulative scores play no
        /// part. Call it once when the draft starts and keep the result: it is a snapshot.
        /// </summary>
        public List<int> GetCurrentMapOrderAscending()
        {
            int active = ActivePlayerCount;
            var order = new List<(int player, float percent)>(active);
            for (int i = 0; i < active; i++) order.Add((i, GetCurrentMapPercent(i)));

            order.Sort((a, b) =>
            {
                int byPercent = a.percent.CompareTo(b.percent);
                return byPercent != 0 ? byPercent : a.player.CompareTo(b.player);
            });

            var result = new List<int>(active);
            foreach (var entry in order) result.Add(entry.player);
            return result;
        }

        void OnRoundEnded(RoundEndReason reason)
        {
            // The coverage is final at this point. Store every player's share of the map for this round.
            for (int i = 0; i < completed.Length; i++) completed[i].Add(GetCurrentMapPercent(i));
        }
    }
}
