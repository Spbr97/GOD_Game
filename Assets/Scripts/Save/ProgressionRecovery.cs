using System.Collections.Generic;
using Game.Core;
using Game.Inventory;
using Game.Quests;
using Game.World;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// Raised for each thing a recovery pass had to put right. Carries what was wrong and
    /// what was done about it, so a log line, a test and — if it ever earns one — a UI
    /// notice all read the same record.
    /// </summary>
    public readonly struct ProgressionRecoveredEvent
    {
        public readonly string Subject;
        public readonly string Problem;
        public readonly string Action;

        public ProgressionRecoveredEvent(string subject, string problem, string action)
        {
            Subject = subject;
            Problem = problem;
            Action = action;
        }
    }

    /// <summary>
    /// The anti-softlock sweep, run after a save has been applied (SPEC.md section 55:
    /// no required action may become permanently unavailable; TASK 041).
    ///
    /// This is not a substitute for the individual restore paths — <c>ItemPickup</c>,
    /// <c>MemoryToll</c>, <c>PuzzleGate</c> and <c>EnemyHealth</c> each put themselves
    /// back from their own world flag, and that is where correct restoration happens. This
    /// runs afterwards and asks a narrower, blunter question: is the player now in a state
    /// they cannot get out of? Two states qualify today, both concerning things marked
    /// <c>essential</c> by content:
    ///
    /// <list type="number">
    /// <item>An essential item recorded as collected that the inventory does not hold.
    /// Either something removed it, or the save was written by a build where the pickup
    /// granted a different item. Re-granting one is safe — the item is essential, so a
    /// spare cannot break anything — and refusing to is a dead save.</item>
    /// <item>An essential door recorded as open that is standing shut. Its own restore
    /// path covers the ordinary case; this covers a door whose <c>SaveIdentity</c> was
    /// added after the save was written, or whose puzzle lives in a scene that is not
    /// loaded.</item>
    /// <item>An active quest whose objective the world already records as done. This one
    /// needs no <c>essential</c> marking, because the situation is unambiguous on its own
    /// evidence: the flag an objective sets when it completes is set, and the journal
    /// still lists the objective as outstanding. Whatever would have reported it — the
    /// enemy that had to die, the trigger that had to be walked through, the pickup that
    /// had to be taken — restored itself from that same flag and will therefore never
    /// report again. The quest is unfinishable and the player is told to go and do
    /// something that is already done.</item>
    /// </list>
    ///
    /// It only ever opens doors and grants items — it never takes anything away and never
    /// closes anything. A recovery rule that can confiscate is a recovery rule that can
    /// itself cause the softlock, and a false positive would then be unrecoverable rather
    /// than merely untidy. Every action is logged, because a recovery that fires in normal
    /// play is a bug somewhere else and the log is how it gets found.
    /// </summary>
    public static class ProgressionRecovery
    {
        /// <summary>
        /// Runs the sweep over the loaded scenes. Returns what it did, newest last, empty
        /// when nothing needed putting right — which is the expected result.
        /// </summary>
        public static List<string> Run()
        {
            var actions = new List<string>();

            RecoverEssentialItems(actions);
            RecoverEssentialDoors(actions);
            RecoverStrandedObjectives(actions);

            return actions;
        }

        /// <summary>
        /// Completes objectives the world says are already done.
        ///
        /// Deliberately driven from the objective's own <c>CompletionFlag</c> rather than
        /// from anything cleverer. That flag is the only record that survives independently
        /// of the quest journal, so it is the only evidence that can contradict it — and a
        /// contradiction in that direction can only mean the journal is behind. The
        /// opposite case, a journal ahead of the world, is left alone: it is not a
        /// softlock, and "un-completing" an objective is exactly the confiscation this
        /// class refuses to do.
        /// </summary>
        private static void RecoverStrandedObjectives(List<string> actions)
        {
            var quests = QuestManager.Instance;
            var world = WorldState.Instance;

            if (quests == null || world == null)
            {
                return;
            }

            // Copied: completing an objective can finish its quest, which can start
            // another and mutate the collection being walked.
            var active = new List<QuestProgress>(quests.ActiveQuests.Values);

            for (var i = 0; i < active.Count; i++)
            {
                var progress = active[i];
                var definition = progress?.Definition;

                if (definition?.Objectives == null)
                {
                    continue;
                }

                for (var o = 0; o < definition.Objectives.Count; o++)
                {
                    var objective = definition.Objectives[o];

                    if (objective == null
                        || string.IsNullOrEmpty(objective.ObjectiveId)
                        || string.IsNullOrEmpty(objective.CompletionFlag)
                        || progress.IsObjectiveComplete(objective.ObjectiveId)
                        || !world.GetFlag(objective.CompletionFlag))
                    {
                        continue;
                    }

                    if (!quests.RecoverObjective(definition.QuestId, objective.ObjectiveId))
                    {
                        continue;
                    }

                    var line = $"completed objective '{objective.ObjectiveId}' of quest "
                               + $"'{definition.QuestId}', which the world flag "
                               + $"'{objective.CompletionFlag}' records as already done";
                    actions.Add(line);
                    GameLogger.LogWarning(LogCategory.Save, $"Progression recovery: {line}.");
                    EventBus.Publish(new ProgressionRecoveredEvent(objective.ObjectiveId,
                        "done in the world but outstanding in the journal", "marked it complete"));
                }
            }
        }

        private static void RecoverEssentialItems(List<string> actions)
        {
            var inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                return;
            }

            var pickups = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (var i = 0; i < pickups.Length; i++)
            {
                var pickup = pickups[i];
                if (pickup == null || !pickup.Essential || pickup.Item == null || string.IsNullOrEmpty(pickup.SaveId))
                {
                    continue;
                }

                if (!WorldObjectState.IsMarked(WorldObjectState.CollectedFlag(pickup.SaveId)))
                {
                    // Not collected yet: the pickup is still standing there to be taken,
                    // which is not a softlock.
                    continue;
                }

                if (inventory.GetCount(pickup.Item) > 0)
                {
                    continue;
                }

                inventory.Add(pickup.Item, 1);

                var line = $"re-granted the essential item '{pickup.Item.ItemId}', "
                           + $"recorded as collected from '{pickup.SaveId}' but absent from the inventory";
                actions.Add(line);
                GameLogger.LogWarning(LogCategory.Save, $"Progression recovery: {line}.", pickup);
                EventBus.Publish(new ProgressionRecoveredEvent(pickup.Item.ItemId,
                    "collected but not held", "granted one"));
            }
        }

        private static void RecoverEssentialDoors(List<string> actions)
        {
            var gates = Object.FindObjectsByType<PuzzleGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (var i = 0; i < gates.Length; i++)
            {
                var gate = gates[i];
                if (gate == null || !gate.Essential || gate.IsOpen)
                {
                    continue;
                }

                // The puzzle's own controller names the flag its solve is remembered as;
                // the id of the puzzle and the name of the flag are authored separately, so
                // one cannot be derived from the other.
                var controller = PuzzleController.Find(gate.PuzzleId);
                var solved = controller != null
                             && !string.IsNullOrEmpty(controller.SolvedFlag)
                             && WorldState.Instance != null
                             && WorldState.Instance.GetFlag(controller.SolvedFlag);

                var opened = !string.IsNullOrEmpty(gate.SaveId)
                             && WorldObjectState.IsMarked(WorldObjectState.OpenedFlag(gate.SaveId));

                if (!solved && !opened)
                {
                    continue;
                }

                gate.Open();

                var line = $"opened the essential gate '{gate.name}' for puzzle '{gate.PuzzleId}', "
                           + "which the save records as already passed";
                actions.Add(line);
                GameLogger.LogWarning(LogCategory.Save, $"Progression recovery: {line}.", gate);
                EventBus.Publish(new ProgressionRecoveredEvent(gate.name, "recorded open but shut", "opened it"));
            }
        }
    }
}
