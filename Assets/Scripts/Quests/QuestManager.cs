using System.Collections.Generic;
using Game.Core;
using Game.Dialogue;
using UnityEngine;

namespace Game.Quests
{
    /// <summary>
    /// Tracks which quests are active and how far through them the player is
    /// (SPEC.md section 23, TASK 003).
    ///
    /// Nothing in the world knows a quest's shape. Triggers, NPCs and memory pickups
    /// report "objective X happened" and this decides whether that advances anything,
    /// which is what lets one trigger volume serve several quests.
    ///
    /// The dependency on Dialogue is one-way and data-only: this subscribes to
    /// Dialogue's consequence payload, and Dialogue knows nothing about quests.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        private static QuestManager instance;

        /// <summary>Resolved on first access; see <see cref="SceneSingleton"/>.</summary>
        public static QuestManager Instance => SceneSingleton.Resolve(ref instance);

        [Tooltip("Quests started as soon as the game begins. Usually just the opening quest.")]
        [SerializeField] private QuestDefinition[] autoStartQuests;

        [Tooltip("Quests that may be started by id from dialogue. Only listed quests can be started this way.")]
        [SerializeField] private QuestDefinition[] questCatalogue;

        private readonly Dictionary<string, QuestProgress> active = new();
        private readonly Dictionary<string, QuestProgress> finished = new();

        public IReadOnlyDictionary<string, QuestProgress> ActiveQuests => active;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                GameLogger.LogWarning(LogCategory.Quest, "A second QuestManager was destroyed.", this);
                Destroy(this);
                return;
            }

            instance = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueConsequenceEvent>(OnDialogueConsequence);
            EventBus.Subscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueConsequenceEvent>(OnDialogueConsequence);
            EventBus.Unsubscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
        }

        private void Start()
        {
            if (autoStartQuests == null)
            {
                return;
            }

            for (var i = 0; i < autoStartQuests.Length; i++)
            {
                StartQuest(autoStartQuests[i]);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>Starts a quest by id, looked up in the catalogue. Used by dialogue consequences.</summary>
        public bool StartQuest(string questId)
        {
            var definition = FindInCatalogue(questId);
            if (definition == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Quest,
                    $"could not start quest '{questId}'",
                    "QuestManager.StartQuest",
                    "no quest with that id is listed in the quest catalogue",
                    "nothing is started; the conversation continues normally",
                    this);
                return false;
            }

            return StartQuest(definition);
        }

        public bool StartQuest(QuestDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            var id = definition.QuestId;

            // Re-giving a quest must not reset progress: the player can re-enter a
            // conversation that hands it out (SPEC.md section 55, anti-softlock).
            if (active.ContainsKey(id) || finished.ContainsKey(id))
            {
                return false;
            }

            if (WorldState.Instance != null && !WorldState.Instance.HasAllFlags(definition.RequiredFlags))
            {
                GameLogger.Log(LogCategory.Quest, $"Quest '{id}' not started: its required flags are not set.", this);
                return false;
            }

            var progress = new QuestProgress(definition);
            active[id] = progress;

            GameLogger.Log(LogCategory.Quest, $"Quest started: {definition.Title} ({id}).", this);
            EventBus.Publish(new QuestStartedEvent(definition));

            // An objective already satisfied when the quest starts would otherwise
            // leave the log showing a step the player cannot repeat.
            CheckCompletion(progress);
            return true;
        }

        /// <summary>
        /// Reports that something matching this objective id happened. Safe to call for
        /// ids no active quest uses, so world triggers need no knowledge of quest state.
        /// </summary>
        public bool ReportObjective(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId))
            {
                return false;
            }

            var advanced = false;

            // Copied because completing a quest can start another and mutate the
            // dictionary while it is being walked.
            var snapshot = new List<QuestProgress>(active.Values);
            for (var i = 0; i < snapshot.Count; i++)
            {
                advanced |= ReportObjective(snapshot[i], objectiveId);
            }

            return advanced;
        }

        private bool ReportObjective(QuestProgress progress, string objectiveId)
        {
            if (progress.Status != QuestStatus.Active || progress.IsObjectiveComplete(objectiveId))
            {
                return false;
            }

            var objective = progress.Definition.GetObjective(objectiveId);
            if (objective == null)
            {
                return false;
            }

            var count = progress.Increment(objectiveId);
            EventBus.Publish(new QuestObjectiveAdvancedEvent(progress.Definition, objective, count));

            if (count < Mathf.Max(1, objective.RequiredCount))
            {
                GameLogger.Log(
                    LogCategory.Quest,
                    $"Objective '{objectiveId}' progressed {count}/{objective.RequiredCount}.",
                    this);
                return true;
            }

            progress.MarkComplete(objectiveId);
            GameLogger.Log(LogCategory.Quest, $"Objective complete: {objective.Description} ({objectiveId}).", this);

            if (!string.IsNullOrEmpty(objective.CompletionFlag))
            {
                WorldState.Instance?.SetFlag(objective.CompletionFlag);
            }

            EventBus.Publish(new QuestObjectiveCompletedEvent(progress.Definition, objective));
            CheckCompletion(progress);
            return true;
        }

        public QuestProgress GetProgress(string questId)
        {
            if (string.IsNullOrEmpty(questId))
            {
                return null;
            }

            if (active.TryGetValue(questId, out var progress))
            {
                return progress;
            }

            return finished.TryGetValue(questId, out var done) ? done : null;
        }

        public QuestStatus GetStatus(string questId)
        {
            var progress = GetProgress(questId);
            return progress?.Status ?? QuestStatus.NotStarted;
        }

        public void FailQuest(string questId, string reason)
        {
            if (!active.TryGetValue(questId, out var progress))
            {
                return;
            }

            progress.Status = QuestStatus.Failed;
            active.Remove(questId);
            finished[questId] = progress;

            GameLogger.LogWarning(LogCategory.Quest, $"Quest failed: {progress.Definition.Title} — {reason}.", this);
            EventBus.Publish(new QuestFailedEvent(progress.Definition, reason));
        }

        private void CheckCompletion(QuestProgress progress)
        {
            if (progress.Status != QuestStatus.Active || !progress.AllRequiredObjectivesComplete)
            {
                return;
            }

            var definition = progress.Definition;
            progress.Status = QuestStatus.Completed;
            active.Remove(definition.QuestId);
            finished[definition.QuestId] = progress;

            var completionFlags = definition.CompletionFlags;
            if (completionFlags != null && WorldState.Instance != null)
            {
                for (var i = 0; i < completionFlags.Count; i++)
                {
                    WorldState.Instance.SetFlag(completionFlags[i]);
                }
            }

            GameLogger.Log(LogCategory.Quest, $"Quest complete: {definition.Title} ({definition.QuestId}).", this);
            EventBus.Publish(new QuestCompletedEvent(definition));
        }

        private void OnDialogueConsequence(DialogueConsequenceEvent consequence)
        {
            switch (consequence.Type)
            {
                case ConsequenceType.StartQuest:
                    StartQuest(consequence.Target);
                    break;

                case ConsequenceType.CompleteObjective:
                    ReportObjective(consequence.Target);
                    break;
            }
        }

        private void OnWorldFlagChanged(WorldFlagChangedEvent changed)
        {
            if (!changed.Value)
            {
                return;
            }

            var snapshot = new List<QuestProgress>(active.Values);
            for (var i = 0; i < snapshot.Count; i++)
            {
                var failureFlags = snapshot[i].Definition.FailureFlags;
                if (failureFlags == null)
                {
                    continue;
                }

                for (var f = 0; f < failureFlags.Count; f++)
                {
                    if (failureFlags[f] == changed.Flag)
                    {
                        FailQuest(snapshot[i].Definition.QuestId, $"failure flag '{changed.Flag}' was set");
                        break;
                    }
                }
            }
        }

        private QuestDefinition FindInCatalogue(string questId)
        {
            if (questCatalogue == null || string.IsNullOrEmpty(questId))
            {
                return null;
            }

            for (var i = 0; i < questCatalogue.Length; i++)
            {
                if (questCatalogue[i] != null && questCatalogue[i].QuestId == questId)
                {
                    return questCatalogue[i];
                }
            }

            return null;
        }

        /// <summary>Test and tooling seam for supplying quests without the Inspector.</summary>
        public void Configure(QuestDefinition[] catalogue, QuestDefinition[] autoStart = null)
        {
            questCatalogue = catalogue;
            autoStartQuests = autoStart;
        }
    }
}
