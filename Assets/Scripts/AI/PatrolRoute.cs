using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// An ordered set of waypoints for <see cref="EnemyState.Patrol"/> (SPEC.md
    /// section 17). A route is a scene object rather than data on the enemy so
    /// several enemies can share one patrol, and so the path is visible in the Scene
    /// view via <see cref="OnDrawGizmos"/>.
    /// </summary>
    public class PatrolRoute : MonoBehaviour
    {
        [Tooltip("Visited in order. Leave empty to use this object's own children as the waypoints.")]
        [SerializeField] private Transform[] waypoints;

        [Tooltip("Loop back to the first waypoint at the end; otherwise walk the route in reverse.")]
        [SerializeField] private bool loop = true;

        [Tooltip("Seconds to stand still at each waypoint.")]
        [SerializeField] private float waitAtWaypoint = 1.5f;

        private int direction = 1;

        public float WaitAtWaypoint => waitAtWaypoint;

        public int WaypointCount
        {
            get
            {
                EnsureWaypoints();
                return waypoints.Length;
            }
        }

        public Vector3 GetPosition(int index)
        {
            EnsureWaypoints();
            if (waypoints.Length == 0)
            {
                return transform.position;
            }

            return waypoints[Mathf.Clamp(index, 0, waypoints.Length - 1)].position;
        }

        /// <summary>
        /// Returns the index after <paramref name="current"/>. A non-looping route
        /// reverses at each end rather than snapping back to the start, which reads as
        /// a guard walking a beat instead of teleporting.
        /// </summary>
        public int NextIndex(int current)
        {
            EnsureWaypoints();
            if (waypoints.Length <= 1)
            {
                return 0;
            }

            if (loop)
            {
                return (current + 1) % waypoints.Length;
            }

            var next = current + direction;
            if (next >= waypoints.Length || next < 0)
            {
                direction = -direction;
                next = current + direction;
            }

            return Mathf.Clamp(next, 0, waypoints.Length - 1);
        }

        /// <summary>The waypoint nearest a position, so an enemy rejoins its route where it stands.</summary>
        public int NearestIndex(Vector3 position)
        {
            EnsureWaypoints();
            var best = 0;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < waypoints.Length; i++)
            {
                var distance = (waypoints[i].position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(Transform[] routeWaypoints, bool looping = true, float wait = 1.5f)
        {
            waypoints = routeWaypoints;
            loop = looping;
            waitAtWaypoint = wait;
        }

        private void EnsureWaypoints()
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                return;
            }

            var children = new Transform[transform.childCount];
            for (var i = 0; i < transform.childCount; i++)
            {
                children[i] = transform.GetChild(i);
            }

            waypoints = children;
        }

        private void OnDrawGizmos()
        {
            EnsureWaypoints();
            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            for (var i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(waypoints[i].position, 0.4f);

                var next = i + 1;
                if (next >= waypoints.Length)
                {
                    if (!loop)
                    {
                        continue;
                    }

                    next = 0;
                }

                if (waypoints[next] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
                }
            }
        }
    }
}
