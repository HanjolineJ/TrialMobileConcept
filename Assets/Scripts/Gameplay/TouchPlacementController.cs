using UnityEngine;
using UnityEngine.EventSystems;
using TNTGame.Core;

namespace TNTGame.Gameplay
{
    /// <summary>
    /// Handles touch/mouse placement of TNT charges onto building blocks.
    /// Press and hold to aim: an indicator follows the pointer and glows teal
    /// over a valid block, ember-red otherwise. Release over a block to attach the
    /// charge to the block surface. Uses the legacy Input API, which covers
    /// both mouse (editor) and touch (device) while Active Input Handling is
    /// set to "Both". Ignores presses that start on UI elements.
    /// </summary>
    public class TouchPlacementController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Visual spawned at the charge position on successful placement.")]
        [SerializeField] private GameObject chargePrefab;

        [Tooltip("Indicator shown under the pointer while aiming. Tinted by validity.")]
        [SerializeField] private SpriteRenderer indicator;

        [Tooltip("Camera used for screen-to-world conversion. Defaults to Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Header("Feedback")]
        [SerializeField] private Color validColor = new Color(0.25f, 0.9f, 0.8f, 0.65f);   // teal ocean glow
        [SerializeField] private Color invalidColor = new Color(0.9f, 0.3f, 0.2f, 0.4f);   // ember red

        private GameManager gm;
        private bool pointerHeld;
        private int activeFingerId = -1;

        private void Start()
        {
            gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[TouchPlacementController] No GameManager in the scene.", this);
                enabled = false;
                return;
            }

            if (worldCamera == null)
                worldCamera = Camera.main;

            SetIndicatorVisible(false);
        }

        private void Update()
        {
            // Placement locks once the charges are detonated.
            if (gm.State != LevelState.Placing)
            {
                pointerHeld = false;
                activeFingerId = -1;
                SetIndicatorVisible(false);
                return;
            }

            bool pressed, released;
            Vector2 screenPos;
            int fingerId;
            ReadPointer(out screenPos, out pressed, out released, out fingerId);

            if (pressed)
            {
                // Taps on UI buttons must not place charges.
                if (IsPointerOverUI(fingerId))
                    return;

                pointerHeld = true;
                activeFingerId = fingerId;
            }

            if (!pointerHeld)
            {
                SetIndicatorVisible(false);
                return;
            }

            // While held: move the indicator and evaluate validity.
            Vector2 worldPos = ScreenToWorld(screenPos);
            Collider2D hit = Physics2D.OverlapPoint(worldPos);
            bool valid = hit != null
                         && hit.GetComponentInParent<BuildingBlock>() != null
                         && gm.TntRemaining > 0;

            if (indicator != null)
            {
                indicator.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
                indicator.color = valid ? validColor : invalidColor;
            }

            if (released)
            {
                if (valid)
                    TryPlace(worldPos, hit);

                pointerHeld = false;
                activeFingerId = -1;
                SetIndicatorVisible(false);
            }
        }

        /// <summary>
        /// Snaps the charge to the block surface, registers it with the
        /// GameManager and spawns the charge visual parented to the block,
        /// so it rides along with the debris after the blast.
        /// </summary>
        private void TryPlace(Vector2 worldPos, Collider2D blockCollider)
        {
            Vector2 attachPosition = blockCollider.ClosestPoint(worldPos);

            if (!gm.TryPlaceCharge(attachPosition))
                return;

            if (chargePrefab != null)
            {
                GameObject charge = Instantiate(chargePrefab, attachPosition, Quaternion.identity);
                charge.transform.SetParent(blockCollider.transform, true);
            }
        }

        /// <summary>
        /// Unifies touch and mouse into one pointer stream.
        /// Only the first finger is tracked; extra fingers are ignored.
        /// </summary>
        private void ReadPointer(out Vector2 screenPos, out bool pressed, out bool released, out int fingerId)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                // While a finger is tracked, ignore a different finger landing.
                if (activeFingerId >= 0 && touch.fingerId != activeFingerId && pointerHeld)
                {
                    screenPos = Vector2.zero;
                    pressed = false;
                    released = false;
                    fingerId = activeFingerId;
                    return;
                }

                screenPos = touch.position;
                pressed = touch.phase == TouchPhase.Began;
                released = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                fingerId = touch.fingerId;
            }
            else
            {
                screenPos = Input.mousePosition;
                pressed = Input.GetMouseButtonDown(0);
                released = Input.GetMouseButtonUp(0);
                fingerId = -1;
            }
        }

        private bool IsPointerOverUI(int fingerId)
        {
            if (EventSystem.current == null)
                return false;

            return fingerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(fingerId)
                : EventSystem.current.IsPointerOverGameObject();
        }

        private Vector2 ScreenToWorld(Vector2 screenPos)
        {
            float distance = -worldCamera.transform.position.z; // world plane is z = 0
            return worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distance));
        }

        private void SetIndicatorVisible(bool visible)
        {
            if (indicator != null && indicator.gameObject.activeSelf != visible)
                indicator.gameObject.SetActive(visible);
        }
    }
}
