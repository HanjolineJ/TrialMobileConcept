using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TNTGame.Gameplay;

namespace TNTGame.Core
{
    /// <summary>High-level state of the level loop.</summary>
    public enum LevelState
    {
        /// <summary>Player is placing TNT charges.</summary>
        Placing,

        /// <summary>Charges detonated, waiting for the physics to settle.</summary>
        Detonating,

        /// <summary>Settled and scored, result panel is shown.</summary>
        Scored
    }

    /// <summary>
    /// Scene-level singleton that owns the level loop: TNT budget, detonation,
    /// settling, scoring, restart and progression (saves stars, unlocks and
    /// loads the next catalog level). Place one instance in the level scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>Singleton instance for the currently loaded level scene.</summary>
        public static GameManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Tuning data for this level (TNT count, blast values, star thresholds).")]
        [SerializeField] private LevelData levelData;

        [Tooltip("All levels in play order; used to find the next level and to record progress.")]
        [SerializeField] private LevelCatalog catalog;

        /// <summary>Raised when the remaining TNT count changes. Parameter: charges left.</summary>
        public event Action<int> TntCountChanged;

        /// <summary>Raised when the level state changes.</summary>
        public event Action<LevelState> StateChanged;

        /// <summary>
        /// Raised once when scoring finishes.
        /// Parameters: percent of the building destroyed (0–100), stars earned (1–3).
        /// </summary>
        public event Action<float, int> LevelScored;

        private readonly List<BuildingBlock> blocks = new List<BuildingBlock>();
        private readonly List<Vector2> placedCharges = new List<Vector2>();

        /// <summary>Tuning data of the current level.</summary>
        public LevelData Data => levelData;

        /// <summary>Number of registered building blocks.</summary>
        public int BlockCount => blocks.Count;

        /// <summary>Registered building blocks (read-only; mainly for tests and tooling).</summary>
        public IReadOnlyList<BuildingBlock> Blocks => blocks;

        /// <summary>Current state of the level loop.</summary>
        public LevelState State { get; private set; }

        /// <summary>Charges the player can still place.</summary>
        public int TntRemaining { get; private set; }

        /// <summary>Percent of the building destroyed (0–100). Valid after scoring.</summary>
        public float ScorePercent { get; private set; }

        /// <summary>Stars earned (1–3). Valid after scoring.</summary>
        public int Stars { get; private set; }

        /// <summary>True when a following level exists in the catalog.</summary>
        public bool HasNextLevel => catalog != null && catalog.NextAfter(levelData) != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 60 FPS target for mobile; the design doc demands >= 30 FPS sustained.
            Application.targetFrameRate = 60;

            if (levelData == null)
            {
                Debug.LogError("[GameManager] No LevelData assigned.", this);
                enabled = false;
                return;
            }

            // Initialised in Awake so UI scripts can read correct values from
            // their own Start regardless of script execution order.
            TntRemaining = levelData.tntCount;
            State = LevelState.Placing;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only hook used by the scene builder to wire the LevelData
        /// asset. (SerializedObject assignment of a freshly created
        /// ScriptableObject can silently fail; a plain field set persists.)
        /// </summary>
        public void EditorSetLevelData(LevelData data)
        {
            levelData = data;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Editor-only hook for the scene builder; same rationale as EditorSetLevelData.</summary>
        public void EditorSetCatalog(LevelCatalog levelCatalog)
        {
            catalog = levelCatalog;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        /// <summary>Registers a building block for detonation and scoring. Called by LevelSetup.</summary>
        public void RegisterBlock(BuildingBlock block)
        {
            if (block != null && !blocks.Contains(block))
                blocks.Add(block);
        }

        /// <summary>
        /// Consumes one charge and records its position for the next Detonate call.
        /// Returns false when placement is not allowed (already detonated or no charges left).
        /// </summary>
        public bool TryPlaceCharge(Vector2 position)
        {
            if (State != LevelState.Placing || TntRemaining <= 0)
                return false;

            placedCharges.Add(position);
            TntRemaining--;
            TntCountChanged?.Invoke(TntRemaining);
            return true;
        }

        /// <summary>
        /// Detonates all placed charges: every block switches to dynamic so the
        /// structure can collapse, and blocks inside a blast radius receive a
        /// radial impulse with distance falloff. No-op outside the Placing state.
        /// </summary>
        public void Detonate()
        {
            if (State != LevelState.Placing)
                return;

            SetState(LevelState.Detonating);

            // Whole building goes dynamic so upper floors can fall once their
            // support is blown away.
            foreach (BuildingBlock block in blocks)
            {
                if (block != null)
                    block.Activate();
            }

            foreach (Vector2 charge in placedCharges)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(charge, levelData.blastRadius);
                foreach (Collider2D hit in hits)
                {
                    BuildingBlock block = hit.GetComponentInParent<BuildingBlock>();
                    if (block != null)
                        block.ApplyBlast(charge, levelData.blastForce, levelData.blastRadius);
                }
            }

            StartCoroutine(SettleAndScore());
        }

        /// <summary>
        /// Restarts the level by reloading the active scene, restoring the exact
        /// initial state (full TNT count, no debris).
        /// </summary>
        public void RestartLevel()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
            else
                SceneManager.LoadScene(scene.name);
        }

        /// <summary>
        /// Loads the next catalog level's scene. No-op when this is the last
        /// level or the catalog is missing.
        /// </summary>
        public void LoadNextLevel()
        {
            LevelData next = catalog != null ? catalog.NextAfter(levelData) : null;
            if (next != null && !string.IsNullOrEmpty(next.sceneName))
                SceneManager.LoadScene(next.sceneName);
        }

        /// <summary>
        /// Waits until every block is asleep (physics settled) or the timeout
        /// from the level data elapses, then computes the score.
        /// </summary>
        private IEnumerator SettleAndScore()
        {
            // Give the blast a moment to actually wake the bodies before we
            // start checking whether they sleep.
            yield return new WaitForSeconds(0.25f);

            float elapsed = 0f;
            while (elapsed < levelData.settleTimeout)
            {
                bool allAsleep = true;
                foreach (BuildingBlock block in blocks)
                {
                    if (block != null && !block.IsAsleep)
                    {
                        allAsleep = false;
                        break;
                    }
                }

                if (allAsleep)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            ComputeScore();
        }

        /// <summary>
        /// Score = percentage of destroyed blocks (crossed the demolition line
        /// or displaced past the threshold). See BuildingBlock.IsDestroyed.
        /// Thresholds: &lt; twoStar = 1 star, up to threeStar = 2 stars, above = 3 stars.
        /// </summary>
        private void ComputeScore()
        {
            int destroyed = 0;
            int total = 0;
            foreach (BuildingBlock block in blocks)
            {
                if (block == null)
                    continue;

                total++;
                if (block.IsDestroyed(levelData.demolitionLineY, levelData.displacementThreshold))
                    destroyed++;
            }

            ScorePercent = total > 0 ? 100f * destroyed / total : 0f;

            if (ScorePercent > levelData.threeStarThreshold)
                Stars = 3;
            else if (ScorePercent >= levelData.twoStarThreshold)
                Stars = 2;
            else
                Stars = 1;

            SetState(LevelState.Scored);
            LevelScored?.Invoke(ScorePercent, Stars);

            // Completing the level (any rating) unlocks the next catalog level.
            ProgressManager.SaveStars(levelData.levelId, Stars);
        }

        private void SetState(LevelState newState)
        {
            if (State == newState)
                return;

            State = newState;
            StateChanged?.Invoke(State);
        }
    }
}
