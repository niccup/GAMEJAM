using System;
using UnityEngine;

namespace GameJam
{
    /// <summary>Per-player data. Slot index == player index == spawn point index.</summary>
    [Serializable]
    public class PlayerSlot
    {
        [Tooltip("Control scheme from the Input Actions asset on the player prefab's PlayerInput (e.g. Keyboard or Gamepad). Only one player should use Keyboard.")]
        public string controlScheme = "Keyboard";

        [Tooltip("Fallback color. In a normal game a player's color is the color of the character they picked on the character selection " +
                 "screen (see the Character Roster asset); this one is only used when the game scene is started directly, without the menu.")]
        public Color color = Color.white;

        [Tooltip("Optional. Overrides the sprite from the player prefab for this slot.")]
        public Sprite spriteOverride;
    }

    /// <summary>Everything the game needs to create the players. Swap the prefab, sprites or input here.</summary>
    [CreateAssetMenu(menuName = "GameJam/Players Config", fileName = "PlayersConfig")]
    public class PlayersConfig : ScriptableObject
    {
        public const int MaxPlayers = 4;

        [Tooltip("Prefab instantiated for every player. Replace it to change visuals/colliders.")]
        public Player playerPrefab;

        [Header("Movement")]
        [Tooltip("How all players move: top speed, acceleration, steering, drift, braking. Shared by every player and " +
                 "applied live, so you can tune the feel while the game is running.")]
        public MovementPhysics movement = new MovementPhysics();

        [Tooltip("One entry per player (up to 4).")]
        public PlayerSlot[] slots = new PlayerSlot[MaxPlayers];

        [Header("Local testing")]
        [Tooltip("Keyboard test mode: only the first players listed below are spawned, all on this one keyboard " +
                 "(by default Player 1 = WASD, Player 2 = arrow keys). The other players stay inactive. " +
                 "Also toggled by Tools > GameJam > Keyboard Test Mode. Takes effect on the next Play.")]
        public bool keyboardTestMode;

        [Tooltip("Control scheme per player in keyboard test mode (from the Input Actions asset). One entry per active player.")]
        public string[] keyboardTestSchemes = { "Keyboard", "KeyboardArrows" };

        /// <summary>How many players are spawned: every slot normally, only the keyboard players in test mode.</summary>
        public int ActivePlayerCount => Mathf.Min(
            keyboardTestMode ? keyboardTestSchemes.Length : slots.Length,
            Mathf.Min(slots.Length, MaxPlayers));

        /// <summary>Control scheme a player uses, taking the test mode into account.</summary>
        public string GetControlScheme(int index) =>
            keyboardTestMode ? keyboardTestSchemes[index] : slots[index].controlScheme;

        /// <summary>
        /// The color of a player for the whole game (character, trail and territory): the color of the character the player
        /// picked on the character selection screen or, if the game was started without it (e.g. the game scene was opened
        /// directly), the color of their slot.
        /// </summary>
        public Color GetPlayerColor(int index)
        {
            if (GameSession.TryGetSelection(index, out PlayerSelection selection)) return selection.Color;
            return slots != null && index >= 0 && index < slots.Length ? slots[index].color : Color.white;
        }
    }
}
