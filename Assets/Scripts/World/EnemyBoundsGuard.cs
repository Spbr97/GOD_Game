using Game.AI;
using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.World
{
    /// <summary>
    /// SPEC.md section 54's edge case 9: an enemy falls outside the world.
    ///
    /// Put back rather than destroyed. An enemy can be a quest target
    /// (<see cref="Game.Quests.QuestTarget"/>) or a boss, and section 55 says a
    /// required NPC cannot disappear permanently — deleting whatever fell through the
    /// floor is exactly the softlock that rule exists to prevent. Home is where the
    /// enemy was authored, so it is both valid and where its patrol route starts.
    ///
    /// Unlike <see cref="PlayerBoundsGuard"/> this is silent: the player did not cause
    /// it and has nothing to act on. It still logs, because a level author does.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyBoundsGuard : MonoBehaviour
    {
        [Tooltip("Seconds between checks. Staggered by a per-instance offset so a room full of enemies does not all test on the same frame.")]
        [SerializeField] private float checkInterval = 0.5f;

        private EnemyController controller;
        private NavMeshAgent agent;
        private float nextCheckAt;

        public int RecoveryCount { get; private set; }

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            agent = GetComponent<NavMeshAgent>();
            nextCheckAt = Time.time + Random.Range(0f, checkInterval);
        }

        private void Update()
        {
            if (Time.time < nextCheckAt)
            {
                return;
            }

            nextCheckAt = Time.time + checkInterval;

            if (WorldBounds.IsInsideWorld(transform.position))
            {
                return;
            }

            Recover();
        }

        /// <summary>Returns the enemy to its authored home. Public so a test can force it.</summary>
        public void Recover()
        {
            var fellFrom = transform.position;
            var home = controller != null ? controller.Home : transform.position;

            if (!WorldBounds.IsInsideWorld(home))
            {
                GameLogger.LogFallback(
                    LogCategory.AI,
                    $"'{name}' left the world and could not be put back",
                    "EnemyBoundsGuard.Recover",
                    "its own home position is outside the scene's WorldBounds",
                    "the enemy is left where it is; check the WorldBounds volume covers where this enemy was placed",
                    this);
                return;
            }

            Teleport(home);

            RecoveryCount++;

            GameLogger.LogFallback(
                LogCategory.AI,
                $"'{name}' left the world",
                "EnemyBoundsGuard.Recover",
                $"position {fellFrom} is outside the scene's WorldBounds",
                $"returned to its home at {home}",
                this);

            EventBus.Publish(new OutOfWorldRecoveryEvent(gameObject, fellFrom, home, false, string.Empty));
        }

        /// <summary>
        /// A NavMeshAgent owns its transform while it is on a mesh, so moving the
        /// transform underneath it is silently undone. Warp is the supported way, and
        /// it also re-seats the agent on the mesh at the destination.
        /// </summary>
        private void Teleport(Vector3 position)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(position);
                return;
            }

            transform.position = position;
        }
    }
}
