using Game.Core;
using Game.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The on-screen quest tracker: the active quest's title and its current objective
    /// (SPEC.md sections 23 and 42).
    ///
    /// It tracks one quest — whichever started most recently — rather than listing
    /// all of them, because TASK 003 has a single quest line and a full quest log is
    /// its own UI task.
    /// </summary>
    public class QuestTrackerUI : MonoBehaviour
    {
        [SerializeField] private GameObject trackerRoot;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text objectiveLabel;

        [Tooltip("How long the completed line stays up before the tracker hides.")]
        [SerializeField] private float completedLingerSeconds = 3f;

        private string trackedQuestId;
        private float hideAt;

        private void Awake()
        {
            SetVisible(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<QuestStartedEvent>(OnQuestStarted);
            EventBus.Subscribe<QuestObjectiveCompletedEvent>(OnObjectiveCompleted);
            EventBus.Subscribe<QuestObjectiveAdvancedEvent>(OnObjectiveAdvanced);
            EventBus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Subscribe<QuestFailedEvent>(OnQuestFailed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<QuestStartedEvent>(OnQuestStarted);
            EventBus.Unsubscribe<QuestObjectiveCompletedEvent>(OnObjectiveCompleted);
            EventBus.Unsubscribe<QuestObjectiveAdvancedEvent>(OnObjectiveAdvanced);
            EventBus.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Unsubscribe<QuestFailedEvent>(OnQuestFailed);
        }

        private void Update()
        {
            if (hideAt > 0f && Time.unscaledTime >= hideAt)
            {
                hideAt = 0f;
                trackedQuestId = null;
                SetVisible(false);
            }
        }

        private void OnQuestStarted(QuestStartedEvent started)
        {
            trackedQuestId = started.Quest.QuestId;
            hideAt = 0f;

            if (titleLabel != null)
            {
                titleLabel.text = started.Quest.Title;
            }

            Refresh();
            SetVisible(true);
        }

        private void OnObjectiveCompleted(QuestObjectiveCompletedEvent completed)
        {
            if (completed.Quest.QuestId == trackedQuestId)
            {
                Refresh();
            }
        }

        private void OnObjectiveAdvanced(QuestObjectiveAdvancedEvent advanced)
        {
            if (advanced.Quest.QuestId == trackedQuestId)
            {
                Refresh();
            }
        }

        private void OnQuestCompleted(QuestCompletedEvent completed)
        {
            if (completed.Quest.QuestId != trackedQuestId)
            {
                return;
            }

            if (objectiveLabel != null)
            {
                objectiveLabel.text = "Complete";
            }

            hideAt = Time.unscaledTime + completedLingerSeconds;
        }

        private void OnQuestFailed(QuestFailedEvent failed)
        {
            if (failed.Quest.QuestId != trackedQuestId)
            {
                return;
            }

            if (objectiveLabel != null)
            {
                objectiveLabel.text = "Failed";
            }

            hideAt = Time.unscaledTime + completedLingerSeconds;
        }

        private void Refresh()
        {
            if (objectiveLabel == null || QuestManager.Instance == null)
            {
                return;
            }

            var progress = QuestManager.Instance.GetProgress(trackedQuestId);
            var objective = progress?.CurrentObjective;

            if (objective == null)
            {
                objectiveLabel.text = string.Empty;
                return;
            }

            var required = Mathf.Max(1, objective.RequiredCount);
            objectiveLabel.text = required > 1
                ? $"{objective.Description}  ({progress.GetCount(objective.ObjectiveId)}/{required})"
                : objective.Description;
        }

        private void SetVisible(bool visible)
        {
            if (trackerRoot != null)
            {
                trackerRoot.SetActive(visible);
            }
        }
    }
}
