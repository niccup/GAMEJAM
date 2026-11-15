using System.Collections;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Plays the sounds of the <see cref="MenuAudioSet"/>. Anything in the menu can call <see cref="Play"/>: it does nothing
    /// when there is no player in the scene, no set, or the slot is empty, so the menu never depends on audio assets.
    /// Put it on one object of the menu scene (it also needs an Audio Listener somewhere, normally on the camera).
    /// </summary>
    public class MenuAudioPlayer : MonoBehaviour
    {
        [Tooltip("The asset with all the sounds and the music. Edit the clips there.")]
        [SerializeField] MenuAudioSet audioSet;

        [Tooltip("Plays the sound effects. Created automatically if left empty.")]
        [SerializeField] AudioSource effectsSource;

        [Tooltip("Plays the music. Created automatically if left empty.")]
        [SerializeField] AudioSource musicSource;

        [SerializeField] bool playMusicOnStart = true;

        Coroutine musicFade;

        /// <summary>The player of the open menu scene (null if there is none).</summary>
        public static MenuAudioPlayer Instance { get; private set; }

        public MenuAudioSet AudioSet => audioSet;

        void Awake()
        {
            Instance = this;
            effectsSource = EnsureSource(effectsSource, loop: false);
            musicSource = EnsureSource(musicSource, loop: true);
        }

        void Start()
        {
            if (playMusicOnStart) PlayMusic();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Plays one of the menu sounds, if the player and the slot exist.</summary>
        public static void Play(MenuSound sound)
        {
            if (Instance != null) Instance.PlayEffect(sound);
        }

        public void PlayEffect(MenuSound sound)
        {
            if (audioSet == null || effectsSource == null) return;

            MenuSoundEntry entry = audioSet.Get(sound);
            if (entry == null || entry.clip == null) return;

            effectsSource.PlayOneShot(entry.clip, entry.volume * audioSet.effectsVolume);
        }

        public void PlayMusic()
        {
            if (audioSet == null || musicSource == null || audioSet.menuMusic == null) return;

            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = null;
            musicSource.clip = audioSet.menuMusic;
            musicSource.volume = audioSet.musicVolume;
            musicSource.loop = true;
            if (!musicSource.isPlaying) musicSource.Play();
        }

        /// <summary>Fades the music out over <paramref name="seconds"/> (real time) and stops it.</summary>
        public static void FadeOutMusic(float seconds)
        {
            if (Instance != null) Instance.FadeOut(seconds);
        }

        void FadeOut(float seconds)
        {
            if (musicSource == null || !musicSource.isPlaying) return;
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(FadeOutRoutine(Mathf.Max(0.01f, seconds)));
        }

        IEnumerator FadeOutRoutine(float seconds)
        {
            float start = musicSource.volume;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(start, 0f, elapsed / seconds);
                yield return null;
            }
            musicSource.Stop();
            musicFade = null;
        }

        AudioSource EnsureSource(AudioSource source, bool loop)
        {
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // plain 2D UI sound, not positioned in the world
            return source;
        }
    }
}
