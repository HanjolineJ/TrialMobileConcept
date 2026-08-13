using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TNTGame.Core;

namespace TNTGame.UI
{
    /// <summary>
    /// One row of the level select screen: a button showing the level name,
    /// its saved star rating, or a LOCKED label while the previous level is
    /// uncompleted. All references are assigned in the Inspector (or by the
    /// scene builder); Setup rebinds everything at show time.
    /// </summary>
    public class LevelSelectEntry : MonoBehaviour
    {
        [Tooltip("Root button of the row; loads the level when unlocked.")]
        [SerializeField] private Button button;

        [Tooltip("Label showing the level's display name.")]
        [SerializeField] private Text nameText;

        [Tooltip("Overlay label shown instead of the stars while the level is locked.")]
        [SerializeField] private Text lockText;

        [Tooltip("Exactly three star slots, left to right.")]
        [SerializeField] private Image[] starImages = new Image[3];

        [Tooltip("Sprite for an earned star.")]
        [SerializeField] private Sprite starFilledSprite;

        [Tooltip("Sprite for an unearned star.")]
        [SerializeField] private Sprite starEmptySprite;

        /// <summary>Rebinds the row to the given level and click action. Level numbers are 1-based.</summary>
        public void Setup(LevelData data, int levelNumber, bool unlocked, int stars, UnityAction onClick)
        {
            if (nameText != null)
                nameText.text = $"{levelNumber}. {data.displayName}";

            if (lockText != null)
                lockText.gameObject.SetActive(!unlocked);

            if (button != null)
            {
                button.interactable = unlocked;
                button.onClick.RemoveAllListeners();
                if (unlocked)
                    button.onClick.AddListener(onClick);
            }

            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] != null)
                {
                    starImages[i].gameObject.SetActive(unlocked);
                    starImages[i].sprite = i < stars ? starFilledSprite : starEmptySprite;
                }
            }
        }
    }
}
