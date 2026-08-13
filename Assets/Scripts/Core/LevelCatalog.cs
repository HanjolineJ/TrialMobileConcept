using UnityEngine;

namespace TNTGame.Core
{
    /// <summary>
    /// Ordered list of all levels. Defines progression: the first level is
    /// always playable, every later level unlocks once the previous one is
    /// completed (at least one star saved, see ProgressManager).
    /// Create via Assets &gt; Create &gt; TNT &gt; Level Catalog; the scene
    /// builder generates the asset automatically.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "TNT/Level Catalog", order = 1)]
    public class LevelCatalog : ScriptableObject
    {
        [Tooltip("All levels in play order. Unlock order follows this list.")]
        public LevelData[] levels = new LevelData[0];

        /// <summary>Index of the level in the catalog, or -1 when not listed.</summary>
        public int IndexOf(LevelData data)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == data)
                    return i;
            }
            return -1;
        }

        /// <summary>The level after the given one, or null when it is the last (or unlisted).</summary>
        public LevelData NextAfter(LevelData data)
        {
            int index = IndexOf(data);
            return index >= 0 && index + 1 < levels.Length ? levels[index + 1] : null;
        }

        /// <summary>First level is always open; later levels need the previous one completed.</summary>
        public bool IsUnlocked(int index)
        {
            if (index <= 0)
                return index == 0;
            return ProgressManager.GetStars(levels[index - 1].levelId) > 0;
        }
    }
}
