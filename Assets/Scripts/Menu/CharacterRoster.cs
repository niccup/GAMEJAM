using System;
using UnityEngine;

namespace GameJam
{
    /// <summary>The four colors a player can have in the game. The actual RGB values are set in the <see cref="CharacterRoster"/>.</summary>
    public enum PlayerColorOption { Black, Red, White, Yellow }

    /// <summary>One playable character as it appears on the character selection screen.</summary>
    [Serializable]
    public class CharacterDefinition
    {
        [Tooltip("Optional. The name shown under the character. Leave it empty to show the name of its color (BLACK, RED, WHITE, YELLOW).")]
        public string displayName;

        [Tooltip("The character picture on the selection screen. Any sprite works, whatever its size or proportions.")]
        public Sprite portrait;

        [Tooltip("Optional. A hand-drawn greyed-out version of the portrait. When set, it is shown instead of the automatic grey-out " +
                 "while another player has taken this character.")]
        public Sprite unavailablePortrait;

        [Tooltip("The player color this character gives its player for the whole game: the character itself, its trail and the territory it captures. " +
                 "Pick another entry to change which character has which color. The actual RGB values are in the Palette above.")]
        public PlayerColorOption color;
    }

    /// <summary>
    /// Everything about the four selectable characters in one asset: their pictures, names and - most importantly - which
    /// player color each of them gives. Artists fill in the pictures; the color of a character is a dropdown, so changing
    /// who is Black, Red, White or Yellow never needs a script. The selection screen and the game read their colors from here.
    /// </summary>
    [CreateAssetMenu(menuName = "GameJam/Character Roster", fileName = "CharacterRoster")]
    public class CharacterRoster : ScriptableObject
    {
        /// <summary>The roster always has exactly one character per player.</summary>
        public const int CharacterCount = PlayersConfig.MaxPlayers;

        [Header("Palette: the exact RGB value of each player color (used for trails and territory too)")]
        public Color black = new Color(0.07f, 0.07f, 0.08f, 1f);
        public Color red = new Color(0.90f, 0.16f, 0.16f, 1f);
        public Color white = new Color(0.97f, 0.97f, 0.97f, 1f);
        public Color yellow = new Color(1.00f, 0.84f, 0.12f, 1f);

        [Header("Characters: shown left to right on the selection screen")]
        public CharacterDefinition[] characters = CreateDefaultCharacters();

        public int Count => characters != null ? characters.Length : 0;

        public CharacterDefinition Get(int index) =>
            characters != null && index >= 0 && index < characters.Length ? characters[index] : null;

        /// <summary>The actual color of one of the four player colors.</summary>
        public Color GetColor(PlayerColorOption option)
        {
            switch (option)
            {
                case PlayerColorOption.Black: return black;
                case PlayerColorOption.Red: return red;
                case PlayerColorOption.White: return white;
                default: return yellow;
            }
        }

        /// <summary>The player color the character at <paramref name="index"/> gives (white if there is no such character).</summary>
        public Color GetColor(int index)
        {
            CharacterDefinition definition = Get(index);
            return definition != null ? GetColor(definition.color) : Color.white;
        }

        /// <summary>The name to show for a character: its own name, or the name of its color if it has none.</summary>
        public string GetDisplayName(int index)
        {
            CharacterDefinition definition = Get(index);
            if (definition == null) return string.Empty;
            return string.IsNullOrWhiteSpace(definition.displayName) ? definition.color.ToString().ToUpperInvariant() : definition.displayName;
        }

        /// <summary>True if two characters share a color (their players could not be told apart in the game).</summary>
        public bool HasDuplicateColors(out PlayerColorOption duplicate)
        {
            duplicate = default;
            if (characters == null) return false;

            for (int i = 0; i < characters.Length; i++)
            for (int j = i + 1; j < characters.Length; j++)
            {
                if (characters[i] == null || characters[j] == null || characters[i].color != characters[j].color) continue;
                duplicate = characters[i].color;
                return true;
            }
            return false;
        }

        static CharacterDefinition[] CreateDefaultCharacters() => new[]
        {
            new CharacterDefinition { color = PlayerColorOption.Black },
            new CharacterDefinition { color = PlayerColorOption.Red },
            new CharacterDefinition { color = PlayerColorOption.White },
            new CharacterDefinition { color = PlayerColorOption.Yellow },
        };

#if UNITY_EDITOR
        /// <summary>Editor only: raised when the asset was edited, so open selection screens can refresh their preview.</summary>
        public static event Action EditorChanged;

        void OnValidate()
        {
            if (characters == null || characters.Length != CharacterCount)
            {
                var fixedSize = new CharacterDefinition[CharacterCount];
                for (int i = 0; i < fixedSize.Length; i++)
                    fixedSize[i] = characters != null && i < characters.Length && characters[i] != null ? characters[i] : new CharacterDefinition();
                characters = fixedSize;
            }

            if (HasDuplicateColors(out PlayerColorOption duplicate))
                Debug.LogWarning($"Character Roster: more than one character has the color {duplicate}. Their players could not be told apart in the game.", this);

            EditorChanged?.Invoke();
        }
#endif
    }
}
