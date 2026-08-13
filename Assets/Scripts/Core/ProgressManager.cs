using UnityEngine;

namespace TNTGame.Core
{
    /// <summary>
    /// Persistent player progress: best star rating per level, stored in
    /// PlayerPrefs. Unlock state is derived, never stored: the first catalog
    /// level is always unlocked, every other level unlocks when the previous
    /// one has at least one star (see LevelCatalog.IsUnlocked).
    /// </summary>
    public static class ProgressManager
    {
        private const string StarsKeyPrefix = "TNT.Stars.";

        /// <summary>Best star rating saved for the level (0 = not completed yet).</summary>
        public static int GetStars(string levelId)
        {
            return PlayerPrefs.GetInt(StarsKeyPrefix + levelId, 0);
        }

        /// <summary>Saves the rating when it beats the stored best.</summary>
        public static void SaveStars(string levelId, int stars)
        {
            if (string.IsNullOrEmpty(levelId) || stars <= GetStars(levelId))
                return;

            PlayerPrefs.SetInt(StarsKeyPrefix + levelId, stars);
            PlayerPrefs.Save();
        }

        /// <summary>Clears the saved rating (used by the play tests for a clean run).</summary>
        public static void DeleteStars(string levelId)
        {
            PlayerPrefs.DeleteKey(StarsKeyPrefix + levelId);
        }
    }
}
