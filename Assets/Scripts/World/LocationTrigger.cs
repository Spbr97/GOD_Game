using Game.Core;
using Game.Quests;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A volume that reports a quest objective and/or sets a flag when the player
    /// enters it — the "reach location" objective type (SPEC.md section 23).
    ///
    /// It knows nothing about which quest it serves, so the same volume can satisfy
    /// several quests and can be authored before any quest references it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LocationTrigger : MonoBehaviour
    {
        [Tooltip("Name used in logs.")]
        [SerializeField] private string locationName = "Location";

        [Tooltip("Objective id reported on entry. Optional.")]
        [SerializeField] private string objectiveId;

        [Tooltip("Flag set on entry. Optional.")]
        [SerializeField] private string flagToSet;

        [Tooltip("Quest started on entry, by id from the QuestManager catalogue. Optional.")]
        [SerializeField] private string questToStart;

        [Tooltip("All of these flags must be set for the trigger to fire.")]
        [SerializeField] private string[] requiredFlags;

        [Tooltip("Fire only the first time the player enters.")]
        [SerializeField] private bool once = true;

        [SerializeField] private LayerMask playerLayers = ~0;

        public bool HasFired { get; private set; }
        public string LocationName => locationName;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (once && HasFired)
            {
                return;
            }

            if ((playerLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            // Matches how Checkpoint identifies the player, rather than a tag: the
            // player is the thing that can die and respawn.
            if (other.GetComponentInParent<Game.Combat.PlayerDeath>() == null)
            {
                return;
            }

            Fire();
        }

        /// <summary>Fires the trigger. Public so tests and debug tools can drive it.</summary>
        public bool Fire()
        {
            if (once && HasFired)
            {
                return false;
            }

            if (WorldState.Instance != null && !WorldState.Instance.HasAllFlags(requiredFlags))
            {
                return false;
            }

            HasFired = true;
            GameLogger.Log(LogCategory.Quest, $"Player reached '{locationName}'.", this);

            if (!string.IsNullOrEmpty(flagToSet))
            {
                WorldState.Instance?.SetFlag(flagToSet);
            }

            // Started before the objective is reported, so a trigger can both begin a
            // quest and satisfy its first step in one entry.
            if (!string.IsNullOrEmpty(questToStart))
            {
                QuestManager.Instance?.StartQuest(questToStart);
            }

            if (!string.IsNullOrEmpty(objectiveId))
            {
                QuestManager.Instance?.ReportObjective(objectiveId);
            }

            return true;
        }

        /// <summary>Test and tooling seam for configuring without the Inspector.</summary>
        public void Configure(string displayName, string objective, string flag, bool fireOnce = true,
            string questId = null)
        {
            locationName = displayName;
            objectiveId = objective;
            flagToSet = flag;
            once = fireOnce;
            questToStart = questId;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = HasFired ? new Color(0.3f, 1f, 0.5f, 0.25f) : new Color(1f, 0.8f, 0.2f, 0.25f);
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}
