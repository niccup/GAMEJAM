using System;
using UnityEngine;

namespace GameJam
{
    /// <summary>Every sound the menus can play. Each one has its own slot in the <see cref="MenuAudioSet"/>.</summary>
    public enum MenuSound
    {
        ButtonHover,
        ButtonClick,
        Back,
        ScreenTransition,
        CharacterMove,
        CharacterConfirm,
        CharacterCancel,
        CharacterUnavailable,
        AllPlayersReady,
        GameStart,
    }

    /// <summary>One sound slot: a clip and how loud it is played. An empty clip simply stays silent.</summary>
    [Serializable]
    public class MenuSoundEntry
    {
        [Tooltip("The sound. Leave it empty for silence - nothing in the menu depends on it.")]
        public AudioClip clip;

        [Range(0f, 1f), Tooltip("Loudness of this sound, so every clip can be balanced on its own.")]
        public float volume = 1f;
    }

    /// <summary>
    /// All the audio of the main menu and the character selection in one asset: drop the sound effects and the music into the
    /// slots below. Every slot may stay empty. Used by the <see cref="MenuAudioPlayer"/> of the menu scene.
    /// </summary>
    [CreateAssetMenu(menuName = "GameJam/Menu Audio Set", fileName = "MenuAudioSet")]
    public class MenuAudioSet : ScriptableObject
    {
        [Header("Main menu buttons")]
        [Tooltip("The pointer moves over a button, or the keyboard / controller moves the highlight onto it.")]
        public MenuSoundEntry buttonHover = new MenuSoundEntry();

        [Tooltip("A button is pressed (Start Game, Options, Authors).")]
        public MenuSoundEntry buttonClick = new MenuSoundEntry();

        [Tooltip("A Back button is pressed, or Escape is used to go back.")]
        public MenuSoundEntry back = new MenuSoundEntry();

        [Header("Screens")]
        [Tooltip("The menu changes from one screen to another (Main Menu, Authors, Character Selection).")]
        public MenuSoundEntry screenTransition = new MenuSoundEntry();

        [Header("Character selection")]
        [Tooltip("A player moves to another character.")]
        public MenuSoundEntry characterMove = new MenuSoundEntry();

        [Tooltip("A player confirms a character.")]
        public MenuSoundEntry characterConfirm = new MenuSoundEntry();

        [Tooltip("A player takes their confirmed character back.")]
        public MenuSoundEntry characterCancel = new MenuSoundEntry();

        [Tooltip("A player tried to take a character that is already taken, or the character they were looking at was just taken by someone else.")]
        public MenuSoundEntry characterUnavailable = new MenuSoundEntry();

        [Tooltip("The last player confirmed: everybody is ready.")]
        public MenuSoundEntry allPlayersReady = new MenuSoundEntry();

        [Tooltip("The menu hands over to the game.")]
        public MenuSoundEntry gameStart = new MenuSoundEntry();

        [Header("Music")]
        [Tooltip("Background music of the menu. It loops and fades out when the game starts. Leave empty for no music.")]
        public AudioClip menuMusic;

        [Range(0f, 1f)] public float musicVolume = 0.5f;

        [Header("Master")]
        [Range(0f, 1f), Tooltip("Scales every sound effect above.")]
        public float effectsVolume = 1f;

        public MenuSoundEntry Get(MenuSound sound)
        {
            switch (sound)
            {
                case MenuSound.ButtonHover: return buttonHover;
                case MenuSound.ButtonClick: return buttonClick;
                case MenuSound.Back: return back;
                case MenuSound.ScreenTransition: return screenTransition;
                case MenuSound.CharacterMove: return characterMove;
                case MenuSound.CharacterConfirm: return characterConfirm;
                case MenuSound.CharacterCancel: return characterCancel;
                case MenuSound.CharacterUnavailable: return characterUnavailable;
                case MenuSound.AllPlayersReady: return allPlayersReady;
                case MenuSound.GameStart: return gameStart;
                default: return null;
            }
        }
    }
}
