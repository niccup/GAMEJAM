using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam
{
    /// <summary>What the chooser asked for this frame while navigating the selection screens.</summary>
    public enum NavCommand { None, Previous, Next, Confirm, Back }

    /// <summary>
    /// Turns one player's own input into menu commands, using only the actions the game already has: "Action" confirms,
    /// "Move" left / right goes to the previous / next entry and "Move" down goes back. Directions fire once per push
    /// (the stick has to return to neutral first), and nothing counts during a short delay after creation so the press
    /// that closed the previous screen cannot confirm anything here. Bindings come from the Input Actions asset.
    /// </summary>
    public sealed class SelectionInput
    {
        const float PressThreshold = 0.6f;
        const float ReleaseThreshold = 0.3f;

        readonly InputAction move;
        readonly InputAction action;
        readonly float armedAt;
        int horizontal;
        int vertical;

        SelectionInput(InputAction move, InputAction action, float delay)
        {
            this.move = move;
            this.action = action;
            armedAt = Time.realtimeSinceStartup + delay;

            // A direction that is already held when the screen opens must not count as a press.
            Vector2 value = move.ReadValue<Vector2>();
            horizontal = Held(value.x);
            vertical = Held(value.y);
        }

        /// <summary>Null if the player has no such actions.</summary>
        public static SelectionInput Create(Player player, string moveActionName, string confirmActionName, float delay)
        {
            InputActionAsset actions = player != null && player.PlayerInput != null ? player.PlayerInput.actions : null;
            InputAction move = actions?.FindAction(moveActionName);
            InputAction confirm = actions?.FindAction(confirmActionName);
            return move != null && confirm != null ? new SelectionInput(move, confirm, delay) : null;
        }

        /// <summary>Call once per frame. Returns the command issued this frame (the first one if several arrive).</summary>
        public NavCommand Poll()
        {
            Vector2 value = move.ReadValue<Vector2>();
            int h = Track(ref horizontal, value.x);
            int v = Track(ref vertical, value.y);
            bool pressed = action.WasPressedThisFrame();

            if (Time.realtimeSinceStartup < armedAt) return NavCommand.None;
            if (pressed) return NavCommand.Confirm;
            if (h < 0) return NavCommand.Previous;
            if (h > 0) return NavCommand.Next;
            if (v < 0) return NavCommand.Back;
            return NavCommand.None;
        }

        static int Held(float value) => value > PressThreshold ? 1 : value < -PressThreshold ? -1 : 0;

        // Returns the direction only on the frame it starts; releasing needs the stick back near neutral.
        static int Track(ref int state, float value)
        {
            if (state != 0 && Mathf.Abs(value) < ReleaseThreshold) state = 0;

            int held = Held(value);
            if (held != 0 && held != state)
            {
                state = held;
                return held;
            }
            return 0;
        }
    }
}
