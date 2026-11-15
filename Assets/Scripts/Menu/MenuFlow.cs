using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// The flow of the menu scene: Main Menu -> Character Selection -> the game, plus the Authors screen. It knows the three
    /// screens and the buttons that move between them, fades between screens and loads the game scene once every player
    /// has chosen a character. It never touches artwork: sprites, fonts, positions and sounds can all be replaced without
    /// breaking it, as long as the buttons and screens below stay assigned.
    /// <para>
    /// Only one screen is shown at a time, whichever ones are switched on in the editor. Escape (and the controller's Select
    /// button) goes back to the Main Menu from the other screens.
    /// </para>
    /// </summary>
    public class MenuFlow : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] MenuScreen mainMenuScreen;
        [SerializeField] MenuScreen authorsScreen;
        [SerializeField] MenuScreen characterSelectScreen;

        [Tooltip("The character selection on the Character Selection screen. It is started when that screen opens.")]
        [SerializeField] CharacterSelectScreen characterSelect;

        [Header("Buttons")]
        [SerializeField] Button startGameButton;
        [SerializeField] Button optionsButton;
        [SerializeField] Button authorsButton;
        [SerializeField] Button authorsBackButton;
        [SerializeField] Button characterSelectBackButton;

        [Header("Options (not implemented yet)")]
        [Tooltip("Called when the Options button is pressed. Empty on purpose: the button is visible but does nothing for now.")]
        [SerializeField] UnityEvent onOptionsPressed;

        [Header("Game")]
        [Tooltip("Name of the scene that is loaded once every player has chosen a character. It has to be in the Build Settings.")]
        [SerializeField] string gameSceneName = "Main";

        [Header("Transitions")]
        [Tooltip("A full-screen black image with a Canvas Group that fades between screens. Leave empty for instant changes.")]
        [SerializeField] CanvasGroup fader;

        [SerializeField, Min(0f)] float fadeSeconds = 0.25f;

        [Header("Input")]
        [Tooltip("Goes back to the Main Menu from the Authors and the Character Selection screen: Escape, or the Select / Back button of a controller. Add or change bindings here.")]
        [SerializeField] InputAction backAction = CreateBackAction();

        MenuScreen current;
        bool transitioning;
        bool starting;

        static InputAction CreateBackAction()
        {
            var action = new InputAction("Back", InputActionType.Button);
            action.AddBinding("<Keyboard>/escape");
            action.AddBinding("<Gamepad>/select");
            return action;
        }

        void Awake()
        {
            // Whatever was left switched on in the editor, play always starts on the Main Menu.
            foreach (MenuScreen screen in new[] { mainMenuScreen, authorsScreen, characterSelectScreen })
                if (screen != null) screen.gameObject.SetActive(screen == mainMenuScreen);
            current = mainMenuScreen;

            if (fader != null)
            {
                fader.alpha = 1f;
                fader.blocksRaycasts = true;
            }
        }

        void OnEnable()
        {
            Subscribe(startGameButton, ShowCharacterSelection);
            Subscribe(optionsButton, OnOptionsPressed);
            Subscribe(authorsButton, ShowAuthors);
            Subscribe(authorsBackButton, ShowMainMenu);
            Subscribe(characterSelectBackButton, ShowMainMenu);
            if (characterSelect != null) characterSelect.AllPlayersReady += OnAllPlayersReady;
            backAction.Enable();
        }

        void OnDisable()
        {
            Unsubscribe(startGameButton, ShowCharacterSelection);
            Unsubscribe(optionsButton, OnOptionsPressed);
            Unsubscribe(authorsButton, ShowAuthors);
            Unsubscribe(authorsBackButton, ShowMainMenu);
            Unsubscribe(characterSelectBackButton, ShowMainMenu);
            if (characterSelect != null) characterSelect.AllPlayersReady -= OnAllPlayersReady;
            backAction.Disable();
        }

        void OnDestroy() => backAction.Dispose();

        IEnumerator Start()
        {
            transitioning = true;
            yield return Fade(0f);
            SelectFirst(current);
            transitioning = false;
        }

        void Update()
        {
            if (transitioning || starting || current == null || current == mainMenuScreen) return;
            if (!backAction.WasPressedThisFrame()) return;

            MenuAudioPlayer.Play(MenuSound.Back);
            ShowMainMenu();
        }

        // ---- Screens ------------------------------------------------------------------------------------------

        public void ShowMainMenu() => GoTo(mainMenuScreen);
        public void ShowAuthors() => GoTo(authorsScreen);
        public void ShowCharacterSelection() => GoTo(characterSelectScreen);

        void GoTo(MenuScreen target)
        {
            if (target == null || transitioning || starting || target == current) return;
            StartCoroutine(Transition(target));
        }

        IEnumerator Transition(MenuScreen target)
        {
            transitioning = true;
            MenuAudioPlayer.Play(MenuSound.ScreenTransition);
            ClearSelection();
            yield return Fade(1f);

            if (current == characterSelectScreen && characterSelect != null) characterSelect.End();
            if (current != null) current.Hide();

            current = target;
            target.Show();
            if (target == characterSelectScreen && characterSelect != null) characterSelect.Begin();

            yield return Fade(0f);
            SelectFirst(target);
            transitioning = false;
        }

        // The Options screen does not exist yet: the button is there but does nothing.
        void OnOptionsPressed() => onOptionsPressed?.Invoke();

        // ---- Starting the game --------------------------------------------------------------------------------

        void OnAllPlayersReady()
        {
            if (!starting) StartCoroutine(StartGame());
        }

        IEnumerator StartGame()
        {
            starting = true;
            transitioning = true;
            MenuAudioPlayer.Play(MenuSound.GameStart);
            MenuAudioPlayer.FadeOutMusic(fadeSeconds + 0.1f);
            ClearSelection();
            yield return Fade(1f);

            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError($"The game scene '{gameSceneName}' cannot be loaded. Add it to File > Build Profiles > Scene List (or fix the name on the MenuFlow).", this);
                GameSession.Clear();
                if (characterSelect != null)
                {
                    characterSelect.End();
                    characterSelect.Begin();
                }
                yield return Fade(0f);
                starting = false;
                transitioning = false;
                yield break;
            }

            // The choices are already stored in the GameSession; the game scene reads them when it creates the players and starts the first round.
            SceneManager.LoadScene(gameSceneName);
        }

        // ---- Helpers ------------------------------------------------------------------------------------------

        IEnumerator Fade(float target)
        {
            if (fader == null) yield break;

            fader.blocksRaycasts = true; // no clicks while the screen changes
            float start = fader.alpha;
            if (fadeSeconds > 0f)
            {
                for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
                {
                    fader.alpha = Mathf.Lerp(start, target, elapsed / fadeSeconds);
                    yield return null;
                }
            }
            fader.alpha = target;
            fader.blocksRaycasts = target > 0.001f;
        }

        static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        static void SelectFirst(MenuScreen screen)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            Selectable first = screen != null ? screen.FirstSelected : null;
            if (first != null && first.gameObject.activeInHierarchy && first.IsInteractable())
            {
                MenuButtonFeedback.MuteSelectSound(0.25f); // opening a screen is not a hover
                eventSystem.SetSelectedGameObject(first.gameObject);
            }
            else
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        static void Subscribe(Button button, UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        static void Unsubscribe(Button button, UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }
    }
}
