using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam
{
    /// <summary>
    /// Turns one player's own input into menu commands, using the actions of the Input Actions asset: a Vector2 action for
    /// moving left / right ("Navigate"), a button for "Confirm" and an optional button for "Cancel". Nothing here is tied to
    /// a device - the bindings (stick, d-pad, WASD, arrows, buttons) are whatever the asset says, per control scheme.
    /// A direction counts once per push (the stick has to return to neutral first), and nothing counts for a short moment
    /// after creation, so the button press that opened the screen cannot trigger anything.
    /// </summary>
    public sealed class MenuInput
    {
        /// <summary>What the player asked for this frame.</summary>
        public readonly struct Frame
        {
            public Frame(int step, bool confirm, bool cancel)
            {
                Step = step;
                Confirm = confirm;
                Cancel = cancel;
            }

            /// <summary>-1 = one step left, 1 = one step right, 0 = none.</summary>
            public int Step { get; }

            public bool Confirm { get; }
            public bool Cancel { get; }
        }

        const float PressThreshold = 0.6f;
        const float ReleaseThreshold = 0.3f;

        readonly InputAction navigate;
        readonly InputAction confirm;
        readonly InputAction cancel;
        readonly float armedAt;
        int held;

        MenuInput(InputAction navigate, InputAction confirm, InputAction cancel, float delay)
        {
            this.navigate = navigate;
            this.confirm = confirm;
            this.cancel = cancel;
            armedAt = Time.realtimeSinceStartup + delay;

            // A direction that is already held when the screen opens must not count as a press.
            held = Held(navigate.ReadValue<Vector2>().x);
        }

        /// <summary>
        /// Looks the actions up on the player's input and switches it to the given action map. Returns null, and says why in
        /// <paramref name="problem"/>, if a required action ("Navigate" or "Confirm") does not exist. "Cancel" is optional.
        /// </summary>
        public static MenuInput Create(PlayerInput playerInput, string mapName, string navigateName, string confirmName, string cancelName,
            float delay, out string problem)
        {
            problem = null;
            InputActionAsset actions = playerInput != null ? playerInput.actions : null;
            if (actions == null)
            {
                problem = "The menu player input has no Input Actions asset.";
                return null;
            }

            // Make sure the map the actions live in is the enabled one (the prefab's default map is "Menu").
            if (!string.IsNullOrEmpty(mapName) && actions.FindActionMap(mapName) != null) playerInput.SwitchCurrentActionMap(mapName);

            InputAction navigate = actions.FindAction(navigateName);
            InputAction confirm = actions.FindAction(confirmName);
            InputAction cancel = string.IsNullOrEmpty(cancelName) ? null : actions.FindAction(cancelName);
            if (navigate == null || confirm == null)
            {
                problem = $"The Input Actions asset needs the actions '{navigateName}' and '{confirmName}' (map '{mapName}') for the character selection.";
                return null;
            }

            return new MenuInput(navigate, confirm, cancel, delay);
        }

        /// <summary>Call once per frame.</summary>
        public Frame Poll()
        {
            int step = Track(ref held, navigate.ReadValue<Vector2>().x);
            bool confirmed = confirm.WasPressedThisFrame();
            bool cancelled = cancel != null && cancel.WasPressedThisFrame();

            if (Time.realtimeSinceStartup < armedAt) return default;
            return new Frame(step, confirmed, cancelled);
        }

        static int Held(float value) => value > PressThreshold ? 1 : value < -PressThreshold ? -1 : 0;

        // Returns the direction only on the frame it starts; releasing needs the stick back near neutral.
        static int Track(ref int state, float value)
        {
            if (state != 0 && Mathf.Abs(value) < ReleaseThreshold) state = 0;

            int now = Held(value);
            if (now != 0 && now != state)
            {
                state = now;
                return now;
            }
            return 0;
        }
    }
}
