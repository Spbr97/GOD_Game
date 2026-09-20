using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.Quests
{
    /// <summary>
    /// Marks an entity as counting towards a quest objective when it dies — the
    /// driver <see cref="ObjectiveType.DefeatEnemy"/> was missing after TASK 003.
    ///
    /// It lives in the quest system rather than in AI so that nothing in
    /// <c>Game.AI</c> has to know quest ids exist (SPEC.md section 58 rule 7). The
    /// objective id is serialized scene data, exactly as on
    /// <see cref="Game.World.LocationTrigger"/>.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class QuestTarget : MonoBehaviour
    {
        [Tooltip("Objective reported when this entity dies. One report per death.")]
        [SerializeField] private string objectiveId;

        [Tooltip("World flag set when this entity dies. Optional.")]
        [SerializeField] private string flagToSet;

        private HealthComponent health;
        private bool reported;

        public bool HasReported => reported;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        /// <summary>
        /// Reports the kill. Public so a scripted death or a test can drive it, and
        /// guarded so a revived-then-killed enemy cannot count twice (SPEC.md section
        /// 56: no duplicate rewards).
        /// </summary>
        public bool Report()
        {
            if (reported)
            {
                return false;
            }

            reported = true;

            if (!string.IsNullOrEmpty(flagToSet))
            {
                WorldState.Instance?.SetFlag(flagToSet);
            }

            if (!string.IsNullOrEmpty(objectiveId))
            {
                QuestManager.Instance?.ReportObjective(objectiveId);
            }

            return true;
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(string objective, string flag = null)
        {
            objectiveId = objective;
            flagToSet = flag;
        }

        private void HandleDied(DamageData killingBlow)
        {
            Report();
        }
    }
}
