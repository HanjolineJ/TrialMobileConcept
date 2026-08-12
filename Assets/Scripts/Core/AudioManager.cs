using UnityEngine;

namespace TNTGame.Core
{
    /// <summary>
    /// Persistent background-music player. Plays the configured tracks on game
    /// start, loops them, and survives scene reloads (RestartLevel reloads the
    /// scene) via DontDestroyOnLoad. Duplicate instances created by a scene
    /// reload destroy themselves, so the music never double-plays.
    ///
    /// Two layers: the main theme (adventurous pirate track) and a quieter
    /// mystical ambience (the djinn's magic — also intended for menu/upgrade
    /// screens later). Music volume defaults to 0.45 and ambience to 0.18 so
    /// future sound effects sit clearly on top.
    ///
    /// The on/off preference is stored in PlayerPrefs and re-applied on every
    /// launch; it only ever mutes the two music sources, never SFX.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        /// <summary>Singleton instance (persists across scene loads).</summary>
        public static AudioManager Instance { get; private set; }

        [Header("Main Theme")]
        [Tooltip("Looping background track. Missing? See Assets/Audio/README.md.")]
        [SerializeField] private AudioClip musicTrack;

        [Tooltip("Music volume (0..1). Deliberately below full so SFX stay on top.")]
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.45f;

        [Header("Ambience Layer")]
        [Tooltip("Secondary mystical layer (djinn magic / future menus). Optional.")]
        [SerializeField] private AudioClip ambienceTrack;

        [Tooltip("Ambience volume (0..1). Kept low: it is a background layer.")]
        [Range(0f, 1f)] [SerializeField] private float ambienceVolume = 0.18f;

        private const string MusicOnPrefKey = "TNT.MusicOn";

        private AudioSource musicSource;
        private AudioSource ambienceSource;

        /// <summary>
        /// Whether music is enabled. Restored from PlayerPrefs on launch and
        /// survives scene reloads (this object persists).
        /// </summary>
        public bool IsMusicOn { get; private set; } = true;

        private void Awake()
        {
            // Keep only the first instance; copies from a reloaded scene would
            // otherwise stack a second playback.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            musicSource = GetComponent<AudioSource>();
            Configure(musicSource, musicTrack, musicVolume);

            // Second source for the ambience layer (added in code so the scene
            // object only needs one AudioSource component).
            ambienceSource = gameObject.AddComponent<AudioSource>();
            Configure(ambienceSource, ambienceTrack, ambienceVolume);

            // Restore the player's saved preference before playback starts.
            IsMusicOn = PlayerPrefs.GetInt(MusicOnPrefKey, 1) == 1;
            ApplyMusicState();
        }

        private static void Configure(AudioSource source, AudioClip clip, float volume)
        {
            source.clip = clip;
            source.loop = true;          // restart at track end
            source.playOnAwake = false;  // started explicitly in Start
            source.spatialBlend = 0f;    // 2D (non-positional)
            source.volume = volume;
        }

        private void Start()
        {
            if (musicSource.clip != null)
            {
                musicSource.Play();
            }
            else
            {
                Debug.LogWarning("[AudioManager] No music track assigned. Drop a loopable file into " +
                                 "Assets/Audio (see README there) and assign it in the Inspector, " +
                                 "or re-run TNT > Build Level 01 Scene.");
            }

            // Ambience is optional — silence is fine, no warning.
            if (ambienceSource.clip != null)
                ambienceSource.Play();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Toggles music on/off; returns the new state. Preference is saved.</summary>
        public bool ToggleMusic()
        {
            SetMusicOn(!IsMusicOn);
            return IsMusicOn;
        }

        /// <summary>Enables/disables the music (both layers) and saves the preference.</summary>
        public void SetMusicOn(bool on)
        {
            IsMusicOn = on;
            PlayerPrefs.SetInt(MusicOnPrefKey, on ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMusicState();
        }

        /// <summary>Mutes both music sources without touching playback or SFX.</summary>
        private void ApplyMusicState()
        {
            bool muted = !IsMusicOn;
            if (musicSource != null)
                musicSource.mute = muted;
            if (ambienceSource != null)
                ambienceSource.mute = muted;
        }

        /// <summary>Sets the main theme volume (0..1) and applies it immediately.</summary>
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null)
                musicSource.volume = musicVolume;
        }

        /// <summary>Sets the ambience layer volume (0..1) and applies it immediately.</summary>
        public void SetAmbienceVolume(float volume)
        {
            ambienceVolume = Mathf.Clamp01(volume);
            if (ambienceSource != null)
                ambienceSource.volume = ambienceVolume;
        }
    }
}
