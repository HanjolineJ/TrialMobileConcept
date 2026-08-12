using UnityEngine;
using TNTGame.Core;

namespace TNTGame.Gameplay
{
    /// <summary>
    /// Builds the level's structure procedurally from the LevelData grid
    /// (columns x rows of blocks) and registers every block with the GameManager
    /// for detonation and scoring. Optionally positions a demolition-line marker.
    /// </summary>
    public class LevelSetup : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Parent transform for the spawned blocks. Defaults to this transform when empty.")]
        [SerializeField] private Transform buildingParent;

        [Tooltip("Optional marker (e.g. a line sprite) that is moved to the demolition line Y from the level data.")]
        [SerializeField] private Transform demolitionLine;

        private void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[LevelSetup] No GameManager in the scene.", this);
                return;
            }

            LevelData data = gm.Data;
            if (data == null)
            {
                Debug.LogError("[LevelSetup] GameManager has no LevelData assigned.", this);
                return;
            }

            if (buildingParent == null)
                buildingParent = transform;

            Build(data, gm);
        }

        /// <summary>Spawns the block grid and registers every block with the GameManager.</summary>
        private void Build(LevelData data, GameManager gm)
        {
            if (data.blockPrefab == null)
            {
                Debug.LogError("[LevelSetup] LevelData has no block prefab assigned.", this);
                return;
            }

            for (int row = 0; row < data.rows; row++)
            {
                for (int column = 0; column < data.columns; column++)
                {
                    Vector2 position = data.buildOrigin + new Vector2(
                        column * data.blockSpacing.x,
                        row * data.blockSpacing.y);

                    BuildingBlock block = Instantiate(
                        data.blockPrefab, position, Quaternion.identity, buildingParent);

                    block.name = $"Block_{row}_{column}";
                    gm.RegisterBlock(block);
                }
            }

            if (demolitionLine != null)
            {
                Vector3 linePosition = demolitionLine.position;
                linePosition.y = data.demolitionLineY;
                demolitionLine.position = linePosition;
            }
        }
    }
}
