using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>What one entry of the options row shows.</summary>
    public struct OptionData
    {
        public Sprite Icon;
        public string Title;
        public string Detail;

        /// <summary>False = greyed out; the step skips it when cycling.</summary>
        public bool Enabled;
    }

    /// <summary>One card / chip of the options row on the <see cref="PowerUpOverlay"/>. Pure view.</summary>
    public class PowerUpOptionView : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text detailLabel;

        [Header("Look")]
        [SerializeField] Color normalColor = new Color(0.14f, 0.16f, 0.22f, 0.92f);
        [SerializeField] Color selectedColor = new Color(0.2f, 0.5f, 0.75f, 0.98f);
        [SerializeField] Color disabledColor = new Color(0.1f, 0.1f, 0.12f, 0.6f);

        bool optionEnabled = true;
        Vector2 titlePosition;
        bool titlePositionKnown;

        public void Set(OptionData data)
        {
            optionEnabled = data.Enabled;

            // Without an icon the title takes the middle of the card.
            if (titleLabel != null)
            {
                if (!titlePositionKnown)
                {
                    titlePosition = titleLabel.rectTransform.anchoredPosition;
                    titlePositionKnown = true;
                }
                titleLabel.rectTransform.anchoredPosition = data.Icon != null ? titlePosition : titlePosition + Vector2.up * 60f;
                titleLabel.text = data.Title;
                titleLabel.color = optionEnabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }

            if (detailLabel != null)
            {
                detailLabel.text = data.Detail;
                detailLabel.color = optionEnabled ? new Color(0.85f, 0.88f, 0.95f) : new Color(1f, 1f, 1f, 0.35f);
            }

            if (iconImage != null)
            {
                iconImage.sprite = data.Icon;
                iconImage.enabled = data.Icon != null;
                iconImage.color = optionEnabled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        public void SetSelected(bool selected)
        {
            if (background != null) background.color = !optionEnabled ? disabledColor : selected ? selectedColor : normalColor;
        }
    }
}
