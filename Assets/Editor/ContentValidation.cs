using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.AI;
using Game.Core;
using Game.Inventory;
using Game.Memory;
using Game.Progression;
using Game.Quests;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    public enum ContentSeverity
    {
        /// <summary>Worth a look. Does not stop a build.</summary>
        Warning,

        /// <summary>Broken content. Stops a build.</summary>
        Error
    }

    /// <summary>
    /// One thing wrong with the authored content. A record rather than a log line so a
    /// test, a menu item and a build can all read the same result and decide separately
    /// what to do about it.
    /// </summary>
    public readonly struct ContentIssue
    {
        public readonly ContentSeverity Severity;

        /// <summary>A short slug naming the rule, so an expected failure can be matched on without matching prose.</summary>
        public readonly string Rule;

        /// <summary>What is wrong with it — the asset, scene object or id.</summary>
        public readonly string Subject;

        public readonly string Message;

        public readonly Object Context;

        public ContentIssue(ContentSeverity severity, string rule, string subject, string message, Object context = null)
        {
            Severity = severity;
            Rule = rule;
            Subject = subject;
            Message = message;
            Context = context;
        }

        public override string ToString() =>
            $"{(Severity == ContentSeverity.Error ? "ERROR" : "warn ")} [{Rule}] {Subject}: {Message}";
    }

    /// <summary>
    /// Checks that the authored content hangs together (TASK 042).
    ///
    /// Almost everything in this project is joined by a string. A quest objective is
    /// completed by whatever reports its id; a dialogue node starts a quest by naming it;
    /// a door names a scene and an arrival point; a gate names a puzzle. That is the right
    /// design — it is what lets content be authored without recompiling, and what lets two
    /// scenes refer to each other at all — but it means the compiler checks none of it. A
    /// typo is not a build error. It is a quest that can never complete, found by a player.
    ///
    /// This is the thing that checks them. It is deliberately not a test: it runs from the
    /// menu while authoring, from a test in CI, and from <c>WindowsBuild</c> before a
    /// player is produced, and those three want the same rules with different reactions.
    ///
    /// **Errors stop a build; warnings do not.** The split is by whether a player would be
    /// stuck. A dangling objective id is an error because the quest cannot finish. An
    /// unknown flag is a warning because a flag nothing sets is usually content that has
    /// not been written yet, which is a normal state for a project mid-production and a
    /// terrible reason to refuse to build.
    /// </summary>
    public static class ContentValidation
    {
        /// <summary>Scenes checked by <see cref="ValidateScenes"/>. The developer arena is not shipped and is not content.</summary>
        public static readonly string[] GameplayScenes =
        {
            "Assets/Scenes/Avarsha.unity",
            "Assets/Scenes/Agniya.unity"
        };

        // ------------------------------------------------------------------- entry points

        [MenuItem("God Game/Validate Content")]
        public static void ValidateFromMenu()
        {
            var issues = ValidateAll();
            Report(issues, "Content validation");
        }

        /// <summary>
        /// Every rule: the asset catalogues and the shipped scenes. Restores whatever
        /// scenes were open, so running it from the menu does not cost the author their
        /// workspace.
        /// </summary>
        public static List<ContentIssue> ValidateAll()
        {
            var issues = ValidateAssets();
            issues.AddRange(ValidateScenes());
            return issues;
        }

        /// <summary>
        /// The asset catalogues only. Opens no scenes, so this is safe to call from
        /// anywhere, including from a test that is mid-scene.
        /// </summary>
        public static List<ContentIssue> ValidateAssets()
        {
            var quests = LoadAll<QuestDefinition>();
            var memories = LoadAll<MemoryFragment>();
            var items = LoadAll<InventoryItem>();
            var skills = LoadAll<SkillDefinition>();
            var archetypes = LoadAll<EnemyArchetype>();

            return ValidateCatalogue(new ContentCatalogue(quests, memories, items, skills, archetypes));
        }

        /// <summary>
        /// The asset rules, over a catalogue the caller supplies. Exists so a test can
        /// feed the validator deliberately broken content without writing an asset to
        /// disk — and a test that writes a broken asset to disk is one crash away from
        /// leaving it there for everyone.
        /// </summary>
        public static List<ContentIssue> ValidateCatalogue(ContentCatalogue catalogue)
        {
            var issues = new List<ContentIssue>();

            CheckUniqueIds(issues, catalogue);
            CheckQuests(issues, catalogue.Quests, catalogue);
            CheckMemories(issues, catalogue.Memories, catalogue);
            CheckSkills(issues, catalogue.Skills);
            CheckEndings(issues, catalogue);
            CheckLocalizationKeys(issues, catalogue);

            issues.AddRange(DialogueValidation.Validate(catalogue));

            return issues;
        }

        /// <summary>
        /// Localization keys on content assets (TASK 040's localization decision).
        ///
        /// Two things can go wrong once assets carry keys, and only one of them is
        /// serious.
        ///
        /// **Two assets sharing a key is an error.** They would both read the same
        /// translated title, and the symptom — two quests with the same name in the
        /// journal — looks like a content mistake rather than a localization one, so it
        /// would be looked for in the wrong place.
        ///
        /// **A key with nothing translated under it is a warning**, and deliberately not
        /// an error. That is the normal state of a project mid-translation, and the
        /// fallback means the player still reads the authored text. Making it an error
        /// would mean an author could not add a key until a translator had caught up,
        /// which is backwards — the key is what tells the translator the asset exists.
        ///
        /// Checked against the English table, because that is the one that must be
        /// complete. Whether another language is complete is a question for whoever
        /// commissioned it.
        /// </summary>
        private static void CheckLocalizationKeys(List<ContentIssue> issues, ContentCatalogue catalogue)
        {
            var english = Resources.Load<Game.Core.Localization.StringTable>("Localization/Strings_en");
            var seen = new Dictionary<string, string>();

            void Check(string key, string subject, UnityEngine.Object asset, params string[] fields)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    // Not localized. Reads as authored, which is valid and is the state
                    // every asset in the project is in today.
                    return;
                }

                if (seen.TryGetValue(key, out var owner))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "localization-key", subject,
                        $"uses localization key '{key}', which '{owner}' already uses. Both would show the "
                        + "same translated text.", asset));
                    return;
                }

                seen[key] = subject;

                if (english == null)
                {
                    return;
                }

                foreach (var field in fields)
                {
                    var full = Game.Core.Localization.LocalizedContent.KeyFor(key, field);

                    if (!english.Has(full))
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Warning, "localization-key", subject,
                            $"declares localization key '{key}' but Strings_en has no '{full}'. The asset's "
                            + "own text is shown instead, so this is a to-do rather than a break.", asset));
                    }
                }
            }

            foreach (var quest in catalogue.Quests)
            {
                if (quest != null)
                {
                    var fields = new List<string> { "title", "description" };
                    if (!string.IsNullOrEmpty(quest.AuthoredRewardsSummary))
                    {
                        fields.Add("rewards");
                    }

                    if (quest.Objectives != null)
                    {
                        foreach (var objective in quest.Objectives)
                        {
                            if (objective != null && !string.IsNullOrEmpty(objective.ObjectiveId))
                            {
                                fields.Add("objective." + objective.ObjectiveId.ToLowerInvariant());
                            }
                        }
                    }

                    Check(quest.LocalizationKey, quest.QuestId, quest, fields.ToArray());
                }
            }

            foreach (var memory in catalogue.Memories)
            {
                if (memory != null)
                {
                    Check(memory.LocalizationKey, memory.MemoryId, memory, "title", "description");
                }
            }

            foreach (var item in catalogue.Items)
            {
                if (item != null)
                {
                    Check(item.LocalizationKey, item.ItemId, item,
                        string.IsNullOrEmpty(item.AuthoredDescription)
                            ? new[] { "name" } : new[] { "name", "description" });
                }
            }

            foreach (var skill in catalogue.Skills)
            {
                if (skill != null)
                {
                    Check(skill.LocalizationKey, skill.SkillId, skill,
                        string.IsNullOrEmpty(skill.AuthoredDescription)
                            ? new[] { "name" } : new[] { "name", "description" });
                }
            }
        }

        /// <summary>
        /// The shipped gameplay scenes. Opens each one, then puts back whatever was open
        /// before.
        /// </summary>
        public static List<ContentIssue> ValidateScenes()
        {
            var issues = new List<ContentIssue>();
            var catalogue = ContentCatalogue.Load();

            var previous = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                var exits = new List<(string From, string Scene, string Spawn, string Object)>();
                var scenesInBuild = new HashSet<string>();

                foreach (var entry in EditorBuildSettings.scenes)
                {
                    if (entry.enabled)
                    {
                        scenesInBuild.Add(System.IO.Path.GetFileNameWithoutExtension(entry.path));
                    }
                }

                // Two passes over the scenes. A flag set by a LocationTrigger in one scene
                // and read by a door in another is perfectly good content, so every flag
                // the scenes set has to be known before any flag reference is judged.
                // Components cannot be held across a scene close, so this is a second
                // walk rather than a saved list.
                foreach (var path in GameplayScenes)
                {
                    if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "missing-scene", path,
                            "a gameplay scene named here does not exist"));
                        continue;
                    }

                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    GatherSceneFlags(catalogue);
                }

                foreach (var path in GameplayScenes)
                {
                    if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                    {
                        continue;
                    }

                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    CheckOpenScene(issues, path, catalogue, scenesInBuild, exits);
                }

                // Done after the loop: checking a door means opening the scene it points
                // at, which closes the scene the door was found in.
                CheckExitDestinations(issues, exits);
            }
            finally
            {
                RestoreScenes(previous);
            }

            return issues;
        }

        public static bool HasErrors(IEnumerable<ContentIssue> issues) =>
            issues != null && issues.Any(issue => issue.Severity == ContentSeverity.Error);

        public static string Format(IEnumerable<ContentIssue> issues)
        {
            var text = new StringBuilder();
            foreach (var issue in issues.OrderByDescending(i => i.Severity))
            {
                text.AppendLine(issue.ToString());
            }

            return text.ToString();
        }

        /// <summary>Logs a run's result: one line per issue, then a count. Used by the menu item and by the build.</summary>
        public static void Report(IReadOnlyList<ContentIssue> issues, string heading)
        {
            var errors = 0;
            var warnings = 0;

            foreach (var issue in issues)
            {
                if (issue.Severity == ContentSeverity.Error)
                {
                    errors++;
                    Debug.LogError(issue.ToString(), issue.Context);
                }
                else
                {
                    warnings++;
                    Debug.LogWarning(issue.ToString(), issue.Context);
                }
            }

            var summary = $"{heading}: {errors} error(s), {warnings} warning(s).";

            if (errors > 0)
            {
                Debug.LogError(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        // ------------------------------------------------------------------- asset rules

        private static void CheckUniqueIds(List<ContentIssue> issues, ContentCatalogue catalogue)
        {
            CheckUnique(issues, "quest", catalogue.Quests.Select(q => (q.QuestId, (Object)q)));
            CheckUnique(issues, "memory", catalogue.Memories.Select(m => (m.MemoryId, (Object)m)));
            CheckUnique(issues, "item", catalogue.Items.Select(i => (i.ItemId, (Object)i)));
            CheckUnique(issues, "skill", catalogue.Skills.Select(s => (s.SkillId, (Object)s)));
            CheckUnique(issues, "enemy archetype", catalogue.Archetypes.Select(a => (a.ArchetypeId, (Object)a)));

            // Objective ids are reported to QuestManager by id alone, with no quest named,
            // so two quests sharing one is not a name clash — it is one objective that
            // completes a step of a quest the player may not even have started.
            var objectiveOwners = new Dictionary<string, string>();
            foreach (var quest in catalogue.Quests)
            {
                if (quest.Objectives == null)
                {
                    continue;
                }

                foreach (var objective in quest.Objectives)
                {
                    if (objective == null || string.IsNullOrWhiteSpace(objective.ObjectiveId))
                    {
                        continue;
                    }

                    if (objectiveOwners.TryGetValue(objective.ObjectiveId, out var owner))
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "duplicate-id", objective.ObjectiveId,
                            $"is an objective of both '{owner}' and '{quest.QuestId}'; reporting it would advance both",
                            quest));
                    }
                    else
                    {
                        objectiveOwners[objective.ObjectiveId] = quest.QuestId;
                    }
                }
            }
        }

        private static void CheckUnique(List<ContentIssue> issues, string kind,
            IEnumerable<(string Id, Object Asset)> entries)
        {
            var seen = new Dictionary<string, Object>();

            foreach (var (id, asset) in entries)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "missing-id", AssetName(asset),
                        $"this {kind} has no id", asset));
                    continue;
                }

                if (seen.TryGetValue(id, out var first))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "duplicate-id", id,
                        $"two {kind} assets share this id: '{AssetName(first)}' and '{AssetName(asset)}'", asset));
                }
                else
                {
                    seen[id] = asset;
                }
            }
        }

        private static void CheckQuests(List<ContentIssue> issues, IReadOnlyList<QuestDefinition> quests,
            ContentCatalogue catalogue)
        {
            foreach (var quest in quests)
            {
                var subject = quest.QuestId;

                if (quest.Objectives == null || quest.Objectives.Count == 0)
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "quest-shape", subject,
                        "has no objectives, so it can start but never finish", quest));
                }

                if (string.IsNullOrWhiteSpace(quest.Title))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Warning, "quest-shape", subject,
                        "has no title, so the journal will show an empty row", quest));
                }

                if (quest.Objectives != null)
                {
                    var blocking = 0;

                    foreach (var objective in quest.Objectives)
                    {
                        if (objective == null)
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Error, "quest-shape", subject,
                                "has an empty objective slot", quest));
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(objective.ObjectiveId))
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Error, "missing-id", subject,
                                "has an objective with no id, which nothing can ever report", quest));
                            continue;
                        }

                        if (objective.RequiredCount < 1)
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Error, "quest-shape", objective.ObjectiveId,
                                $"needs to be reported {objective.RequiredCount} times, which is impossible", quest));
                        }

                        if (string.IsNullOrWhiteSpace(objective.Description) && !objective.Hidden)
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Warning, "quest-shape", objective.ObjectiveId,
                                "is visible in the journal but has no description", quest));
                        }

                        if (!objective.Optional)
                        {
                            blocking++;
                        }
                    }

                    if (quest.Objectives.Count > 0 && blocking == 0)
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "quest-shape", subject,
                            "every objective is optional, so nothing the player does completes the quest", quest));
                    }
                }

                CheckRewards(issues, quest, catalogue);
            }
        }

        private static void CheckRewards(List<ContentIssue> issues, QuestDefinition quest, ContentCatalogue catalogue)
        {
            var subject = quest.QuestId;
            var rewards = quest.Rewards;

            if ((rewards == null || rewards.Count == 0) && !string.IsNullOrWhiteSpace(quest.RewardsSummary))
            {
                issues.Add(new ContentIssue(ContentSeverity.Warning, "quest-reward", subject,
                    $"promises \"{quest.RewardsSummary}\" in prose but grants nothing", quest));
                return;
            }

            if (rewards == null)
            {
                return;
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];

                if (reward == null)
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "quest-reward", subject,
                        $"reward {i} is an empty slot", quest));
                    continue;
                }

                switch (reward.Type)
                {
                    case QuestRewardType.Item:
                        if (reward.Item == null)
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Error, "quest-reward", subject,
                                $"reward {i} grants an item but no item is assigned", quest));
                        }

                        if (reward.Amount < 1)
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Error, "quest-reward", subject,
                                $"reward {i} grants {reward.Amount} of an item", quest));
                        }

                        break;

                    case QuestRewardType.MemoryRestore:
                        if (string.IsNullOrWhiteSpace(reward.TargetId))
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Error, "quest-reward", subject,
                                $"reward {i} restores a memory but names none", quest));
                        }
                        else if (!catalogue.MemoryIds.Contains(reward.TargetId))
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", subject,
                                $"reward {i} restores memory '{reward.TargetId}', which does not exist", quest));
                        }

                        break;

                    case QuestRewardType.AbilityUnlock:
                    case QuestRewardType.WorldFlag:
                        if (string.IsNullOrWhiteSpace(reward.TargetId))
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Error, "quest-reward", subject,
                                $"reward {i} is a {reward.Type} with no target", quest));
                        }

                        break;

                    case QuestRewardType.SkillPoints:
                        if (reward.Amount < 1)
                        {
                            issues.Add(new ContentIssue(ContentSeverity.Error, "quest-reward", subject,
                                $"reward {i} grants {reward.Amount} skill points", quest));
                        }

                        break;
                }
            }
        }

        private static void CheckMemories(List<ContentIssue> issues, IReadOnlyList<MemoryFragment> memories,
            ContentCatalogue catalogue)
        {
            foreach (var memory in memories)
            {
                var subject = memory.MemoryId;

                if (!string.IsNullOrWhiteSpace(memory.AssociatedQuestId)
                    && !catalogue.QuestIds.Contains(memory.AssociatedQuestId))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", subject,
                        $"belongs to quest '{memory.AssociatedQuestId}', which does not exist", memory));
                }

                if (!string.IsNullOrWhiteSpace(memory.ObjectiveIdOnDiscovery)
                    && !catalogue.ObjectiveIds.Contains(memory.ObjectiveIdOnDiscovery))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", subject,
                        $"reports objective '{memory.ObjectiveIdOnDiscovery}' on discovery, which no quest defines",
                        memory));
                }

                if (string.IsNullOrWhiteSpace(memory.Title))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Warning, "memory-shape", subject,
                        "has no title", memory));
                }
            }
        }

        private static void CheckSkills(List<ContentIssue> issues, IReadOnlyList<SkillDefinition> skills)
        {
            var byId = new Dictionary<string, SkillDefinition>();
            foreach (var skill in skills)
            {
                if (!string.IsNullOrWhiteSpace(skill.SkillId))
                {
                    byId[skill.SkillId] = skill;
                }
            }

            foreach (var skill in skills)
            {
                if (skill.Cost < 0)
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "skill-shape", skill.SkillId,
                        $"costs {skill.Cost} points", skill));
                }

                if (string.IsNullOrWhiteSpace(skill.PrerequisiteId))
                {
                    continue;
                }

                if (!byId.ContainsKey(skill.PrerequisiteId))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", skill.SkillId,
                        $"requires skill '{skill.PrerequisiteId}', which does not exist", skill));
                    continue;
                }

                // A cycle means neither skill can ever be unlocked, and the tree UI would
                // recurse forever drawing it.
                var walked = new HashSet<string> { skill.SkillId };
                var current = skill.PrerequisiteId;

                while (!string.IsNullOrEmpty(current) && byId.TryGetValue(current, out var step))
                {
                    if (!walked.Add(current))
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "skill-shape", skill.SkillId,
                            $"its prerequisite chain loops through '{current}', so it can never be unlocked", skill));
                        break;
                    }

                    current = step.PrerequisiteId;
                }
            }
        }

        /// <summary>
        /// SPEC.md sections 19 and 70 describe three endings and a hidden fourth.
        /// <c>SaveData.EndingFlags</c> is reserved and nothing writes it. This reports that
        /// as one warning rather than staying silent, because a validator that passes a
        /// game with no endings is a validator nobody should trust when the endings exist.
        /// </summary>
        private static void CheckEndings(List<ContentIssue> issues, ContentCatalogue catalogue)
        {
            var hasEndingContent = catalogue.KnownFlags.Any(flag =>
                flag.StartsWith("ENDING_", System.StringComparison.Ordinal));

            if (!hasEndingContent)
            {
                issues.Add(new ContentIssue(ContentSeverity.Warning, "ending-condition", "the game",
                    "no ending conditions are authored — no content sets any ENDING_* flag (SPEC.md section 70). "
                    + "Expected until Act V exists; this rule starts checking them the moment one appears"));
            }
        }

        // ------------------------------------------------------------------- scene rules

        private static void CheckOpenScene(List<ContentIssue> issues, string path, ContentCatalogue catalogue,
            HashSet<string> scenesInBuild, List<(string, string, string, string)> exits)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var sceneName = System.IO.Path.GetFileNameWithoutExtension(path);

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var exit in root.GetComponentsInChildren<SceneExit>(true))
                {
                    if (string.IsNullOrWhiteSpace(exit.TargetScene))
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "scene-exit", Where(path, exit),
                            "names no destination scene", exit));
                        continue;
                    }

                    if (!scenesInBuild.Contains(exit.TargetScene))
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "scene-exit", Where(path, exit),
                            $"leads to '{exit.TargetScene}', which is not an enabled scene in Build Settings", exit));
                        continue;
                    }

                    exits.Add((path, exit.TargetScene, exit.TargetSpawnId, exit.name));
                }

                foreach (var target in root.GetComponentsInChildren<QuestTarget>(true))
                {
                    CheckObjectiveReference(issues, catalogue, path, target, target.ObjectiveId, "completes");
                }

                foreach (var trigger in root.GetComponentsInChildren<LocationTrigger>(true))
                {
                    CheckObjectiveReference(issues, catalogue, path, trigger, trigger.ObjectiveId, "reports");

                    if (!string.IsNullOrWhiteSpace(trigger.ArrivalSpawnId)
                        && SceneSpawnPoint.Find(trigger.ArrivalSpawnId) == null)
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "scene-arrival", Where(path, trigger),
                            $"fires on arrival at '{trigger.ArrivalSpawnId}', which has no spawn point in '{sceneName}'",
                            trigger));
                    }

                    if (!string.IsNullOrWhiteSpace(trigger.QuestToStart)
                        && !catalogue.QuestIds.Contains(trigger.QuestToStart))
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", Where(path, trigger),
                            $"starts quest '{trigger.QuestToStart}', which does not exist", trigger));
                    }
                }

                foreach (var pickup in root.GetComponentsInChildren<ItemPickup>(true))
                {
                    CheckObjectiveReference(issues, catalogue, path, pickup, pickup.ObjectiveIdOnCollect, "reports");

                    if (pickup.Item == null)
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "pickup-shape", Where(path, pickup),
                            "is an item pickup with no item assigned, so it can never be taken", pickup));
                    }
                }

                foreach (var puzzle in root.GetComponentsInChildren<PuzzleController>(true))
                {
                    CheckObjectiveReference(issues, catalogue, path, puzzle, puzzle.ObjectiveIdOnSolve, "reports");
                }

                foreach (var boss in root.GetComponentsInChildren<BossController>(true))
                {
                    if (boss.Reward == null)
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Warning, "boss-reward", Where(path, boss),
                            $"boss '{boss.BossId}' reveals no reward on defeat (SPEC.md section 18)", boss));
                    }
                    else if (boss.Reward.activeSelf)
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "boss-reward", Where(path, boss),
                            $"boss '{boss.BossId}'s reward is already active in the scene, so the player can take it "
                            + "without fighting", boss));
                    }
                }

                foreach (var interactable in root.GetComponentsInChildren<Interactable>(true))
                {
                    CheckFlagReferences(issues, catalogue, path, interactable, interactable.RequiredFlags, "requires");
                    CheckFlagReferences(issues, catalogue, path, interactable, interactable.BlockingFlags, "is blocked by");
                }
            }

            // A gateless puzzle is content nobody can see the result of.
            var gates = Object.FindObjectsByType<PuzzleGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var controllers = Object.FindObjectsByType<PuzzleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var gate in gates)
            {
                if (string.IsNullOrWhiteSpace(gate.PuzzleId))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "puzzle-link", Where(path, gate),
                        "is a gate with no puzzle id, so nothing can ever open it", gate));
                }
                else if (controllers.All(c => c.PuzzleId != gate.PuzzleId))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "puzzle-link", Where(path, gate),
                        $"waits on puzzle '{gate.PuzzleId}', which no PuzzleController in '{sceneName}' provides",
                        gate));
                }
            }
        }

        /// <summary>
        /// Adds every flag the open scene's components set to the known set. Scene-authored
        /// flags are as real as asset-authored ones; only the compiler cannot see either.
        /// </summary>
        private static void GatherSceneFlags(ContentCatalogue catalogue)
        {
            foreach (var trigger in Object.FindObjectsByType<LocationTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                catalogue.AddKnownFlag(trigger.FlagToSet);
            }

            foreach (var target in Object.FindObjectsByType<QuestTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                catalogue.AddKnownFlag(target.FlagToSet);
            }

            foreach (var puzzle in Object.FindObjectsByType<PuzzleController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                catalogue.AddKnownFlag(puzzle.SolvedFlag);
            }

            foreach (var toll in Object.FindObjectsByType<MemoryToll>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                catalogue.AddKnownFlag(toll.PaidFlag);
            }

            foreach (var exit in Object.FindObjectsByType<SceneExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                catalogue.AddKnownFlag(exit.FlagOnUse);
            }

            foreach (var identity in Object.FindObjectsByType<SaveIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Per-object state flags are generated, not authored, so content that gates
                // on "this enemy is dead" is gating on something real.
                catalogue.AddKnownFlag(WorldObjectState.DeadFlag(identity.Id));
                catalogue.AddKnownFlag(WorldObjectState.CollectedFlag(identity.Id));
                catalogue.AddKnownFlag(WorldObjectState.PaidFlag(identity.Id));
                catalogue.AddKnownFlag(WorldObjectState.OpenedFlag(identity.Id));
            }
        }

        private static void CheckObjectiveReference(List<ContentIssue> issues, ContentCatalogue catalogue,
            string path, Component component, string objectiveId, string verb)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                return;
            }

            if (!catalogue.ObjectiveIds.Contains(objectiveId))
            {
                issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", Where(path, component),
                    $"{verb} objective '{objectiveId}', which no quest defines", component));
            }
        }

        private static void CheckFlagReferences(List<ContentIssue> issues, ContentCatalogue catalogue,
            string path, Component component, IReadOnlyList<string> flags, string verb)
        {
            if (flags == null)
            {
                return;
            }

            foreach (var flag in flags)
            {
                if (!string.IsNullOrWhiteSpace(flag) && !catalogue.KnownFlags.Contains(flag))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Warning, "unknown-flag", Where(path, component),
                        $"{verb} flag '{flag}', which nothing in the content or the engine ever sets", component));
                }
            }
        }

        private static void CheckExitDestinations(List<ContentIssue> issues,
            List<(string From, string Scene, string Spawn, string Object)> exits)
        {
            foreach (var exit in exits)
            {
                var destination = $"Assets/Scenes/{exit.Scene}.unity";

                if (AssetDatabase.LoadAssetAtPath<Object>(destination) == null)
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "scene-exit", $"{exit.From}:{exit.Object}",
                        $"leads to '{exit.Scene}', which is in Build Settings but has no scene file"));
                    continue;
                }

                EditorSceneManager.OpenScene(destination, OpenSceneMode.Single);

                if (SceneSpawnPoint.Find(exit.Spawn) == null)
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "scene-exit", $"{exit.From}:{exit.Object}",
                        $"sends the player to spawn point '{exit.Spawn}' in '{exit.Scene}', which has no such point"));
                }
            }
        }

        // ------------------------------------------------------------------------ helpers

        private static void RestoreScenes(SceneSetup[] previous)
        {
            if (previous == null)
            {
                return;
            }

            // A batch-mode run can begin with no loaded scene at all, and restoring an
            // empty setup throws.
            foreach (var setup in previous)
            {
                if (setup.isLoaded && setup.isActive)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                    return;
                }
            }
        }

        private static string Where(string scenePath, Component component) =>
            $"{System.IO.Path.GetFileNameWithoutExtension(scenePath)}/{component.name}";

        private static string AssetName(Object asset) => asset != null ? asset.name : "<missing>";

        /// <summary>
        /// Every asset of a type in the project. Public because tests over authored
        /// content — readability, difficulty tuning — need the same catalogue this does.
        /// </summary>
        public static List<T> LoadAll<T>() where T : Object
        {
            var result = new List<T>();

            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Every id and flag the authored content knows about, gathered once.
    ///
    /// Built once and passed around because each rule needs the whole set — "is this a
    /// real objective" cannot be answered by looking at one quest — and re-scanning the
    /// asset database per rule turns a fast check into a slow one nobody runs.
    /// </summary>
    public sealed class ContentCatalogue
    {
        public readonly IReadOnlyList<QuestDefinition> Quests;
        public readonly IReadOnlyList<MemoryFragment> Memories;
        public readonly IReadOnlyList<InventoryItem> Items;
        public readonly IReadOnlyList<SkillDefinition> Skills;
        public readonly IReadOnlyList<EnemyArchetype> Archetypes;
        public readonly IReadOnlyList<Game.Dialogue.DialogueGraph> Graphs;

        public readonly HashSet<string> QuestIds = new();
        public readonly HashSet<string> ObjectiveIds = new();
        public readonly HashSet<string> MemoryIds = new();
        public readonly HashSet<string> ItemIds = new();

        /// <summary>Every flag any content or engine constant sets. What "unknown flag" is measured against.</summary>
        public readonly HashSet<string> KnownFlags = new();

        public static ContentCatalogue Load() => new(
            ContentValidation.LoadAll<QuestDefinition>(),
            ContentValidation.LoadAll<MemoryFragment>(),
            ContentValidation.LoadAll<InventoryItem>(),
            ContentValidation.LoadAll<SkillDefinition>(),
            ContentValidation.LoadAll<EnemyArchetype>());

        public ContentCatalogue(IReadOnlyList<QuestDefinition> quests, IReadOnlyList<MemoryFragment> memories,
            IReadOnlyList<InventoryItem> items, IReadOnlyList<SkillDefinition> skills,
            IReadOnlyList<EnemyArchetype> archetypes)
        {
            Quests = quests;
            Memories = memories;
            Items = items;
            Skills = skills;
            Archetypes = archetypes;
            Graphs = ContentValidation.LoadAll<Game.Dialogue.DialogueGraph>();

            foreach (var quest in quests)
            {
                QuestIds.Add(quest.QuestId);
                AddFlags(quest.RequiredFlags);
                AddFlags(quest.CompletionFlags);
                AddFlags(quest.FailureFlags);

                if (quest.Objectives == null)
                {
                    continue;
                }

                foreach (var objective in quest.Objectives)
                {
                    if (objective == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(objective.ObjectiveId))
                    {
                        ObjectiveIds.Add(objective.ObjectiveId);
                    }

                    AddFlag(objective.CompletionFlag);
                }
            }

            foreach (var memory in memories)
            {
                MemoryIds.Add(memory.MemoryId);
                AddFlag(memory.DiscoveryFlag);
            }

            foreach (var item in items)
            {
                ItemIds.Add(item.ItemId);
            }

            foreach (var graph in Graphs)
            {
                if (graph.Nodes == null)
                {
                    continue;
                }

                foreach (var node in graph.Nodes)
                {
                    if (node == null)
                    {
                        continue;
                    }

                    AddConsequenceFlags(node.Consequences);

                    if (node.Choices == null)
                    {
                        continue;
                    }

                    foreach (var choice in node.Choices)
                    {
                        if (choice != null)
                        {
                            AddConsequenceFlags(choice.Consequences);
                        }
                    }
                }
            }

            // Flags the engine itself names. A scene gating on ENTERED_AVARSHA is gating on
            // something real even though no ScriptableObject mentions it.
            foreach (var field in typeof(WorldFlags).GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                {
                    AddFlag((string)field.GetRawConstantValue());
                }
            }
        }

        private void AddConsequenceFlags(Game.Dialogue.DialogueConsequence[] consequences)
        {
            if (consequences == null)
            {
                return;
            }

            foreach (var consequence in consequences)
            {
                if (consequence != null
                    && (consequence.Type == Game.Dialogue.ConsequenceType.SetFlag
                        || consequence.Type == Game.Dialogue.ConsequenceType.ClearFlag))
                {
                    AddFlag(consequence.Target);
                }
            }
        }

        private void AddFlags(IReadOnlyList<string> flags)
        {
            if (flags == null)
            {
                return;
            }

            foreach (var flag in flags)
            {
                AddFlag(flag);
            }
        }

        /// <summary>Adds a flag discovered outside the asset catalogues — one set by a scene component.</summary>
        public void AddKnownFlag(string flag) => AddFlag(flag);

        private void AddFlag(string flag)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                KnownFlags.Add(flag);
            }
        }
    }
}
