using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Raised when something was found outside the world and put back (SPEC.md
    /// section 50's "invalid player position"). Carries where it fell from so a log
    /// or a debug overlay can point a level author at the hole.
    /// </summary>
    public readonly struct OutOfWorldRecoveryEvent
    {
        public readonly GameObject Subject;
        public readonly Vector3 FellFrom;
        public readonly Vector3 RecoveredTo;
        public readonly bool IsPlayer;

        /// <summary>The sentence a player is shown. Empty for enemies, which recover silently.</summary>
        public readonly string PlayerMessage;

        public OutOfWorldRecoveryEvent(GameObject subject, Vector3 fellFrom, Vector3 recoveredTo, bool isPlayer, string playerMessage)
        {
            Subject = subject;
            FellFrom = fellFrom;
            RecoveredTo = recoveredTo;
            IsPlayer = isPlayer;
            PlayerMessage = playerMessage;
        }
    }

    /// <summary>
    /// The volume the game treats as "inside the world" (SPEC.md section 50's invalid
    /// player position, section 54's edge cases 9 and 10, and section 56's
    /// out-of-bounds traversal).
    ///
    /// A scene without one is not an error. <see cref="IsInsideWorld"/> answers true
    /// when no bounds exist, so every test arena and the boot scene keep working
    /// unchanged and the volume stays something a level opts into rather than a
    /// global that every scene must satisfy before it can run.
    ///
    /// The box's Y extent is deliberately unused: the kill plane is the only vertical
    /// limit, so a jump, a tall temple or a staircase can never read as an escape.
    /// Horizontal limits and a floor are what a level actually has.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldBounds : MonoBehaviour
    {
        private static WorldBounds instance;

        /// <summary>Resolved on first access; see <see cref="SceneSingleton"/>.</summary>
        public static WorldBounds Instance => SceneSingleton.Resolve(ref instance);

        [Tooltip("Anything below this height has fallen out of the world, however wide the horizontal bounds are.")]
        [SerializeField] private float killPlaneY = -30f;

        [Tooltip("Also treat leaving the box below as out of the world. Off leaves the kill plane as the only limit, which is what an open scene wants.")]
        [SerializeField] private bool useHorizontalBounds = true;

        [Tooltip("Centre of the playable box, in world space.")]
        [SerializeField] private Vector3 boundsCentre = Vector3.zero;

        [Tooltip("Width and depth of the playable box. Its Y is ignored — the kill plane is the only vertical limit.")]
        [SerializeField] private Vector3 boundsSize = new(400f, 0f, 400f);

        public float KillPlaneY => killPlaneY;
        public Vector3 BoundsCentre => boundsCentre;
        public Vector3 BoundsSize => boundsSize;

        /// <summary>Whether a position is still inside this scene's playable world.</summary>
        public bool Contains(Vector3 position)
        {
            if (position.y < killPlaneY)
            {
                return false;
            }

            if (!useHorizontalBounds)
            {
                return true;
            }

            var offset = position - boundsCentre;
            return Mathf.Abs(offset.x) <= boundsSize.x * 0.5f
                   && Mathf.Abs(offset.z) <= boundsSize.z * 0.5f;
        }

        /// <summary>
        /// The same question without needing a reference, and permissive when the
        /// scene has no bounds — a guard asking "is this position valid?" must not
        /// start dragging things around merely because a scene never opted in.
        /// </summary>
        public static bool IsInsideWorld(Vector3 position)
        {
            var bounds = Instance;
            return bounds == null || bounds.Contains(position);
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(float killPlane, Vector3 centre, Vector3 size, bool horizontal = true)
        {
            killPlaneY = killPlane;
            boundsCentre = centre;
            boundsSize = size;
            useHorizontalBounds = horizontal;
        }

        private void OnDrawGizmosSelected()
        {
            // Drawn tall enough to see from the scene floor, but only the footprint is
            // enforced — see the class comment on why Y is not a limit.
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
            var centre = new Vector3(boundsCentre.x, killPlaneY + 50f, boundsCentre.z);
            Gizmos.DrawWireCube(centre, new Vector3(boundsSize.x, 100f, boundsSize.z));

            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.5f);
            var plane = new Vector3(boundsCentre.x, killPlaneY, boundsCentre.z);
            Gizmos.DrawWireCube(plane, new Vector3(boundsSize.x, 0.05f, boundsSize.z));
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
