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

        [Header("Layout")]
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
                titleLabel.color = titleColor;
            }
            if (hintLabel != null) hintLabel.text = hint;
        }

        public void ShowOptions(IReadOnlyList<OptionData> options, int selected, OptionsLayout layout)
        {
            if (optionTemplate == null || optionsRoot == null) return;

            bool chips = layout == OptionsLayout.Chips;
            float scale = chips ? chipScale : 1f;
            var templateRect = (RectTransform)optionTemplate.transform;
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
