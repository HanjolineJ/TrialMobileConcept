using UnityEngine;

namespace TNTGame.Core
{
    /// <summary>
    /// Persistent background-music player. Plays the configured track on game
    /// start, loops it, and survives scene reloads (RestartLevel reloads the
    /// scene) via DontDestroyOnLoad. Duplicate instances created by a scene
    /// reload destroy themselves, so the music never double-plays.
    /// Volume defaults to 0.45 so future sound effects sit clearly on top.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        /// <summary>Singleton instance (persists across scene loads).</summary>
        public static AudioManager Instance { get; private set; }

        [Tooltip("Looping background track. Missing? See Assets/Audio/README.md.")]
        [SerializeField] private AudioClip musicTrack;

        [Tooltip("Music volume (0..1). Deliberately below full so SFX stay on top.")]
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.45f;

        private AudioSource source;

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

            source = GetComponent<AudioSource>();
            source.clip = musicTrack;
            source.loop = true;          // seamless-enough restart at track end
            source.playOnAwake = false;  // started explicitly in Start
            source.spatialBlend = 0f;    // 2D (non-positional)
            source.volume = musicVolume;
        }

        private void Start()
        {
            if (source.clip != null)
            {
                source.Play();
            }
            else
            {
                Debug.LogWarning("[AudioManager] No music track assigned. Drop a loopable file into " +
                                 "Assets/Audio (see README there) and assign it in the Inspector, " +
                                 "or re-run TNT > Build Level 01 Scene.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Sets the music volume (0..1) and applies it immediately.</summary>
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (source != null)
                source.volume = musicVolume;
        }

        /// <summary>Mutes/unmutes the music without losing playback position.</summary>
        public void SetMusicMuted(bool muted)
        {
            if (source != null)
                source.mute = muted;
        }

        /// <summary>Toggles music mute; returns the new muted state.</summary>
        public bool ToggleMusicMuted()
        {
            bool muted = source != null && !source.mute;
            SetMusicMuted(muted);
            return muted;
        }
    }
}
