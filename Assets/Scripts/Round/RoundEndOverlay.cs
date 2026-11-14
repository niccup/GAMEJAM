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

        [Tooltip("Next Round button. Hidden on the final results screen.")]
        [SerializeField] Button nextRoundButton;

        [Header("Look")]
        [Tooltip("Label text. {0} = player number (1-4), {1} = percentage of the whole map (0-100).")]
        [SerializeField] string labelFormat = "P{0}\n{1:0.0}%";

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
        /// Shows the percentages of the round that just ended. After the last round (<paramref name="round"/> &gt;=
        /// <paramref name="maxRounds"/>) it becomes the final results screen: no Next Round button.
        /// </summary>
        public void Show(TerritoryCoverage coverage, PlayersConfig players, int round, int maxRounds)
        {
            bool isFinal = round >= maxRounds;

            for (int i = 0; i < percentageLabels.Length; i++)
            {
                TMP_Text label = percentageLabels[i];
                if (label == null) continue;

                float percent = i < coverage.PlayerCount ? coverage.GetFraction(i) * 100f : 0f;
                label.text = string.Format(CultureInfo.InvariantCulture, labelFormat, i + 1, percent);

                if (tintLabelsWithPlayerColor && players != null && i < players.slots.Length)
                {
                    Color color = players.slots[i].color;
                    color.a = 1f;
                    label.color = color;
                }
            }

            if (titleLabel != null)
                titleLabel.text = isFinal ? finalTitle : string.Format(CultureInfo.InvariantCulture, roundTitleFormat, round, maxRounds);

            if (nextRoundButton != null) nextRoundButton.gameObject.SetActive(!isFinal);
            if (panel != null) panel.SetActive(true);

            // Lets keyboard / gamepad confirm the button too.
            if (!isFinal && nextRoundButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(nextRoundButton.gameObject);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        void OnNextRoundClicked() => NextRoundRequested?.Invoke();
    }
}
