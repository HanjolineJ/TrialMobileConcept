using UnityEngine;
using UnityEngine.UI;
using TNTGame.Core;

namespace TNTGame.UI
{
    /// <summary>
    /// Mobile HUD and result panel. Shows the remaining TNT count, wires the
    /// detonate/restart buttons to the GameManager, and reveals the result
    /// panel (percent destroyed + 1–3 stars) when scoring finishes.
    /// All references are assigned in the Inspector; the script only listens
    /// to GameManager events and never drives game logic itself.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        [Tooltip("Label showing how many TNT charges are left.")]
        [SerializeField] private Text tntCountText;

        [Tooltip("Button that triggers the explosion.")]
        [SerializeField] private Button detonateButton;

        [Tooltip("Button that reloads the level.")]
        [SerializeField] private Button restartButton;

        [Header("Music Toggle")]
        [Tooltip("Button toggling the background music on/off.")]
        [SerializeField] private Button musicButton;

        [Tooltip("Icon image inside the music button; its sprite shows the state.")]
        [SerializeField] private Image musicIcon;

        [Tooltip("Icon shown while music is on (musical note).")]
        [SerializeField] private Sprite musicOnSprite;

        [Tooltip("Icon shown while music is off (slashed note).")]
        [SerializeField] private Sprite musicOffSprite;

        [Header("Result Panel")]
        [Tooltip("Root object of the result panel, hidden until scoring finishes.")]
        [SerializeField] private GameObject resultPanel;

        [Tooltip("Label showing the destroyed percentage.")]
        [SerializeField] private Text scoreText;

        [Tooltip("Exactly three star slots, left to right.")]
        [SerializeField] private Image[] starImages = new Image[3];

        [Tooltip("Sprite for an earned star.")]
        [SerializeField] private Sprite starFilledSprite;

        [Tooltip("Sprite for an unearned star.")]
        [SerializeField] private Sprite starEmptySprite;

        private GameManager gm;

        private void Start()
        {
            gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[UIManager] No GameManager in the scene.", this);
                enabled = false;
                return;
            }

            gm.TntCountChanged += HandleTntCountChanged;
            gm.StateChanged += HandleStateChanged;
            gm.LevelScored += HandleLevelScored;

            if (detonateButton != null)
                detonateButton.onClick.AddListener(gm.Detonate);
            if (restartButton != null)
                restartButton.onClick.AddListener(gm.RestartLevel);
            if (musicButton != null)
                musicButton.onClick.AddListener(HandleMusicClicked);

            if (resultPanel != null)
                resultPanel.SetActive(false);

            // Sync the UI with the current state in case events fired before
            // this script subscribed.
            HandleTntCountChanged(gm.TntRemaining);
            HandleStateChanged(gm.State);
            UpdateMusicIcon();
        }

        private void OnDestroy()
        {
            if (gm == null)
                return;

            gm.TntCountChanged -= HandleTntCountChanged;
            gm.StateChanged -= HandleStateChanged;
            gm.LevelScored -= HandleLevelScored;
        }

        private void HandleTntCountChanged(int remaining)
        {
            if (tntCountText != null)
                tntCountText.text = $"TNT: {remaining}";
        }

        private void HandleStateChanged(LevelState state)
        {
            // Charges can only be placed and detonated while placing.
            if (detonateButton != null)
                detonateButton.interactable = state == LevelState.Placing;

            // Restart must stay available in every state — including Scored,
            // where the result panel is visible.
            if (restartButton != null)
                restartButton.interactable = true;
        }

        private void HandleMusicClicked()
        {
            if (AudioManager.Instance == null)
                return;

            AudioManager.Instance.ToggleMusic();
            UpdateMusicIcon();
        }

        private void UpdateMusicIcon()
        {
            // Default to the "on" icon when no AudioManager exists.
            bool isOn = AudioManager.Instance == null || AudioManager.Instance.IsMusicOn;
            if (musicIcon != null)
                musicIcon.sprite = isOn ? musicOnSprite : musicOffSprite;
        }

        private void HandleLevelScored(float percent, int stars)
        {
            if (resultPanel != null)
                resultPanel.SetActive(true);

            if (scoreText != null)
                scoreText.text = $"Destroyed: {percent:0}%";

            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] != null)
                    starImages[i].sprite = i < stars ? starFilledSprite : starEmptySprite;
            }
        }
    }
}
