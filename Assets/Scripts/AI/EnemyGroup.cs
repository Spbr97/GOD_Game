using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Group coordination (SPEC.md section 17). Two jobs:
    ///
    /// 1. Attack slots. Only a few members may swing at once; the rest circle. Without
    ///    this, five enemies reaching the player together all attack on the same frame,
    ///    which is unreadable rather than difficult. The count scales with difficulty
    ///    (SPEC.md section 44: difficulty affects behaviour, not health).
    /// 2. Shared alerts. One member spotting the player sends the others to
    ///    investigate, so a patrol reacts as a squad.
    ///
    /// Members find their group with <c>GetComponentInParent</c>, so grouping is just
    /// scene hierarchy — an encounter is a parent object with enemies under it.
    /// </summary>
    public class EnemyGroup : MonoBehaviour
    {
        [Tooltip("How many members may attack simultaneously at Normal difficulty.")]
        [Min(1)][SerializeField] private int baseSimultaneousAttackers = 2;

        [Tooltip("An alert from one member reaches others within this radius.")]
        [SerializeField] private float alertRadius = 20f;

        private readonly List<EnemyController> members = new();
        private readonly List<EnemyController> attackers = new();

        /// <summary>Attack slots available at the current difficulty.</summary>
        public int MaxSimultaneousAttackers => Difficulty.ScaleAttackerCount(baseSimultaneousAttackers);

        public int ActiveAttackerCount => attackers.Count;
        public int MemberCount => members.Count;

        public void Register(EnemyController member)
        {
            if (member != null && !members.Contains(member))
            {
                members.Add(member);
            }
        }

        public void Unregister(EnemyController member)
        {
            members.Remove(member);
            attackers.Remove(member);
        }

        /// <summary>
        /// Asks for permission to attack. A member that already holds a slot keeps it,
        /// so re-asking mid-combo cannot lose it to a latecomer.
        /// </summary>
        public bool TryClaimAttackSlot(EnemyController member)
        {
            if (member == null)
            {
                return false;
            }

            if (attackers.Contains(member))
            {
                return true;
            }

            // Drop members that died or were despawned while holding a slot, or the
            // encounter would deadlock with nobody allowed to attack.
            attackers.RemoveAll(a => a == null || !a.isActiveAndEnabled || a.State == EnemyState.Dead);

            if (attackers.Count >= MaxSimultaneousAttackers)
            {
                return false;
            }

            attackers.Add(member);
            return true;
        }

        public void ReleaseAttackSlot(EnemyController member)
        {
            attackers.Remove(member);
        }

        public bool HoldsAttackSlot(EnemyController member)
        {
            return attackers.Contains(member);
        }

        /// <summary>
        /// Tells nearby members where something happened. They investigate rather than
        /// instantly acquiring the target, so a shout is a lead, not free vision.
        /// </summary>
        public void BroadcastAlert(EnemyController source, Vector3 position)
        {
            var alerted = 0;

            foreach (var member in members)
            {
                if (member == null || member == source || member.State == EnemyState.Dead)
                {
                    continue;
                }

                if ((member.transform.position - position).sqrMagnitude > alertRadius * alertRadius)
                {
                    continue;
                }

                member.NotifyDisturbance(position);
                alerted++;
            }

            if (alerted > 0)
            {
                GameLogger.Log(LogCategory.AI, $"{source?.name} alerted {alerted} ally/allies at {position}.", this);
            }
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(int simultaneousAttackers, float radius = 20f)
        {
            baseSimultaneousAttackers = Mathf.Max(1, simultaneousAttackers);
            alertRadius = radius;
        }
    }
}
