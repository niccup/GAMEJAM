using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// One screen of the menu (Main Menu, Authors, Character Selection): an object that is switched on while the screen is
    /// shown. The <see cref="MenuFlow"/> shows one screen at a time. The button that is highlighted first can be set here, so
    /// the screen can be played with a keyboard or controller from the first moment.
    /// </summary>
    public class MenuScreen : MonoBehaviour
    {
        [Tooltip("The button that is highlighted when the screen opens (for keyboard and controller). Leave empty for none.")]
        [SerializeField] Selectable firstSelected;

        [Header("Events (optional)")]
        public UnityEvent onShown;
        public UnityEvent onHidden;

        public Selectable FirstSelected => firstSelected;

        public bool IsShown => gameObject.activeSelf;

        public void Show()
        {
            gameObject.SetActive(true);
            onShown?.Invoke();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            onHidden?.Invoke();
        }
    }
}
