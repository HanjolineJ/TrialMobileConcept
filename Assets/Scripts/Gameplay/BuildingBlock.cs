using UnityEngine;

namespace TNTGame.Gameplay
{
    /// <summary>
    /// A single destructible block of the building.
    /// Starts with a static Rigidbody2D so the structure is perfectly stable
    /// before detonation, and switches to dynamic when the blast reaches it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class BuildingBlock : MonoBehaviour
    {
        private Rigidbody2D rb;
        private Vector2 initialPosition;
        private bool activated;

        /// <summary>World position the block was placed at when the level was built.</summary>
        public Vector2 InitialPosition => initialPosition;

        /// <summary>True while the block's body is asleep (or still static).</summary>
        public bool IsAsleep => rb == null || rb.IsSleeping() || rb.bodyType != RigidbodyType2D.Dynamic;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // Static until the blast: the building cannot wobble or collapse
            // while the player is still placing charges.
            rb.bodyType = RigidbodyType2D.Static;
        }

        private void Start()
        {
            // Cached in Start (not Awake) so the position is final: LevelSetup
            // moves the block into place right after Instantiate, and Awake runs
            // before that assignment.
            initialPosition = transform.position;
        }

        /// <summary>
        /// Switches the block to a dynamic body so physics takes over.
        /// Idempotent — safe to call once per charge.
        /// </summary>
        public void Activate()
        {
            if (activated)
                return;

            activated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        /// <summary>
        /// Applies a radial blast impulse to the block, with linear falloff
        /// from full force at the blast centre to zero at the edge of the radius.
        /// Also activates the block so it can be pushed.
        /// (2D has no AddExplosionForce, so the falloff is done by hand.)
        /// </summary>
        /// <param name="origin">World position of the explosion.</param>
        /// <param name="force">Peak impulse strength at the centre.</param>
        /// <param name="radius">Blast radius in world units.</param>
        public void ApplyBlast(Vector2 origin, float force, float radius)
        {
            Activate();

            Vector2 offset = rb.worldCenterOfMass - origin;
            float distance = offset.magnitude;
            if (distance >= radius)
                return;

            float falloff = 1f - distance / radius;
            // Push straight up for a (near) direct hit to avoid a zero direction.
            Vector2 direction = distance > 0.001f ? offset / distance : Vector2.up;
            rb.AddForce(direction * (force * falloff), ForceMode2D.Impulse);
        }

        /// <summary>
        /// True when the block counts as destroyed for scoring: either it
        /// started above the demolition line and ended up below it, or it was
        /// knocked away from its original position by more than the threshold.
        /// (A pure "below the line" test cannot distinguish a fallen block from
        /// one still standing on the ground — both rest at the same height.)
        /// </summary>
        public bool IsDestroyed(float demolitionLineY, float displacementThreshold)
        {
            if (initialPosition.y >= demolitionLineY && transform.position.y < demolitionLineY)
                return true;

            return ((Vector2)transform.position - initialPosition).sqrMagnitude
                   > displacementThreshold * displacementThreshold;
        }
    }
}
