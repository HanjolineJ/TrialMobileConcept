using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TNTGame.Core;

namespace TNTGame.UI
{
    /// <summary>
    /// Modal level select overlay. Lists every catalog level with its saved
    /// star rating; locked levels (previous level not completed) are visible
    /// but not clickable. Refreshed on every Show so newly earned stars and
    /// unlocks appear immediately. The panel starts inactive.
    /// </summary>
    public class LevelSelectUI : MonoBehaviour
    {
        [Tooltip("All levels in play order.")]
        [SerializeField] private LevelCatalog catalog;

        [Tooltip("Root object of the overlay, hidden until Show is called.")]
        [SerializeField] private GameObject panel;

        [Tooltip("Button that closes the overlay without switching levels.")]
        [SerializeField] private Button closeButton;

        [Tooltip("One row per catalog level, in the same order.")]
        [SerializeField] private LevelSelectEntry[] entries = new LevelSelectEntry[0];

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only hook used by the scene builder to wire the catalog.
        /// (SerializedObject assignment of a freshly created ScriptableObject
        /// can silently fail; a plain field set persists.)
        /// </summary>
        public void EditorSetCatalog(LevelCatalog levelCatalog)
        {
            catalog = levelCatalog;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void Start()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (panel != null)
                panel.SetActive(false);
        }

        /// <summary>Opens the overlay and refreshes names, locks and star ratings.</summary>
        public void Show()
        {
            Refresh();
            if (panel != null)
                panel.SetActive(true);
        }

        /// <summary>Closes the overlay.</summary>
        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }

        private void Refresh()
        {
            if (catalog == null)
            {
                Debug.LogError("[LevelSelectUI] No LevelCatalog assigned.", this);
                return;
            }

            int count = Mathf.Min(entries.Length, catalog.levels.Length);
            for (int i = 0; i < count; i++)
            {
                LevelData data = catalog.levels[i];
                if (entries[i] == null || data == null)
                    continue;

                entries[i].Setup(
                    data,
                    i + 1, // levels are numbered from 1
                    catalog.IsUnlocked(i),
                    ProgressManager.GetStars(data.levelId),
                    () => SceneManager.LoadScene(data.sceneName));
            }
        }
    }
}
