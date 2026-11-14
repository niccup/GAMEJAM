using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// The end-of-round screen: one percentage label per player and a "Next Round" button, on a Screen Space - Overlay
    /// canvas above the map. Pure view - it only displays numbers it is given and raises
    /// <see cref="NextRoundRequested"/>. Build the hierarchy with Tools > GameJam > Setup Scene, then restyle it freely.
    /// Put this component on an object that stays active (e.g. the Canvas); <see cref="panel"/> is what gets shown/hidden.
    /// </summary>
    public class RoundEndOverlay : MonoBehaviour
    {
        [Tooltip("Root of the overlay content. Activated when the round ends, deactivated when it starts.")]
        [SerializeField] GameObject panel;

        [Tooltip("One label per player (index = player index), shown at the top.")]
        [SerializeField] TMP_Text[] percentageLabels = new TMP_Text[PlayersConfig.MaxPlayers];

        [Tooltip("Optional. Shows which round just ended, or that these are the final results.")]
        [SerializeField] TMP_Text titleLabel;

        [Tooltip("Optional. On the final screen: who won, or that it is a tie.")]
        [SerializeField] TMP_Text winnerLabel;

        [Tooltip("Next Round button. Hidden on the final results screen.")]
        [SerializeField] Button nextRoundButton;

        [Header("Look")]
        [Tooltip("Label text after a round that is not the last. {0} = player number (1-4), {1} = cumulative score (0-100), {2} = share of this round's map (0-100).")]
        [SerializeField] string labelFormat = "P{0}\n{1:0.0}%\n<size=45%>this map {2:0.0}%</size>";

        [Tooltip("Label text on the final screen. {0} = player number, {1} = final cumulative score, {2} = share of the last map, {3} = tag (winner / tie / empty).")]
        [SerializeField] string finalLabelFormat = "P{0}\n{1:0.0}%\n<size=45%>{3}</size>";

        [Tooltip("Tag under the label of the single winner.")]
        [SerializeField] string winnerTag = "WINNER";

        [Tooltip("Tag under the labels of players sharing the top score.")]
        [SerializeField] string tieTag = "TIE";

        [Tooltip("Heading of the winner line. {0} = player number.")]
        [SerializeField] string winnerFormat = "Winner: Player {0}";

        [Tooltip("Heading when several players share the top score. {0} = their player numbers, e.g. '1 & 3'.")]
        [SerializeField] string tieFormat = "Tie: Players {0}";

        [Tooltip("Title after a round that is not the last. {0} = round number, {1} = total rounds.")]
        [SerializeField] string roundTitleFormat = "Round {0} / {1}";

        [Tooltip("Title after the last round.")]
        [SerializeField] string finalTitle = "Final Results";

        [Tooltip("Tint each label with its player's color.")]
        [SerializeField] bool tintLabelsWithPlayerColor = true;

        /// <summary>Raised when the Next Round button is pressed.</summary>
        public event Action NextRoundRequested;

        void Awake()
        {
            if (nextRoundButton != null) nextRoundButton.onClick.AddListener(OnNextRoundClicked);
        }

        void OnDestroy()
        {
            if (nextRoundButton != null) nextRoundButton.onClick.RemoveListener(OnNextRoundClicked);
        }

        /// <summary>
        /// Shows the scores after a round: every player's cumulative score, and this map's share below it. After the last
        /// round (<paramref name="round"/> &gt;= <paramref name="maxRounds"/>) it becomes the final results screen with the
        /// winner (or tie) and no Next Round button.
        /// </summary>
        public void Show(ScoreManager scores, int round, int maxRounds)
        {
            bool isFinal = round >= maxRounds;
            PlayersConfig players = scores.PlayersConfig;
            var leaders = isFinal ? scores.GetLeaders() : null;

            for (int i = 0; i < percentageLabels.Length; i++)
            {
                TMP_Text label = percentageLabels[i];
                if (label == null) continue;

                float cumulative = scores.GetCumulativePercent(i);
                float thisMap = scores.GetCurrentMapPercent(i);
                string tag = "";
                if (isFinal && leaders.Contains(i)) tag = leaders.Count == 1 ? winnerTag : tieTag;

                label.text = string.Format(CultureInfo.InvariantCulture, isFinal ? finalLabelFormat : labelFormat, i + 1, cumulative, thisMap, tag);

                if (tintLabelsWithPlayerColor && players != null && i < players.slots.Length)
                {
                    Color color = players.slots[i].color;
                    color.a = 1f;
                    label.color = color;
                }
            }

            if (titleLabel != null)
                titleLabel.text = isFinal ? finalTitle : string.Format(CultureInfo.InvariantCulture, roundTitleFormat, round, maxRounds);

            if (winnerLabel != null)
            {
                winnerLabel.gameObject.SetActive(isFinal);
                if (isFinal) winnerLabel.text = DescribeLeaders(leaders);
            }

            if (nextRoundButton != null) nextRoundButton.gameObject.SetActive(!isFinal);
            if (panel != null) panel.SetActive(true);

            // Lets keyboard / gamepad confirm the button too.
            if (!isFinal && nextRoundButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(nextRoundButton.gameObject);
        }

        string DescribeLeaders(System.Collections.Generic.List<int> leaders)
        {
            if (leaders.Count == 1) return string.Format(CultureInfo.InvariantCulture, winnerFormat, leaders[0] + 1);

            var numbers = new System.Collections.Generic.List<string>();
            foreach (int player in leaders) numbers.Add((player + 1).ToString(CultureInfo.InvariantCulture));
            return string.Format(CultureInfo.InvariantCulture, tieFormat, string.Join(" & ", numbers));
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        void OnNextRoundClicked() => NextRoundRequested?.Invoke();
    }
}
