using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// The picture side of one character on the selection screen: the character sprite, its name, its player color, a
    /// highlight while someone hovers over it, and the "taken" look (greyed-out picture, selected frame, owner tag) once a
    /// player has confirmed it. Pure view - it never decides anything, it only shows what the screen tells it, so replacing a
    /// sprite or restyling a part can never break the selection. Every part is optional: leave a field empty to drop that
    /// part. Edit this slot as a prefab (Assets/Prefabs/Menu/CharacterSlot.prefab) to restyle all four at once.
    /// </summary>
    public class CharacterSlotView : MonoBehaviour
    {
        static readonly int GreyAmountId = Shader.PropertyToID("_GreyAmount");

        [Header("Parts (every part is optional)")]
        [Tooltip("The character picture. Its sprite comes from the Character Roster asset, not from here.")]
        [SerializeField] Image portrait;

        [Tooltip("Shows the player color this character gives (tinted with the color from the Character Roster).")]
        [SerializeField] Image colorSwatch;

        [Tooltip("The name under the character (from the Character Roster).")]
        [SerializeField] TMP_Text nameLabel;

        [Tooltip("Shown while a player's indicator is on this character.")]
        [SerializeField] GameObject hoverIndicator;

        [Tooltip("The 'selected character' marker: shown while a player has confirmed this character.")]
        [SerializeField] GameObject selectedIndicator;

        [Tooltip("Optional extra marker for a taken character, e.g. a lock or a TAKEN stamp.")]
        [SerializeField] GameObject takenIndicator;

        [Tooltip("The tag that shows which player took this character. Switched on while the character is taken.")]
        [SerializeField] GameObject ownerBadge;

        [Tooltip("The text of the owner tag, e.g. P2.")]
        [SerializeField] TMP_Text ownerLabel;

        [Tooltip("Where the player indicators (P1-P4 with their arrows) hover above this character. Move it to change their height and position.")]
        [SerializeField] RectTransform cursorAnchor;

        [Header("Unavailable look: a character that another player has taken")]
        [Tooltip("UI material that greys the picture out (shader 'GameJam/UI/Desaturate'). Edit that material to change how strong the grey-out is. " +
                 "Leave empty to only tint the picture with the Unavailable Color.")]
        [SerializeField] Material unavailableMaterial;

        [Tooltip("Tint of the picture while the character is available.")]
        [SerializeField] Color availableColor = Color.white;

        [Tooltip("Tint of the picture while the character is taken.")]
        [SerializeField] Color unavailableColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [SerializeField, Min(0f)] float greyTransitionSeconds = 0.15f;

        [Header("Motion")]
        [Tooltip("How much the slot grows while a player's indicator is on it.")]
        [SerializeField, Min(1f)] float hoverScale = 1.06f;

        [SerializeField, Min(0f)] float scaleSpeed = 14f;
        [SerializeField, Min(0.01f)] float shakeSeconds = 0.28f;
        [SerializeField, Min(0f)] float shakeDistance = 14f;

        CharacterDefinition definition;
        bool taken;
        int owner = -1;
        bool hovered;
        float grey;        // 0 = full color, 1 = fully greyed
        float targetGrey;
        float baseGreyAmount = 1f;
        Material greyInstance;
        Vector3 baseScale = Vector3.one;
        bool scaleKnown;
        float shakeTimeLeft;
        Vector2 shakeBase;

        RectTransform Rect => (RectTransform)transform;

        /// <summary>Where the player indicators hover above this slot (the slot itself if no anchor is set).</summary>
        public RectTransform CursorAnchor => cursorAnchor != null ? cursorAnchor : Rect;

        /// <summary>Fills the slot with a character: sprite, name and color swatch. Also used by the editor preview.</summary>
        public void Bind(CharacterDefinition character, Color color, string displayName)
        {
            definition = character;
            if (nameLabel != null) nameLabel.text = displayName;
            if (colorSwatch != null) colorSwatch.color = color;
            ApplyPortrait();
        }

        /// <summary>Shows the slot as free (not taken, nobody hovering). Called when the selection screen opens.</summary>
        public void ResetState()
        {
            taken = false;
            owner = -1;
            hovered = false;
            targetGrey = 0f;
            grey = 0f;
            shakeTimeLeft = 0f;
            ApplyPortrait();
            ApplyGrey();
            ApplyMarkers();
            if (scaleKnown) transform.localScale = baseScale;
        }

        /// <summary>Shows the slot's current state. Cheap to call every frame.</summary>
        public void SetState(bool isTaken, int ownerPlayer, bool isHovered)
        {
            if (taken != isTaken || owner != ownerPlayer)
            {
                taken = isTaken;
                owner = ownerPlayer;
                targetGrey = taken && !HasCustomUnavailableSprite ? 1f : 0f;
                ApplyPortrait();
                ApplyMarkers();
            }

            if (hovered != isHovered)
            {
                hovered = isHovered;
                ApplyMarkers();
            }
        }

        /// <summary>A short sideways shake: "this one is not available".</summary>
        public void Shake()
        {
            if (shakeTimeLeft <= 0f) shakeBase = Rect.anchoredPosition;
            shakeTimeLeft = shakeSeconds;
        }

        bool HasCustomUnavailableSprite => definition != null && definition.unavailablePortrait != null;

        void ApplyPortrait()
        {
            if (portrait == null) return;

            Sprite sprite = definition != null ? definition.portrait : null;
            if (taken && HasCustomUnavailableSprite) sprite = definition.unavailablePortrait;
            portrait.sprite = sprite;
            portrait.enabled = sprite != null; // an Image without a sprite would show as a white box
        }

        void ApplyMarkers()
        {
            if (hoverIndicator != null) hoverIndicator.SetActive(hovered && !taken);
            if (selectedIndicator != null) selectedIndicator.SetActive(taken);
            if (takenIndicator != null) takenIndicator.SetActive(taken);
            if (ownerBadge != null) ownerBadge.SetActive(taken);
            if (ownerLabel != null) ownerLabel.text = taken ? $"P{owner + 1}" : string.Empty;
        }

        void Awake() => RememberScale();

        void RememberScale()
        {
            if (scaleKnown) return;
            baseScale = transform.localScale;
            scaleKnown = true;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            RememberScale();
            float dt = Time.unscaledDeltaTime;

            if (!Mathf.Approximately(grey, targetGrey))
            {
                grey = greyTransitionSeconds <= 0f ? targetGrey : Mathf.MoveTowards(grey, targetGrey, dt / greyTransitionSeconds);
                ApplyGrey();
            }

            Vector3 wanted = baseScale * (hovered && !taken ? hoverScale : 1f);
            Vector3 scale = Vector3.Lerp(transform.localScale, wanted, 1f - Mathf.Exp(-scaleSpeed * dt));
            if ((scale - transform.localScale).sqrMagnitude > 1e-8f) transform.localScale = scale;

            if (shakeTimeLeft > 0f)
            {
                shakeTimeLeft -= dt;
                if (shakeTimeLeft <= 0f)
                {
                    Rect.anchoredPosition = shakeBase;
                }
                else
                {
                    float strength = shakeTimeLeft / shakeSeconds;
                    Rect.anchoredPosition = shakeBase + new Vector2(Mathf.Sin((shakeSeconds - shakeTimeLeft) * 70f) * shakeDistance * strength, 0f);
                }
            }
        }

        void ApplyGrey()
        {
            if (portrait == null) return;
            portrait.color = Color.Lerp(availableColor, unavailableColor, grey);

            if (unavailableMaterial == null || !Application.isPlaying) return;
            if (grey > 0.001f)
            {
                if (greyInstance == null)
                {
                    // One copy per slot, so every character can fade to grey on its own.
                    greyInstance = new Material(unavailableMaterial) { name = unavailableMaterial.name + " (slot)" };
                    if (unavailableMaterial.HasProperty(GreyAmountId)) baseGreyAmount = unavailableMaterial.GetFloat(GreyAmountId);
                }
                greyInstance.SetFloat(GreyAmountId, grey * baseGreyAmount);
                if (portrait.material != greyInstance) portrait.material = greyInstance;
            }
            else if (greyInstance != null && portrait.material == greyInstance)
            {
                portrait.material = null; // back to the default UI material
            }
        }

        void OnDestroy()
        {
            if (greyInstance != null) Destroy(greyInstance);
        }
    }
}
