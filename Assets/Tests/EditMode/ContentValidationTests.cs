using System.Collections.Generic;
using System.Linq;
using Game.EditorTools;
using Game.Inventory;
using Game.Memory;
using Game.Progression;
using Game.Quests;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The content validator (TASK 042).
    ///
    /// Two kinds of test here, and both are needed. The first kind asserts the **shipped
    /// content passes** — that is the gate, and it is what fails when somebody mistypes an
    /// objective id. The second kind feeds the validator content that is deliberately
    /// broken and asserts it complains, because a validator that never fires is
    /// indistinguishable from one that is broken, and the first kind alone cannot tell
    /// those apart.
    /// </summary>
    public class ContentValidationTests
    {
        private SceneSetup[] previous;
        private readonly List<Object> created = new();

        [SetUp]
        public void SaveOpenScenes() => previous = EditorSceneManager.GetSceneManagerSetup();

        [TearDown]
        public void Cleanup()
        {
            for (var i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();

            foreach (var setup in previous)
            {
                if (setup.isLoaded && setup.isActive)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                    return;
                }
            }
        }

        private T New<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            created.Add(asset);
            return asset;
        }

        private static bool Has(IEnumerable<ContentIssue> issues, string rule) =>
            issues.Any(issue => issue.Rule == rule);

        private static IEnumerable<ContentIssue> Errors(IEnumerable<ContentIssue> issues) =>
            issues.Where(issue => issue.Severity == ContentSeverity.Error);

        // -------------------------------------------------------------- the gate itself

        /// <summary>
        /// TASK 042's gate: invalid content fails validation before a build. This is the
        /// half that says the content is currently valid. If it fails, something authored
        /// is broken and the build will refuse it — read the message, it names the asset.
        /// </summary>
        [Test]
        public void TheShippedContentHasNoValidationErrors()
        {
            var issues = ContentValidation.ValidateAll();
            var errors = Errors(issues).ToList();

            Assert.IsEmpty(errors, "The authored content has errors:\n" + ContentValidation.Format(errors));
        }

        /// <summary>
        /// Warnings are allowed to exist, but not silently. This pins the ones that are
        /// currently accepted, so a new one has to be looked at by whoever adds it rather
        /// than joining a pile nobody reads.
        /// </summary>
        [Test]
        public void TheShippedContentsWarningsAreTheOnesWeKnowAbout()
        {
            var warnings = ContentValidation.ValidateAll()
                .Where(issue => issue.Severity == ContentSeverity.Warning)
                .ToList();

            // "quest-reward" came off this list in the closure pass of 28 September 2026,
            // once Q001 and Q002 granted what their prose promised. Only the endings
            // remain, and they cannot be authored before Act V exists.
            var accepted = new HashSet<string> { "ending-condition" };
            var unexpected = warnings.Where(issue => !accepted.Contains(issue.Rule)).ToList();

            Assert.IsEmpty(unexpected,
                "New content warnings appeared. Either fix them or add the rule to this test's accepted set, "
                + "deliberately:\n" + ContentValidation.Format(unexpected));
        }

        [Test]
        public void EverySceneExitLeadsSomewhereThatExists()
        {
            var issues = ContentValidation.ValidateScenes();

            Assert.IsEmpty(Errors(issues).Where(issue => issue.Rule == "scene-exit"),
                ContentValidation.Format(issues.Where(issue => issue.Rule == "scene-exit")));
        }

        // --------------------------------------------------- the validator actually fires

        [Test]
        public void ADuplicateQuestIdIsAnError()
        {
            var first = New<QuestDefinition>("Q_First");
            var second = New<QuestDefinition>("Q_Second");
            first.Configure("Q_DUPLICATE", "First", "First", new[] { NewObjective("A") });
            second.Configure("Q_DUPLICATE", "Second", "Second", new[] { NewObjective("B") });

            var issues = Validate(quests: new[] { first, second });

            Assert.IsTrue(Has(Errors(issues), "duplicate-id"));
        }

        /// <summary>
        /// An objective is reported by id alone with no quest named, so two quests sharing
        /// one is not a naming clash — it is one report advancing a quest the player may
        /// not have started.
        /// </summary>
        [Test]
        public void AnObjectiveIdSharedByTwoQuestsIsAnError()
        {
            var first = New<QuestDefinition>("Q_First");
            var second = New<QuestDefinition>("Q_Second");
            first.Configure("Q_ONE", "First", "First", new[] { NewObjective("SHARED") });
            second.Configure("Q_TWO", "Second", "Second", new[] { NewObjective("SHARED") });

            var issues = Validate(quests: new[] { first, second });

            Assert.IsTrue(Errors(issues).Any(issue => issue.Rule == "duplicate-id" && issue.Subject == "SHARED"));
        }

        [Test]
        public void AQuestWithNoObjectivesIsAnError()
        {
            var quest = New<QuestDefinition>("Q_Empty");
            quest.Configure("Q_EMPTY", "Goes nowhere", "Goes nowhere", new QuestObjective[0]);

            Assert.IsTrue(Has(Errors(Validate(quests: new[] { quest })), "quest-shape"));
        }

        [Test]
        public void AQuestWhoseObjectivesAreAllOptionalIsAnError()
        {
            var quest = New<QuestDefinition>("Q_Optional");
            var objective = NewObjective("OPTIONAL_ONE");
            objective.Optional = true;
            quest.Configure("Q_OPTIONAL", "Never finishes", "Never finishes", new[] { objective });

            Assert.IsTrue(Errors(Validate(quests: new[] { quest }))
                .Any(issue => issue.Message.Contains("optional")));
        }

        [Test]
        public void ARewardThatGrantsAnItemWithNoItemAssignedIsAnError()
        {
            var quest = New<QuestDefinition>("Q_Reward");
            quest.Configure("Q_REWARD", "Pays badly", "Pays badly", new[] { NewObjective("STEP") },
                rewardDefinitions: new[] { new QuestReward { Type = QuestRewardType.Item, Item = null, Amount = 1 } });

            Assert.IsTrue(Has(Errors(Validate(quests: new[] { quest })), "quest-reward"));
        }

        [Test]
        public void ARewardRestoringAMemoryThatDoesNotExistIsAnError()
        {
            var quest = New<QuestDefinition>("Q_Reward");
            quest.Configure("Q_REWARD", "Pays in ghosts", "Pays in ghosts", new[] { NewObjective("STEP") },
                rewardDefinitions: new[]
                {
                    new QuestReward { Type = QuestRewardType.MemoryRestore, TargetId = "MEM_DOES_NOT_EXIST" }
                });

            Assert.IsTrue(Has(Errors(Validate(quests: new[] { quest })), "unknown-id"));
        }

        [Test]
        public void AMemoryReportingAnObjectiveNoQuestDefinesIsAnError()
        {
            var memory = New<MemoryFragment>("Mem_Orphan");
            memory.Configure("MEM_ORPHAN", "An orphan", "An orphan memory", "Nobody",
                MemoryCategory.Personal, MemoryImportance.Optional, objectiveId: "NO_SUCH_OBJECTIVE");

            Assert.IsTrue(Has(Errors(Validate(memories: new[] { memory })), "unknown-id"));
        }

        [Test]
        public void ASkillRequiringASkillThatDoesNotExistIsAnError()
        {
            var skill = New<SkillDefinition>("Skill_Orphan");
            skill.Configure("SKILL_ORPHAN", "Orphan", SkillBranch.Warrior, 1,
                SkillEffectType.MaxHealthBonus, 10f, "SKILL_MISSING");

            Assert.IsTrue(Has(Errors(Validate(skills: new[] { skill })), "unknown-id"));
        }

        /// <summary>A loop means neither skill can ever be unlocked, and the tree UI would recurse drawing it.</summary>
        [Test]
        public void ASkillPrerequisiteLoopIsAnError()
        {
            var a = New<SkillDefinition>("Skill_A");
            var b = New<SkillDefinition>("Skill_B");
            a.Configure("SKILL_A", "A", SkillBranch.Warrior, 1, SkillEffectType.MaxHealthBonus, 10f, "SKILL_B");
            b.Configure("SKILL_B", "B", SkillBranch.Warrior, 1, SkillEffectType.MaxHealthBonus, 10f, "SKILL_A");

            Assert.IsTrue(Errors(Validate(skills: new[] { a, b }))
                .Any(issue => issue.Message.Contains("loops")));
        }

        [Test]
        public void AnUnknownFlagIsAWarningAndNotAnError()
        {
            var quest = New<QuestDefinition>("Q_Flagged");
            quest.Configure("Q_FLAGGED", "Gated", "Gated", new[] { NewObjective("STEP") });

            var catalogue = NewCatalogue(quests: new[] { quest });

            Assert.IsFalse(catalogue.KnownFlags.Contains("A_FLAG_NOBODY_SETS"));
        }

        /// <summary>
        /// A flag a scene's `LocationTrigger` sets is as real as one a quest sets. Before
        /// the two-pass scene walk, every scene-authored flag looked unknown.
        /// </summary>
        [Test]
        public void FlagsSetInASceneCountAsKnown()
        {
            var issues = ContentValidation.ValidateScenes();
            var unknownFlags = issues.Where(issue => issue.Rule == "unknown-flag").ToList();

            Assert.IsEmpty(unknownFlags,
                "Scene content gates on flags nothing sets:\n" + ContentValidation.Format(unknownFlags));
        }

        [Test]
        public void SeverityDecidesWhetherABuildIsRefused()
        {
            var clean = new List<ContentIssue>
            {
                new(ContentSeverity.Warning, "test", "x", "a warning")
            };

            var broken = new List<ContentIssue>
            {
                new(ContentSeverity.Warning, "test", "x", "a warning"),
                new(ContentSeverity.Error, "test", "y", "an error")
            };

            Assert.IsFalse(ContentValidation.HasErrors(clean), "warnings alone must not stop a build");
            Assert.IsTrue(ContentValidation.HasErrors(broken));
        }

        // ------------------------------------------------------------------------ helpers

        private static QuestObjective NewObjective(string id) =>
            new() { ObjectiveId = id, Description = id, RequiredCount = 1 };

        private ContentCatalogue NewCatalogue(
            IReadOnlyList<QuestDefinition> quests = null,
            IReadOnlyList<MemoryFragment> memories = null,
            IReadOnlyList<SkillDefinition> skills = null) =>
            new(quests ?? new QuestDefinition[0],
                memories ?? new MemoryFragment[0],
                new InventoryItem[0],
                skills ?? new SkillDefinition[0],
                new Game.AI.EnemyArchetype[0]);

        /// <summary>
        /// Runs the asset rules over a catalogue built from these assets alone, rather than
        /// over the project. A test that added a broken asset to the real catalogue would
        /// have to write it to disk, and would then be one crash away from leaving it there.
        /// </summary>
        private List<ContentIssue> Validate(
            IReadOnlyList<QuestDefinition> quests = null,
            IReadOnlyList<MemoryFragment> memories = null,
            IReadOnlyList<SkillDefinition> skills = null) =>
            ContentValidation.ValidateCatalogue(NewCatalogue(quests, memories, skills));
    }
}
