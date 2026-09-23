using Game.Combat;
using UnityEngine;

namespace Game.Quests
{
    /// <summary>Completes a survive objective after the player stays alive for the authored duration.</summary>
    public class SurviveObjective : MonoBehaviour
    {
        [SerializeField] private string objectiveId;
        [Min(0.1f)] [SerializeField] private float seconds = 30f;
        private float elapsed;
        private bool reported;
        private PlayerDeath player;

        private void Update()
        {
            if (reported) return;
            if (!HasActiveObjective()) { elapsed = 0f; return; }
            if (player == null) player = FindAnyObjectByType<PlayerDeath>();
            if (player == null || player.IsDead) { elapsed = 0f; return; }
            elapsed += Time.deltaTime;
            if (elapsed < seconds || string.IsNullOrEmpty(objectiveId)) return;
            reported = QuestManager.Instance != null && QuestManager.Instance.ReportObjective(objectiveId);
        }

        private bool HasActiveObjective()
        {
            var quests = QuestManager.Instance;
            if (quests == null || string.IsNullOrEmpty(objectiveId)) return false;
            foreach (var progress in quests.ActiveQuests.Values)
                if (progress.Definition.GetObjective(objectiveId) != null
                    && !progress.IsObjectiveComplete(objectiveId)) return true;
            return false;
        }

        public void Configure(string id, float duration)
        {
            objectiveId = id;
            seconds = Mathf.Max(0.1f, duration);
        }
    }
}