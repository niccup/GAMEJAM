using System.Globalization;
using TMPro;
using UnityEngine;

namespace GameJam
{
    /// <summary>Shows the remaining round time in a text label (HUD). Optional - the round runs without it.</summary>
    public class RoundTimerDisplay : MonoBehaviour
    {
        [SerializeField] RoundManager round;
        [SerializeField] TMP_Text label;

        [Tooltip("Label text. {0} = minutes, {1} = seconds (two digits).")]
        [SerializeField] string format = "{0}:{1:00}";

        int shownSeconds = -1;

        void Update()
        {
            if (round == null || label == null) return;

            int seconds = Mathf.CeilToInt(round.TimeRemaining);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            label.text = string.Format(CultureInfo.InvariantCulture, format, seconds / 60, seconds % 60);
        }
    }
}
