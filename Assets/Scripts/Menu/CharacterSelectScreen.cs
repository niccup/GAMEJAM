using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameJam
{
    /// <summary>
    /// The character selection: every player picks one of four characters, each with their own device. This component only
    /// connects the parts - the rules are in the <see cref="CharacterSelectModel"/>, the pictures in the
    /// <see cref="CharacterSlotView"/>s and <see cref="PlayerCursorView"/>s, the characters and their colors in the
    /// <see cref="CharacterRoster"/> asset and the controls in the Input Actions asset - so replacing art, colors or
    /// bindings never touches the flow.
    /// <para>
    /// Each player gets their own <see cref="PlayerInput"/> (their control scheme and device come from the Players Config,
    /// like in the game, including the keyboard test mode), moves left / right with "Navigate" and takes a character with
    /// "Confirm". A taken character is greyed out and nobody else can take it; "Cancel" gives it back. Once every
    /// player has confirmed, the choices are stored in the <see cref="GameSession"/> and <see cref="AllPlayersReady"/> is
    /// raised, which makes the menu load the game.
    /// </para>
    /// </summary>
    [ExecuteAlways]
    public class CharacterSelectScreen : MonoBehaviour
    {
        sealed class Seat
        {
            public int Index;
            public string Scheme;
            public PlayerInput Input;
            public MenuInput Menu;
            public InputDevice[] Devices;
            public float ConfirmLockedUntil;
        }

        [Header("Data")]
        [Tooltip("The four characters: their pictures, names and the player color each one gives.")]
        [SerializeField] CharacterRoster roster;

        [Tooltip("Which control scheme each player uses, and whether the keyboard test mode is on (then only its first players take part).")]
        [SerializeField] PlayersConfig playersConfig;

        [Header("Parts on the screen")]
        [Tooltip("The four character slots, left to right (slot 1 shows the first character of the roster).")]
        [SerializeField] CharacterSlotView[] slots = new CharacterSlotView[CharacterRoster.CharacterCount];

        [Tooltip("The four player indicators P1 - P4.")]
        [SerializeField] PlayerCursorView[] cursors = new PlayerCursorView[PlayersConfig.MaxPlayers];

        [Tooltip("Optional. Shows how many players are ready, or that a controller is missing.")]
        [SerializeField] TMP_Text statusLabel;

        [Header("Input")]
        [Tooltip("Prefab with a PlayerInput that uses the Input Actions asset. One copy is created per player when the screen opens.")]
        [SerializeField] PlayerInput menuInputPrefab;

        [Tooltip("The action map in the Input Actions asset that holds the three actions below.")]
        [SerializeField] string actionMapName = "Menu";

        [Tooltip("Vector2 action: left / right moves to the previous / next character.")]
        [SerializeField] string navigateActionName = "Navigate";

        [Tooltip("Button action: takes the character the player is on.")]
        [SerializeField] string confirmActionName = "Confirm";

        [Tooltip("Button action: gives back the confirmed character. Leave empty if players must not change their mind.")]
        [SerializeField] string cancelActionName = "Cancel";

        [Tooltip("Input is ignored for this long after the screen opens, so the press that opened it cannot pick a character.")]
        [SerializeField, Min(0f)] float inputDelay = 0.35f;

        [Header("Behaviour")]
        [Tooltip("Moving past the last character continues at the first one.")]
        [SerializeField] bool wrapAround = true;

        [Tooltip("A player who lost the character they were looking at to someone else cannot confirm for this long, so a press meant for the taken character does not land on another one.")]
        [SerializeField, Min(0f)] float confirmLockAfterMove = 0.3f;

        [Tooltip("Pause after the last player confirmed, before the game starts. Anyone can still cancel during it.")]
        [SerializeField, Min(0f)] float startDelay = 1.2f;

        [Header("Layout")]
        [Tooltip("Horizontal gap between the indicators of different players on the same character (canvas units).")]
        [SerializeField, Min(0f)] float cursorSpacing = 90f;

        [Header("Texts")]
        [Tooltip("{0} = players ready, {1} = players taking part.")]
        [SerializeField] string statusFormat = "Players ready: {0} / {1}";

        [SerializeField] string allReadyText = "All players ready!";

        [Tooltip("{0} = the players whose controller is missing, e.g. 'P3, P4'.")]
        [SerializeField] string waitingForControllerFormat = "Waiting for a controller: {0}";

        readonly List<Seat> seats = new List<Seat>();
        readonly HashSet<InputDevice> claimedDevices = new HashSet<InputDevice>();
        CharacterSelectModel model;
        bool isOpen;
        bool committed;
        bool pairingDirty;
        float readyTimer;
        string lastStatus;

        /// <summary>Raised once, when every player has confirmed and the choices are stored in the <see cref="GameSession"/>.</summary>
        public event Action AllPlayersReady;

        /// <summary>True from <see cref="Begin"/> until <see cref="End"/>.</summary>
        public bool IsOpen => isOpen;

        /// <summary>True once the choices were stored and the game is about to start.</summary>
        public bool IsCommitted => committed;

        // ---- Opening and closing ------------------------------------------------------------------------------

        /// <summary>Starts a fresh selection: nothing is taken, each player gets their input and their indicator.</summary>
        public void Begin()
        {
            End();

            if (roster == null || slots == null || slots.Length == 0 || cursors == null)
            {
                Debug.LogError("Character selection is not set up: it needs a Character Roster, the character slots and the player indicators.", this);
                return;
            }

            int characters = Mathf.Min(roster.Count, slots.Length);
            int active = playersConfig != null ? playersConfig.ActivePlayerCount : PlayersConfig.MaxPlayers;
            active = Mathf.Clamp(active, 1, PlayersConfig.MaxPlayers);
            if (characters < active)
                Debug.LogError($"{active} players take part but there are only {characters} characters: some player could never pick one. Check the Character Roster.", this);
            if (roster.HasDuplicateColors(out PlayerColorOption duplicate))
                Debug.LogWarning($"Two characters share the color {duplicate}; their players would look the same in the game. Change one in the Character Roster.", this);
            if (playersConfig == null)
                Debug.LogWarning("The character selection has no Players Config assigned, so it assumes one keyboard player and three gamepad players.", this);

            GameSession.Clear();
            model = new CharacterSelectModel(characters, PlayersConfig.MaxPlayers, active, wrapAround);
            committed = false;
            readyTimer = 0f;
            lastStatus = null;

            for (int i = 0; i < active; i++) seats.Add(new Seat { Index = i, Scheme = SchemeFor(i) });
            PairSeats();
            InputSystem.onDeviceChange += OnDeviceChange;

            BindSlots();
            foreach (CharacterSlotView slot in slots)
                if (slot != null) slot.ResetState();
            foreach (PlayerCursorView cursor in cursors)
                if (cursor != null) cursor.ResetView();

            Canvas.ForceUpdateCanvases();
            isOpen = true;
            RefreshViews(snapCursors: true);
        }

        /// <summary>Closes the selection: the players' inputs are removed and their devices are free again.</summary>
        public void End()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            foreach (Seat seat in seats)
                if (seat.Input != null) Destroy(seat.Input.gameObject);

            seats.Clear();
            claimedDevices.Clear();
            isOpen = false;
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                CharacterRoster.EditorChanged += RefreshPreview;
                RefreshPreview();
            }
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            CharacterRoster.EditorChanged -= RefreshPreview;
#endif
            End();
        }

        // ---- Players and their inputs -------------------------------------------------------------------------

        string SchemeFor(int index)
        {
            if (playersConfig != null) return playersConfig.GetControlScheme(index);
            return index == 0 ? "Keyboard" : "Gamepad";
        }

        // Gives every player that has no input yet one, as far as their devices are connected. Called when the screen opens
        // and whenever a device is plugged in, so a controller connected during the selection joins in.
        void PairSeats()
        {
            foreach (Seat seat in seats)
                if (seat.Menu == null) TryCreateInput(seat);
        }

        bool TryCreateInput(Seat seat)
        {
            if (menuInputPrefab == null)
            {
                Debug.LogError("The character selection has no Menu Input Prefab assigned.", this);
                return false;
            }

            InputActionAsset actions = menuInputPrefab.actions;
            InputControlScheme? scheme = actions != null ? actions.FindControlScheme(seat.Scheme) : null;
            if (scheme == null)
            {
                Debug.LogWarning($"Control scheme '{seat.Scheme}' of player {seat.Index + 1} does not exist in the Input Actions asset.", this);
                return false;
            }

            // The keyboard test mode lets two players share the keyboard, each with their own control scheme.
            bool share = playersConfig != null && playersConfig.keyboardTestMode;
            InputDevice[] devices = PlayerDevices.Claim(scheme.Value, claimedDevices, share, out string missing);
            if (missing != null)
            {
                // Not complete yet: hand the devices back and wait until the missing one is connected.
                if (!share)
                    foreach (InputDevice device in devices) claimedDevices.Remove(device);
                return false;
            }

            PlayerInput input = PlayerInput.Instantiate(menuInputPrefab.gameObject, seat.Index, seat.Scheme, -1, devices);
            if (input == null) return false;
            input.neverAutoSwitchControlSchemes = true; // a player stays on the device they started with
            input.gameObject.name = $"Menu Input P{seat.Index + 1}";

            MenuInput menu = MenuInput.Create(input, actionMapName, navigateActionName, confirmActionName, cancelActionName, inputDelay, out string problem);
            if (menu == null)
            {
                Debug.LogError(problem, this);
                Destroy(input.gameObject);
                if (!share)
                    foreach (InputDevice device in devices) claimedDevices.Remove(device);
                return false;
            }

            seat.Input = input;
            seat.Menu = menu;
            seat.Devices = devices;
            return true;
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected) pairingDirty = true;
        }

        // ---- Every frame --------------------------------------------------------------------------------------

        void Update()
        {
            if (!Application.isPlaying || !isOpen || committed) return;

            if (pairingDirty)
            {
                pairingDirty = false;
                PairSeats();
            }

            KeepUiSelectionClear();

            float now = Time.unscaledTime;
            bool moveSound = false, confirmSound = false, cancelSound = false, unavailableSound = false;

            // The inputs of all players are applied one after another, in player order. A claim is atomic, so when two players
            // confirm the same character in the same frame the first one gets it and the other one is refused.
            foreach (Seat seat in seats)
            {
                if (seat.Menu == null) continue;

                MenuInput.Frame input = seat.Menu.Poll();
                int player = seat.Index;

                if (input.Confirm)
                {
                    if (model.HasConfirmed(player)) continue;

                    int character = model.GetCursor(player);
                    if (now < seat.ConfirmLockedUntil)
                    {
                        unavailableSound = true; // this press was meant for the character that was just taken
                    }
                    else
                    {
                        ConfirmStatus status = model.TryConfirm(player);
                        if (status == ConfirmStatus.Confirmed)
                        {
                            confirmSound = true;
                        }
                        else if (status == ConfirmStatus.Unavailable)
                        {
                            unavailableSound = true;
                            if (character >= 0 && character < slots.Length && slots[character] != null) slots[character].Shake();
                        }
                    }
                }
                else if (input.Cancel)
                {
                    if (!string.IsNullOrEmpty(cancelActionName) && model.TryCancel(player)) cancelSound = true;
                }
                else if (input.Step != 0)
                {
                    if (model.Move(player, input.Step)) moveSound = true;
                }
            }

            // Whoever is still looking at a character somebody just took is moved to another one.
            IReadOnlyList<int> moved = model.ResolveCursors();
            for (int i = 0; i < moved.Count; i++)
            {
                seats[moved[i]].ConfirmLockedUntil = now + confirmLockAfterMove;
                unavailableSound = true;
            }

            if (unavailableSound) MenuAudioPlayer.Play(MenuSound.CharacterUnavailable);
            if (confirmSound) MenuAudioPlayer.Play(model.AllConfirmed ? MenuSound.AllPlayersReady : MenuSound.CharacterConfirm);
            else if (cancelSound) MenuAudioPlayer.Play(MenuSound.CharacterCancel);
            else if (moveSound) MenuAudioPlayer.Play(MenuSound.CharacterMove);

            RefreshViews(snapCursors: false);
            UpdateStartCountdown();
        }

        void UpdateStartCountdown()
        {
            if (!model.AllConfirmed)
            {
                readyTimer = 0f;
                return;
            }

            readyTimer += Time.unscaledDeltaTime;
            if (readyTimer >= startDelay) Commit();
        }

        // Stores what every player chose and tells the menu to start the game.
        void Commit()
        {
            committed = true;
            GameSession.Clear();
            for (int player = 0; player < model.ActivePlayerCount; player++)
            {
                int character = model.GetChoice(player);
                GameSession.Set(new PlayerSelection(player, character, roster.GetDisplayName(character), roster.GetColor(character), seats[player].Devices));
            }

            RefreshViews(snapCursors: false);
            AllPlayersReady?.Invoke();
        }

        // Nothing on this screen may be "clicked" by the controller's confirm button, so no UI object stays selected.
        static void KeepUiSelectionClear()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null) eventSystem.SetSelectedGameObject(null);
        }

        // ---- Showing the state --------------------------------------------------------------------------------

        void BindSlots()
        {
            if (roster == null || slots == null) return;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].Bind(roster.Get(i), roster.GetColor(i), roster.GetDisplayName(i));
        }

        void RefreshViews(bool snapCursors)
        {
            if (model == null) return;

            for (int c = 0; c < slots.Length; c++)
            {
                if (slots[c] == null) continue;

                int owner = model.GetOwner(c);
                bool hovered = false;
                for (int p = 0; p < model.ActivePlayerCount; p++)
                    if (!model.HasConfirmed(p) && model.GetCursor(p) == c) hovered = true;
                slots[c].SetState(owner != CharacterSelectModel.None, owner, hovered);
            }

            for (int p = 0; p < cursors.Length; p++)
            {
                PlayerCursorView cursor = cursors[p];
                if (cursor == null) continue;

                if (p >= model.ActivePlayerCount)
                {
                    cursor.SetState(CursorState.Hidden, Color.white);
                    continue;
                }

                bool ready = model.HasConfirmed(p);
                int character = ready ? model.GetChoice(p) : model.GetCursor(p);
                bool hasInput = p < seats.Count && seats[p].Menu != null;

                CursorState state = !hasInput ? CursorState.WaitingForController : ready ? CursorState.Ready : CursorState.Choosing;
                cursor.SetState(state, ready ? roster.GetColor(character) : Color.white);
                cursor.MoveTo(CursorPosition(p, character), snapCursors);
            }

            if (statusLabel != null)
            {
                string status = BuildStatusText();
                if (status != lastStatus)
                {
                    lastStatus = status;
                    statusLabel.text = status;
                }
            }
        }

        // The arrow tip of a player's indicator: at the cursor anchor of the character, shifted sideways by the player's number
        // so that several players on the same character do not cover each other.
        Vector3 CursorPosition(int player, int character)
        {
            CharacterSlotView slot = slots[Mathf.Clamp(character, 0, slots.Length - 1)];
            float offset = (player - (PlayersConfig.MaxPlayers - 1) * 0.5f) * cursorSpacing;
            return slot.CursorAnchor.position + transform.TransformVector(new Vector3(offset, 0f, 0f));
        }

        string BuildStatusText()
        {
            if (committed || model.AllConfirmed) return allReadyText;

            string waiting = null;
            foreach (Seat seat in seats)
            {
                if (seat.Menu != null) continue;
                string label = $"P{seat.Index + 1}";
                waiting = waiting == null ? label : waiting + ", " + label;
            }
            if (waiting != null) return SafeFormat(waitingForControllerFormat, waiting);

            return SafeFormat(statusFormat, model.ConfirmedCount, model.ActivePlayerCount);
        }

        // A typo in a text format must not stop the screen from working.
        static string SafeFormat(string format, params object[] args)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

#if UNITY_EDITOR
        // Edit mode: shows the roster's sprites, names and colors on the slots, so artists see their changes without pressing Play.
        // It only sets sprites, colors and texts - nothing that needs to wait until the Inspector is done.
        void RefreshPreview()
        {
            if (this == null || Application.isPlaying) return;
            BindSlots();
        }

        void OnValidate() => RefreshPreview();
#endif
    }
}
