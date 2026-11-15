using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GameJam
{
    /// <summary>How the options row is drawn: big cards in the middle of the screen, or small chips at the bottom (so the map stays visible).</summary>
    public enum OptionsLayout { Cards, Chips }

    /// <summary>
    /// The screen of the selection flow: a title bar, a hint bar, an optional dimmed background and a row of options
    /// (power-up cards, ghost chips ...). Pure view - the current <see cref="SelectionStep"/> decides what it shows.
    /// Build the hierarchy with Tools > GameJam > Setup Scene, then restyle it freely. Put this component on an object
    /// that stays active (e.g. the Canvas); <see cref="panel"/> is what gets shown/hidden. Option views are copies of
    /// <see cref="optionTemplate"/>.
    /// </summary>
    public class PowerUpOverlay : MonoBehaviour
    {
        [Tooltip("Root of the overlay content. Activated by Open, deactivated by Close.")]
        [SerializeField] GameObject panel;

        [Tooltip("Full-screen dark layer behind everything. Only shown while no map has to be visible.")]
        [SerializeField] GameObject dimmer;

        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text hintLabel;

        [Tooltip("Parent of the option views. Its position is set by the layout.")]
        [SerializeField] RectTransform optionsRoot;

        [Tooltip("Inactive prototype of one option (card or chip). Copied as often as needed.")]
        [SerializeField] PowerUpOptionView optionTemplate;

        [Header("Artwork")]
        [Tooltip("Shown on the card of a power-up that has no icon of its own (its Icon slot is empty). Replace it with any sprite.")]
        [SerializeField] Sprite placeholderIcon;

        [Header("Layout")]
        [Tooltip("The options row is shrunk (cards, chips and the gaps between them) until it fits within this share of the screen width, " +
                 "so any number of options stays on one screen at any resolution or aspect ratio. Never enlarged beyond normal size.")]
        [SerializeField, Range(0.5f, 1f)] float maxRowWidth = 0.94f;
        [SerializeField] float spacing = 40f;
        [Tooltip("Size of a chip relative to a card.")]
        [SerializeField, Range(0.2f, 1f)] float chipScale = 0.55f;
        [Tooltip("Position of the chips row: from the bottom edge of the screen to its center line.")]
        [SerializeField] float chipsHeight = 200f;

        readonly List<PowerUpOptionView> views = new List<PowerUpOptionView>();
        int shown;

        void Awake()
        {
            if (optionTemplate != null) optionTemplate.gameObject.SetActive(false);
            Close();
        }

        /// <summary>The picture for a power-up without artwork of its own (null if none is assigned).</summary>
        public Sprite PlaceholderIcon => placeholderIcon;

        public void Open()
        {
            if (panel != null) panel.SetActive(true);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            HideOptions();
        }

        public void SetDimmed(bool dimmed)
        {
            if (dimmer != null) dimmer.SetActive(dimmed);
        }

        public void SetHeader(string title, string hint, Color titleColor)
        {
            if (titleLabel != null)
            {
                titleLabel.text = title;
                PlayerColorText.Apply(titleLabel, titleColor);
            }
            if (hintLabel != null) hintLabel.text = hint;
        }

        public void ShowOptions(IReadOnlyList<OptionData> options, int selected, OptionsLayout layout)
        {
            if (optionTemplate == null || optionsRoot == null) return;

            bool chips = layout == OptionsLayout.Chips;
            float scale = chips ? chipScale : 1f;
            var templateRect = (RectTransform)optionTemplate.transform;

            // Shrink the whole row so it fits the screen, whatever its resolution or aspect ratio (canvas units).
            float screenWidth = panel != null ? ((RectTransform)panel.transform).rect.width : 0f;
            if (screenWidth <= 0f) screenWidth = ((RectTransform)transform).rect.width;
            if (screenWidth > 0f && options.Count > 0)
            {
                float rowAtFullScale = options.Count * templateRect.sizeDelta.x + (options.Count - 1) * spacing;
                scale = Mathf.Min(scale, screenWidth * maxRowWidth / rowAtFullScale);
            }

            float width = templateRect.sizeDelta.x * scale;
            float gap = spacing * scale;
            float total = options.Count * width + (options.Count - 1) * gap;

            optionsRoot.anchorMin = optionsRoot.anchorMax = optionsRoot.pivot = new Vector2(0.5f, chips ? 0f : 0.5f);
            optionsRoot.anchoredPosition = new Vector2(0f, chips ? chipsHeight : 0f);

            while (views.Count < options.Count) views.Add(Instantiate(optionTemplate, optionsRoot));

            for (int i = 0; i < views.Count; i++)
            {
                bool used = i < options.Count;
                views[i].gameObject.SetActive(used);
                if (!used) continue;

                var rect = (RectTransform)views[i].transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = templateRect.sizeDelta;
                rect.localScale = Vector3.one * scale;
                rect.anchoredPosition = new Vector2(-total * 0.5f + width * 0.5f + i * (width + gap), 0f);
                views[i].Set(options[i]);
            }

            shown = options.Count;
            SetSelected(selected);
        }

        public void SetSelected(int selected)
        {
            for (int i = 0; i < shown; i++) views[i].SetSelected(i == selected);
        }

        public void HideOptions()
        {
            foreach (PowerUpOptionView view in views) view.gameObject.SetActive(false);
            shown = 0;
        }
    }
}
