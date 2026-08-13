using UnityEngine;
using TNTGame.Gameplay;

namespace TNTGame.Core
{
    /// <summary>
    /// Designer-tunable configuration for a single level.
    /// Create instances via Assets &gt; Create &gt; TNT &gt; Level Data and store them in Assets/Data.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "TNT/Level Data", order = 0)]
    public class LevelData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used as the PlayerPrefs key for saved stars (e.g. \"skybound\").")]
        public string levelId = "level";

        [Tooltip("Name shown on the level select screen.")]
        public string displayName = "Level";

        [Tooltip("Scene that runs this level. Must be registered in Build Settings.")]
        public string sceneName = "";

        [Header("Explosives")]
        [Tooltip("How many TNT charges the player may place this level.")]
        [Min(1)] public int tntCount = 3;

        [Tooltip("Radius of the blast in world units. Blocks outside the radius get no impulse.")]
        [Min(0.1f)] public float blastRadius = 2.5f;

        [Tooltip("Peak impulse applied at the centre of the blast. Falls off linearly to zero at the edge of the radius.")]
        [Min(0f)] public float blastForce = 12f;

        [Header("Scoring")]
        [Tooltip("Percent of blocks that must be destroyed for 2 stars.")]
        [Range(0f, 100f)] public float twoStarThreshold = 40f;

        [Tooltip("Percent of blocks that must be destroyed for 3 stars.")]
        [Range(0f, 100f)] public float threeStarThreshold = 75f;

        [Tooltip("Maximum seconds to wait for the physics to settle before scoring anyway.")]
        [Min(0.5f)] public float settleTimeout = 3f;

        [Tooltip("A block moved more than this many world units from its start position counts as destroyed, even if it never crosses the demolition line.")]
        [Min(0.05f)] public float displacementThreshold = 0.5f;

        [Header("Building Layout")]
        [Tooltip("Prefab spawned for every block of the building. Must have a BuildingBlock component.")]
        public BuildingBlock blockPrefab;

        [Tooltip("Number of blocks per horizontal row.")]
        [Min(1)] public int columns = 5;

        [Tooltip("Number of stacked rows.")]
        [Min(1)] public int rows = 6;

        [Tooltip("World-space distance between block centres.")]
        public Vector2 blockSpacing = new Vector2(0.55f, 0.55f);

        [Tooltip("World-space position of the bottom-left block of the grid. Y = half a block height, so the bottom row rests on the ground.")]
        public Vector2 buildOrigin = new Vector2(-1.1f, 0.25f);

        [Tooltip("World-space Y of the demolition line. Blocks that start above it and end up below it count as destroyed.")]
        public float demolitionLineY = 0.4f;
    }
}
