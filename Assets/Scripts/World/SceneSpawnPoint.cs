using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A named arrival point in a scene (TASK 041). A <see cref="SceneExit"/> in one
    /// scene names one of these in another, and the player is put here on arrival.
    ///
    /// Matched by id rather than by reference for the same reason
    /// <see cref="Game.Combat.Checkpoint"/> and <c>CheckpointManager</c> are: a scene
    /// cannot hold a reference into a scene that is not loaded, so the only thing that
    /// can cross the gap is a string. <c>SceneExitTests</c> is what stops that string
    /// being a typo nobody notices until a player walks through the door.
    ///
    /// Falls back to the GameObject's name when no id is set, matching
    /// <see cref="SaveIdentity"/> and <c>Checkpoint.CheckpointId</c>.
    /// </summary>
    public class SceneSpawnPoint : MonoBehaviour
    {
        [Tooltip("What a SceneExit in another scene names to arrive here. Falls back to this object's name.")]
        [SerializeField] private string spawnId;

        [Tooltip("Faced on arrival. Leave unset to face this object's own forward.")]
        [SerializeField] private Transform facing;

        public string SpawnId => string.IsNullOrEmpty(spawnId) ? name : spawnId;

        public Vector3 Position => transform.position;

        public Quaternion Rotation
        {
            get
            {
                if (facing == null)
                {
                    return transform.rotation;
                }

                var toTarget = facing.position - transform.position;
                toTarget.y = 0f;
                return toTarget.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(toTarget, Vector3.up)
                    : transform.rotation;
            }
        }

        /// <summary>
        /// The spawn point with this id in the loaded scenes, or null. Includes inactive
        /// objects: a spawn point is a bare transform and content authors do disable the
        /// group one sits under.
        /// </summary>
        public static SceneSpawnPoint Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var points = Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < points.Length; i++)
            {
                if (string.Equals(points[i].SpawnId, id, System.StringComparison.Ordinal))
                {
                    return points[i];
                }
            }

            return null;
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(string id, Transform facesTowards = null)
        {
            spawnId = id;
            facing = facesTowards;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawRay(transform.position + Vector3.up, Rotation * Vector3.forward * 2f);
        }
    }
}
