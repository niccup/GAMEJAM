using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam
{
    /// <summary>What one player chose on the character selection screen.</summary>
    public readonly struct PlayerSelection
    {
        public PlayerSelection(int playerIndex, int characterIndex, string characterName, Color color, InputDevice[] devices)
        {
            PlayerIndex = playerIndex;
            CharacterIndex = characterIndex;
            CharacterName = characterName;
            Color = color;
            Devices = devices;
        }

        /// <summary>The player (0 = Player 1).</summary>
        public int PlayerIndex { get; }

        /// <summary>The character the player picked (its position in the <see cref="CharacterRoster"/>).</summary>
        public int CharacterIndex { get; }

        public string CharacterName { get; }

        /// <summary>The player's color for the whole game: the character, its trail and its territory.</summary>
        public Color Color { get; }

        /// <summary>The input devices the player used on the selection screen. The game gives the player the same ones.</summary>
        public InputDevice[] Devices { get; }
    }

    /// <summary>
    /// The hand-over from the menu to the game. The character selection stores each player's choice here and the game
    /// scene reads it when it creates the players (see <see cref="PlayersConfig.GetPlayerColor"/> and the
    /// <see cref="PlayerSpawner"/>). It is plain static data, so it survives the scene change without any object having to
    /// live on. When the game scene is opened directly, nothing is stored and the game uses the colors of the Players Config as before.
    /// </summary>
    public static class GameSession
    {
        static readonly PlayerSelection?[] selections = new PlayerSelection?[PlayersConfig.MaxPlayers];

        // Editor "Enter Play Mode" without a domain reload keeps statics alive, so start every play session empty.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearOnStartup() => Clear();

        /// <summary>True if at least one player has a stored selection.</summary>
        public static bool HasSelections
        {
            get
            {
                foreach (PlayerSelection? selection in selections)
                    if (selection.HasValue) return true;
                return false;
            }
        }

        public static void Clear() => Array.Clear(selections, 0, selections.Length);

        public static void Set(PlayerSelection selection)
        {
            if (selection.PlayerIndex < 0 || selection.PlayerIndex >= selections.Length) return;
            selections[selection.PlayerIndex] = selection;
        }

        public static bool TryGetSelection(int playerIndex, out PlayerSelection selection)
        {
            if (playerIndex >= 0 && playerIndex < selections.Length && selections[playerIndex].HasValue)
            {
                selection = selections[playerIndex].Value;
                return true;
            }

            selection = default;
            return false;
        }
    }
}
