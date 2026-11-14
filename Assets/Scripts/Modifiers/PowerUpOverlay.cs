using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// The screen that offers a power-up to one player: the modifier's icon, name and a prompt naming that player. Pure view -
    /// <see cref="PowerUpSelection"/> decides when it is shown. Build the hierarchy with Tools > GameJam > Setup Scene,
    /// then restyle it freely. Put this component on an object that stays active (e.g. the Canvas); <see cref="panel"/>
    /// is what gets shown/hidden.
    /// </summary>
    public class PowerUpOverlay : MonoBehaviour
    {
        [Tooltip("Root of the overlay content. Activated by Show, deactivated by Hide.")]
        [SerializeField] GameObject panel;

        [SerializeField] Image iconImage;

        [Tooltip("Optional. The modifier's name.")]
        [SerializeField] TMP_Text nameLabel;

        [Tooltip("Optional. The modifier's description.")]
        [SerializeField] TMP_Text descriptionLabel;

        [Tooltip("Optional. Tells the players what to do.")]
        [SerializeField] TMP_Text promptLabel;

        [Tooltip("Prompt text. {0} = the modifier's name, {1} = the player number who may take it.")]
        [SerializeField] string promptFormat = "Player {1}: press ACTION to take it!";

        [Tooltip("Tint the prompt with the chooser's player color.")]
        [SerializeField] bool tintPromptWithPlayerColor = true;

        void Awake() => Hide();

        public void Show(PlayerModifier modifier, Player chooser)
        {
            if (iconImage != null)
            {
                iconImage.sprite = modifier.Icon;
                iconImage.enabled = modifier.Icon != null;
            }

            if (nameLabel != null) nameLabel.text = modifier.DisplayName;
            if (descriptionLabel != null) descriptionLabel.text = modifier.Description;
            if (promptLabel != null)
            {
                promptLabel.text = string.Format(CultureInfo.InvariantCulture, promptFormat, modifier.DisplayName, chooser.Index + 1);
                if (tintPromptWithPlayerColor)
                {
                    Color color = chooser.Color;
                    color.a = 1f;
                    promptLabel.color = color;
                }
            }
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
