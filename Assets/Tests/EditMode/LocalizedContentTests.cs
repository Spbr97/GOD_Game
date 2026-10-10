using System.Linq;
using Game.Core.Localization;
using Game.Dialogue;
using Game.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// How text authored as data gets translated (TASK 040's localization decision).
    ///
    /// The decision was a **key on each asset pointing into the shared string table**,
    /// rather than a per-language copy of every asset. What makes that decision safe to
    /// adopt incrementally is the fallback: an asset with no key, or a key nothing has
    /// translated yet, shows the text the author typed. The whole project is in that state
    /// today, so the fallback is not an edge case — it is the current behaviour of every
    /// quest, memory, item, skill and ability in the game, and these tests are what say it
    /// stays that way.
    /// </summary>
    public class LocalizedContentTests
    {
        [TearDown]
        public void Reset() => Strings.ResetForTests();

        private static StringTable TableWith(params (string Key, string Value)[] rows)
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            table.Configure("test", "Test", rows.Select(row => new StringEntry
            {
                Key = row.Key,
                Value = row.Value
            }));
            return table;
        }

        [Test]
        public void AnAssetWithNoKeyShowsWhatTheAuthorTyped()
        {
            Strings.UseForTests(TableWith(("quest.q001.title", "should not be used")));

            Assert.AreEqual("The Queen's Charge",
                LocalizedContent.Text(string.Empty, "title", "The Queen's Charge"),
                "An asset that has not been given a key is not localized, and must read as authored.");
        }

        [Test]
        public void AKeyTheTableKnowsIsTranslated()
        {
            Strings.UseForTests(TableWith(("quest.q001.title", "रानी का आदेश")));

            Assert.AreEqual("रानी का आदेश",
                LocalizedContent.Text("quest.q001", "title", "The Queen's Charge"));
        }

        /// <summary>
        /// The case that makes incremental translation possible: a key exists, and this
        /// particular field has not been translated yet. The author's text must show,
        /// **not** the key — a player would otherwise read "quest.q001.description" in
        /// their journal.
        /// </summary>
        [Test]
        public void AKeyTheTableDoesNotKnowFallsBackToTheAuthoredText()
        {
            Strings.UseForTests(TableWith(("quest.q001.title", "रानी का आदेश")));

            var description = LocalizedContent.Text("quest.q001", "description", "Queen Amara has asked you.");

            Assert.AreEqual("Queen Amara has asked you.", description);
            Assert.AreNotEqual("quest.q001.description", description,
                "Showing the key is right for code and wrong for content: an untranslated asset is a "
                + "to-do, not a bug, and the player must still be able to read their journal.");
        }

        [Test]
        public void KeysAreDerivedByConventionFromTheAssetsPrefix()
        {
            Assert.AreEqual("quest.q001.title", LocalizedContent.KeyFor("quest.q001", "title"));
            Assert.AreEqual("memory.mem_003.description", LocalizedContent.KeyFor("memory.mem_003", "description"));
        }

        /// <summary>
        /// Through a real asset rather than the helper, because the accessor is where a
        /// mistake would actually live — a property that forgot to route through
        /// <see cref="LocalizedContent"/> would pass every test above.
        /// </summary>
        [Test]
        public void AQuestDefinitionReadsItsTitleThroughTheTable()
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.Configure("Q001", "The Queen's Charge", "Authored description",
                new[] { new QuestObjective { ObjectiveId = "A", Description = "a", RequiredCount = 1 } });

            Strings.ResetForTests();
            Assert.AreEqual("The Queen's Charge", quest.Title, "with no key, the authored title");
            Assert.AreEqual("The Queen's Charge", quest.AuthoredTitle);

            quest.ConfigureLocalizationKey("quest.q001");
            Strings.UseForTests(TableWith(("quest.q001.title", "रानी का आदेश")));

            Assert.AreEqual("रानी का आदेश", quest.Title, "with a key, the translation");
            Assert.AreEqual("The Queen's Charge", quest.AuthoredTitle,
                "and the authored text stays reachable, because validation and the Inspector need it");

            Object.DestroyImmediate(quest);
        }

        [Test]
        public void QuestObjectivesAndRewardsUseTheSameAssetKey()
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            var objective = new QuestObjective
            {
                ObjectiveId = "REACH_RUINS", Description = "Search the ruins", RequiredCount = 1
            };
            quest.Configure("Q001", "Title", "Description", new[] { objective },
                rewards: "A mark of trust");
            quest.ConfigureLocalizationKey("quest.q001");

            Strings.UseForTests(TableWith(
                ("quest.q001.objective.reach_ruins", "Find the old stones"),
                ("quest.q001.rewards", "The queen's token")));

            Assert.AreEqual("Find the old stones", quest.ObjectiveDescription(objective));
            Assert.AreEqual("The queen's token", quest.RewardsSummary);

            Strings.UseForTests(TableWith());
            Assert.AreEqual("Search the ruins", quest.ObjectiveDescription(objective));
            Assert.AreEqual("A mark of trust", quest.RewardsSummary);

            Object.DestroyImmediate(quest);
        }

        [Test]
        public void DialogueLinesAndChoicesUseTheGraphAndNodeIds()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            var node = new DialogueNode
            {
                DialogueId = "INTRO", Speaker = "Amara", Text = "Welcome",
                LowIntegrityText = "Who are you?",
                Choices = new[] { new DialogueChoice { Text = "I remember" } }
            };
            graph.Configure("DLG_AMARA", new[] { "INTRO" }, new[] { node });
            Strings.UseForTests(TableWith(
                ("dialogue.dlg_amara.node.intro.speaker", "The Queen"),
                ("dialogue.dlg_amara.node.intro.text", "Greetings"),
                ("dialogue.dlg_amara.node.intro.low_integrity", "Have we met?"),
                ("dialogue.dlg_amara.node.intro.choice.0", "I know you")));

            Assert.AreEqual("The Queen", graph.SpeakerText(node));
            Assert.AreEqual("Greetings", graph.SubtitleText(node));
            Assert.AreEqual("Have we met?", graph.LowIntegrityText(node));
            Assert.AreEqual("I know you", graph.ChoiceText(node, 0));

            Object.DestroyImmediate(graph);
        }
    }
}
