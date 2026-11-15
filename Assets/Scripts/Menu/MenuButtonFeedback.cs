using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// Gives any menu button its sounds: one when the pointer moves over it or the keyboard / controller highlights it, and one
    /// when it is pressed. The sounds come from the <see cref="MenuAudioSet"/>; which slot a button uses can be changed here
    /// (the Back buttons use the "Back" sound). Put it on every button that should make sound - it is already on the button prefab.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Selectable))]
    public class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
    {
        // Selecting a button from code (e.g. when a screen opens) must not make the hover sound.
        static float muteSelectUntil;

        [SerializeField] MenuSound hoverSound = MenuSound.ButtonHover;
        [SerializeField] MenuSound clickSound = MenuSound.ButtonClick;

        Selectable selectable;
        Button button;
        bool pointerInside;

        /// <summary>Suppresses the hover sound of buttons that get selected from code during the next <paramref name="seconds"/>.</summary>
        public static void MuteSelectSound(float seconds) => muteSelectUntil = Time.unscaledTime + seconds;

        void OnEnable()
        {
            selectable = GetComponent<Selectable>();
            button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(PlayClick);
        }

        void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(PlayClick);
            pointerInside = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            if (selectable != null && selectable.IsInteractable()) MenuAudioPlayer.Play(hoverSound);
        }

        public void OnPointerExit(PointerEventData eventData) => pointerInside = false;

        public void OnSelect(BaseEventData eventData)
        {
            // A click selects the button as well; the pointer already made the hover sound, so only keyboard / controller selections count here.
            if (pointerInside || Time.unscaledTime < muteSelectUntil) return;
            if (selectable != null && selectable.IsInteractable()) MenuAudioPlayer.Play(hoverSound);
        }

        void PlayClick() => MenuAudioPlayer.Play(clickSound);
    }
}
