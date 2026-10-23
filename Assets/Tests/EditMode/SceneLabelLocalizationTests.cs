using System.Linq;
using Game.Core;
using Game.Core.Localization;
using Game.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>
    /// Player-facing text authored into a scene, and the picker that switches language
    /// (TASK 042's scene-label pass).
    ///
    /// The asset half of localization was finished earlier; this covers the half that
    /// lives in scenes. Two things turned out to matter more than the field count
    /// suggested: three <c>Prompt</c> overrides were hard-coded English in C# rather than
    /// scene data at all, and the only prompt that shows an object's display name was
    /// showing GameObject names — a player read "Light Brazier_0".
    /// </summary>
    public class SceneLabelLocalizationTests
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

        // ------------------------------------------------------------------ the slug

        [Test]
        public void ASlugIsLowerCaseAndUnderscored()
        {
            Assert.AreEqual("speak_to", LocalizedContent.Slug("Speak to"));
            Assert.AreEqual("interact", LocalizedContent.Slug("Interact"));
            Assert.AreEqual("the_left_brazier", LocalizedContent.Slug("the left brazier"));
        }

        /// <summary>
        /// Runs of punctuation and whitespace must collapse, or "Speak to" and
        /// "Speak  to" become two keys and one of them is always missing.
        /// </summary>
        [Test]
        public void RunsOfSeparatorsCollapseToOneUnderscore()
        {
            Assert.AreEqual("speak_to", LocalizedContent.Slug("Speak  to"));
            Assert.AreEqual("speak_to", LocalizedContent.Slug("Speak -- to"));
            Assert.AreEqual("speak_to", LocalizedContent.Slug("  Speak to  "));
        }

        // ----------------------------------------------------------------- shared keys

        [Test]
        public void ASharedKeyTranslatesRepeatedTextOnce()
        {
            Strings.UseForTests(TableWith(("interaction.verb.speak_to", "बात करें")));

            Assert.AreEqual("बात करें", LocalizedContent.Shared("interaction.verb", "Speak to"));
        }

        [Test]
        public void ASharedKeyWithNothingBehindItFallsBackToTheAuthoredText()
        {
            Strings.UseForTests(TableWith(("interaction.verb.enter", "प्रवेश")));

            Assert.AreEqual("Speak to", LocalizedContent.Shared("interaction.verb", "Speak to"),
                "an untranslated verb must still read, not come back as a key");
        }

        // ------------------------------------------------- the prompt on a real object

        private sealed class TestInteractable : Interactable
        {
            public override void Interact(GameObject interactor) { }
        }

        private static TestInteractable NewInteractable()
        {
            // Interactable requires a Collider, so it is added before the component.
            var go = new GameObject("Test_Object");
            go.AddComponent<BoxCollider>();
            return go.AddComponent<TestInteractable>();
        }

        [Test]
        public void AnInteractableWithNoKeyReadsAsAuthored()
        {
            var subject = NewInteractable();
            subject.ConfigureInteraction("Speak to", "Queen Amara", 2.5f);

            Strings.ResetForTests();

            Assert.AreEqual("Queen Amara", subject.DisplayName);
            Assert.AreEqual("Speak to", subject.PromptVerb);

            Object.DestroyImmediate(subject.gameObject);
        }

        [Test]
        public void AnInteractableWithAKeyReadsTheTable()
        {
            var subject = NewInteractable();
            subject.ConfigureInteraction("Speak to", "Queen Amara", 2.5f);
            subject.ConfigureLocalizationKey("interactable.npc_queenamara");

            Strings.UseForTests(TableWith(
                ("interactable.npc_queenamara.name", "रानी अमरा"),
                ("interaction.verb.speak_to", "बात करें")));

            Assert.AreEqual("रानी अमरा", subject.DisplayName);
            Assert.AreEqual("बात करें", subject.PromptVerb, "the verb comes from its shared key");
            Assert.AreEqual("Queen Amara", subject.AuthoredDisplayName, "the authored text stays reachable");

            Object.DestroyImmediate(subject.gameObject);
        }

        /// <summary>
        /// Verbs are shared by default so a translator sees "Enter" once rather than once
        /// per door, but an object may override its own — which some languages need, where
        /// the verb agrees with the noun it applies to.
        /// </summary>
        [Test]
        public void AnObjectCanOverrideTheSharedVerb()
        {
            var subject = NewInteractable();
            subject.ConfigureInteraction("Enter", "the temple", 2.5f);
            subject.ConfigureLocalizationKey("interactable.temple_door");

            Strings.UseForTests(TableWith(
                ("interaction.verb.enter", "shared enter"),
                ("interactable.temple_door.verb", "agreeing enter")));

            Assert.AreEqual("agreeing enter", subject.PromptVerb,
                "an object's own .verb entry should win over the shared one");

            Object.DestroyImmediate(subject.gameObject);
        }

        // ----------------------------------------------------- the shipped scene labels

        /// <summary>
        /// The bug this pass existed to fix, asserted against the shipped content: the
        /// only prompt that shows a display name is a brazier's, and none of the braziers
        /// had one authored, so <c>DisplayName</c> fell through to the GameObject's name.
        /// </summary>
        [TestCase("Assets/Scenes/Avarsha.unity")]
        [TestCase("Assets/Scenes/Agniya.unity")]
        public void NoBrazierShowsItsGameObjectNameToThePlayer(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var brazier in root.GetComponentsInChildren<FireBrazier>(true))
                {
                    Assert.IsNotEmpty(brazier.AuthoredDisplayName ?? string.Empty,
                        $"{brazier.name} has no display name, so its prompt reads "
                        + $"\"Light {brazier.name}\" to the player.");
                    Assert.AreNotEqual(brazier.name, brazier.AuthoredDisplayName,
                        "a display name that is the GameObject's name is a developer identifier on screen");
                }
            }
        }

        [TestCase("Assets/Scenes/Avarsha.unity")]
        [TestCase("Assets/Scenes/Agniya.unity")]
        public void EveryPlayerFacingSceneLabelHasAKeyAndAnEntry(string path)
        {
            var english = Resources.Load<StringTable>("Localization/Strings_en");
            Assert.IsNotNull(english);

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            void Check(Interactable subject, string authored, string what)
            {
                if (string.IsNullOrWhiteSpace(authored)) return;

                Assert.IsNotEmpty(subject.LocalizationKey ?? string.Empty,
                    $"{subject.name} shows \"{authored}\" ({what}) and has no localization key.");

                var key = LocalizedContent.KeyFor(subject.LocalizationKey, "name");
                Assert.IsTrue(english.Has(key), $"Strings_en has no '{key}' for {subject.name}.");
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var brazier in root.GetComponentsInChildren<FireBrazier>(true))
                    Check(brazier, brazier.AuthoredDisplayName, "a brazier's Light prompt");

                foreach (var exit in root.GetComponentsInChildren<SceneExit>(true))
                    Check(exit, exit.AuthoredDisplayName, "a door's prompt");

                foreach (var npc in root.GetComponentsInChildren<Game.Dialogue.NpcInteractable>(true))
                    Check(npc, npc.AuthoredNpcName, "a character's name");

                foreach (var interactable in root.GetComponentsInChildren<Interactable>(true))
                {
                    var verb = interactable.AuthoredPromptVerb;
                    if (string.IsNullOrWhiteSpace(verb)) continue;

                    var verbKey = LocalizedContent.KeyFor("interaction.verb", LocalizedContent.Slug(verb));
                    Assert.IsTrue(english.Has(verbKey),
                        $"Strings_en has no '{verbKey}' for {interactable.name}'s verb \"{verb}\".");
                }
            }
        }

        /// <summary>
        /// No prompt may be built by C# string interpolation. Three were —
        /// <c>NpcInteractable</c>, <c>MemoryPickup</c> and <c>FireBrazier</c> — which is
        /// player-facing English baked into the assembly, and which TASK 042's claim that
        /// "every string a script shows a player is externalized" did not cover.
        /// </summary>
        [Test]
        public void NoPromptIsBuiltByInterpolation()
        {
            var offenders = new System.Collections.Generic.List<string>();

            foreach (var file in System.IO.Directory.GetFiles(
                Application.dataPath + "/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            {
                foreach (var line in System.IO.File.ReadAllLines(file))
                {
                    if (!line.Contains("override string Prompt")) continue;

                    // An interpolated string containing a placeholder is text assembled in
                    // code. Strings.Format calls are fine; $"..." with a {brace} is not.
                    if (line.Contains("$\"") && line.Contains("{"))
                    {
                        offenders.Add(System.IO.Path.GetFileName(file) + ": " + line.Trim());
                    }
                }
            }

            Assert.IsEmpty(offenders,
                "These prompts interpolate text in C#, so they cannot be translated:\n"
                + string.Join("\n", offenders));
        }

        // ------------------------------------------------------------- the language picker

        [Test]
        public void TheBuildOffersEveryLanguageItHasATableFor()
        {
            var languages = SettingsManager.AvailableLanguages();

            Assert.IsNotEmpty(languages);
            Assert.AreEqual(Strings.DefaultLanguage, languages[0].LanguageCode,
                "English should sort first, because it is the fallback and the authored language");
            Assert.IsTrue(languages.Any(table => table.LanguageCode == StringTable.PseudoLanguageCode),
                "the pseudo-locale must be reachable here: the Editor and a Development player are where "
                + "long text and non-ASCII glyphs get checked, and hiding it would mean editing a settings "
                + "file by hand to see it");
        }

        /// <summary>
        /// The other half of that: it must **not** reach a Release player. A player who
        /// picks the pseudo-locale gets accented, padded nonsense and then has to navigate
        /// back out of a menu they can no longer read.
        ///
        /// Asserted against the source rather than by building, because the gate is an
        /// <c>#if</c> and this test runs in the Editor where the symbol is defined — so no
        /// runtime check here could tell a gated build from an ungated one. What this can
        /// prove is that the gate is present and spelled correctly, which is the way it
        /// would actually be lost: by someone tidying the directive away.
        /// </summary>
        [Test]
        public void ThePseudoLocaleIsGatedOutOfAReleasePlayer()
        {
            var source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "Scripts/Core/SettingsManager.cs"));

            Assert.IsTrue(source.Contains("#if !UNITY_EDITOR && !GAME_DEVELOPER_TOOLS"),
                "AvailableLanguages no longer gates the pseudo-locale, so a Release player would offer it "
                + "to players as though it were a language.");
            Assert.IsTrue(source.Contains("StringTable.PseudoLanguageCode"),
                "the gate should filter by the named constant, not by a literal that can drift from the "
                + "generator and the tests");
        }

        [Test]
        public void TheMainMenuHasALanguagePickerWiredUp()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);

            Game.UI.MainMenuController menu = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                menu = root.GetComponentInChildren<Game.UI.MainMenuController>(true);
                if (menu != null) break;
            }

            Assert.IsNotNull(menu, "MainMenu has no MainMenuController");

            var serialized = new UnityEditor.SerializedObject(menu);

            // The picker moved in TASK 054. It was a pair of references on this controller,
            // which is why it existed in the Main Menu and nowhere else; it is now one of
            // the rows SettingsPanel builds, and the panel is mounted by the pause menu as
            // well. So what this asserts is that the menu can reach the panel at all —
            // without it, Strings.SetLanguage is once again a method with no caller.
            var panel = serialized.FindProperty("settings").objectReferenceValue as Game.UI.SettingsPanel;
            Assert.IsNotNull(panel,
                "The Main Menu has no SettingsPanel, so nothing can switch language: "
                + "Strings.SetLanguage exists and no control calls it.");

            var built = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            Assert.IsEmpty(built,
                "SettingsPanel's controls are built at runtime; finding authored ones means "
                + "a hand-laid copy has come back and will drift from the built one.");
        }
    }
}
