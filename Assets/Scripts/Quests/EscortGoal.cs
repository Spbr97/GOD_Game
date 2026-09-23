using UnityEngine;

namespace Game.Quests
{
    /// <summary>Reports an escort objective when its assigned actor reaches this trigger.</summary>
    [RequireComponent(typeof(Collider))]
    public class EscortGoal : MonoBehaviour
    {
        [SerializeField] private Transform escort;
        [SerializeField] private string objectiveId;
        private bool reported;

        private void OnTriggerEnter(Collider other) => TryReport(other);
        private void OnTriggerStay(Collider other) => TryReport(other);

        private void TryReport(Collider other)
        {
            if (reported || escort == null || other == null || !other.transform.IsChildOf(escort)
                || string.IsNullOrEmpty(objectiveId)) return;
            reported = QuestManager.Instance != null && QuestManager.Instance.ReportObjective(objectiveId);
        }

        public void Configure(Transform actor, string id)
        {
            escort = actor;
            objectiveId = id;
            GetComponent<Collider>().isTrigger = true;
        }
    }
}