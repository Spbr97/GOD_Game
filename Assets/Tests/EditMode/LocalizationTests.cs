using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Core.Localization;
using Game.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// The string table and the code that reads it (SPEC.md section 73, TASK 042).
    ///
    /// The important test here is <see cref="EveryKeyTheCodeAsksForHasEnglishText"/>. The
    /// point of externalizing text is that it can be translated; the risk it introduces
    /// is that a key and its text drift apart, and the failure is a label reading
    /// <c>hud.helath</c> in a shipped build. That test makes it impossible to ship one.
    /// </summary>
    public class LocalizationTests
    {
        private StringTable english;

        [SetUp]
        public void LoadTable()
        {
            english = Resources.Load<StringTable>("Localization/Strings_en");
            Assert.IsNotNull(english,
                "Assets/Resources/Localization/Strings_en.asset is missing. Without it every label in the game "
                + "shows its key.");
        }

        [TearDown]
        public void Reset() => Strings.ResetForTests();

        private static IEnumerable<string> AllKeys()
        {
            foreach (var key in typeof(StringKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()))
            {
                yield return key;
            }

            var catalogue = ContentCatalogue.Load();
            foreach (var quest in catalogue.Quests)
            {
                if (string.IsNullOrEmpty(quest.LocalizationKey)) continue;
                yield return quest.LocalizationKey + ".title";
                yield return quest.LocalizationKey + ".description";
                if (!string.IsNullOrEmpty(quest.AuthoredRewardsSummary))
                    yield return quest.LocalizationKey + ".rewards";
                foreach (var objective in quest.Objectives)
                    yield return quest.LocalizationKey + ".objective." + objective.ObjectiveId.ToLowerInvariant();
            }

            foreach (var memory in catalogue.Memories)
            {
                if (string.IsNullOrEmpty(memory.LocalizationKey)) continue;
                yield return memory.LocalizationKey + ".title";
                yield return memory.LocalizationKey + ".description";
            }

            foreach (var item in catalogue.Items)
            {
                if (string.IsNullOrEmpty(item.LocalizationKey)) continue;
                yield return item.LocalizationKey + ".name";
                if (!string.IsNullOrEmpty(item.AuthoredDescription))
                    yield return item.LocalizationKey + ".description";
            }

            foreach (var skill in catalogue.Skills)
            {
                if (string.IsNullOrEmpty(skill.LocalizationKey)) continue;
                yield return skill.LocalizationKey + ".name";
                if (!string.IsNullOrEmpty(skill.AuthoredDescription))
                    yield return skill.LocalizationKey + ".description";
            }

            foreach (var graph in catalogue.Graphs)
            {
                if (graph.Nodes == null) continue;
                foreach (var node in graph.Nodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.DialogueId)) continue;
                    var prefix = graph.LocalizationKey + ".node." + node.DialogueId.ToLowerInvariant() + ".";
                    if (!string.IsNullOrEmpty(node.Speaker)) yield return prefix + "speaker";
                    if (!string.IsNullOrEmpty(node.Text)) yield return prefix + "text";
                    if (!string.IsNullOrEmpty(node.Subtitle)) yield return prefix + "subtitle";
                    if (!string.IsNullOrEmpty(node.LowIntegrityText)) yield return prefix + "low_integrity";
                    if (node.Choices == null) continue;
                    for (var i = 0; i < node.Choices.Length; i++)
                        if (node.Choices[i] != null && !string.IsNullOrEmpty(node.Choices[i].Text))
                            yield return prefix + "choice." + i;
                }
            }
        }

        // ------------------------------------------------------------------- the table

        [Test]
        public void EveryKeyTheCodeAsksForHasEnglishText()
        {
            var missing = AllKeys().Where(key => !english.Has(key)).ToList();

            Assert.IsEmpty(missing,
                "These keys are used by the code and have no English text:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void TheTableHasNoUnusedEntry()
        {
            var known = new HashSet<string>(AllKeys());
            var orphans = english.Entries
                .Where(entry => entry != null && !string.IsNullOrEmpty(entry.Key) && !known.Contains(entry.Key))
                .Select(entry => entry.Key)
                .ToList();

            Assert.IsEmpty(orphans,
                "These entries are in the table but nothing asks for them. Either a key was renamed and the old "
                + "row left behind, or a string is dead:\n  " + string.Join("\n  ", orphans));
        }

        [Test]
        public void TheTableHasNoDuplicateKeys()
        {
            var duplicates = english.Entries
                .Where(entry => entry != null && !string.IsNullOrEmpty(entry.Key))
                .GroupBy(entry => entry.Key)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            Assert.IsEmpty(duplicates, "Duplicate keys: " + string.Join(", ", duplicates));
        }

        [Test]
        public void NoEntryHasEmptyText()
        {
            var blank = english.Entries
                .Where(entry => entry != null && !string.IsNullOrEmpty(entry.Key) && string.IsNullOrEmpty(entry.Value))
                .Select(entry => entry.Key)
                .ToList();

            Assert.IsEmpty(blank,
                "A blank string renders as an invisible label, which is harder to diagnose than a wrong one: "
                + string.Join(", ", blank));
        }

        /// <summary>
        /// A placeholder is <c>{0}</c>, not <c>{name}</c>. Positional placeholders are what
        /// <c>string.Format</c> takes, and a named one throws at runtime rather than being
        /// substituted.
        /// </summary>
        [Test]
        public void EveryPlaceholderIsPositional()
        {
            var named = new Regex(@"\{[A-Za-z_][A-Za-z0-9_]*\}");
            var offenders = english.Entries
                .Where(entry => entry != null && entry.Value != null && named.IsMatch(entry.Value))
                .Select(entry => $"{entry.Key}: {entry.Value}")
                .ToList();

            Assert.IsEmpty(offenders, "Named placeholders will not substitute:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// A string with a gap in its placeholder numbering — <c>{0}</c> and <c>{2}</c> but
        /// no <c>{1}</c> — means a caller is passing arguments the text will never show,
        /// or is about to throw.
        /// </summary>
        [Test]
        public void PlaceholderNumbersRunFromZeroWithoutGaps()
        {
            var placeholder = new Regex(@"\{(\d+)");
            var offenders = new List<string>();

            foreach (var entry in english.Entries)
            {
                if (entry?.Value == null)
                {
                    continue;
                }

                var indices = placeholder.Matches(entry.Value)
                    .Select(match => int.Parse(match.Groups[1].Value))
                    .Distinct()
                    .OrderBy(value => value)
                    .ToList();

                if (indices.Count == 0)
                {
                    continue;
                }

                if (indices[0] != 0 || indices.Last() != indices.Count - 1)
                {
                    offenders.Add($"{entry.Key}: {entry.Value}");
                }
            }

            Assert.IsEmpty(offenders, "Placeholder numbering has gaps:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// SPEC.md section 32 fixes what this sentence must say. It is now table text
        /// rather than a compiled constant, so this is what stops it drifting.
        /// </summary>
        [Test]
        public void TheBackupRestoredMessageStillSaysWhatTheSpecRequires()
        {
            var message = english.Find(StringKeys.SaveBackupRestored);

            Assert.AreEqual(
                "Your previous save could not be loaded. A safe backup has been restored.",
                message);
        }

        // -------------------------------------------------------------- the lookup code

        [Test]
        public void AMissingKeyShowsTheKeyRatherThanNothing()
        {
            Strings.UseForTests(NewTable(("known", "Known text")));

            LogAssert.ignoreFailingMessages = true;
            var result = Strings.Get("no.such.key");
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual("no.such.key", result,
                "a blank label is undiagnosable; the key is at least greppable");
        }

        [Test]
        public void FormatFillsPositionalPlaceholders()
        {
            Strings.UseForTests(NewTable(("hp", "Health  {0}/{1}")));

            Assert.AreEqual("Health  40/100", Strings.Format("hp", 40, 100));
        }

        /// <summary>
        /// A translator can reorder placeholders, and must be able to — Hindi puts the verb
        /// last. If reordering broke the substitution the whole scheme would be pointless.
        /// </summary>
        [Test]
        public void ATranslationMayReorderItsPlaceholders()
        {
            Strings.UseForTests(NewTable(("prompt", "{1} ko {0}")));

            Assert.AreEqual("Amara ko Speak", Strings.Format("prompt", "Speak", "Amara"));
        }

        /// <summary>
        /// A bad placeholder in a translation must not take the frame with it. Returning
        /// the unformatted text is wrong on screen; throwing inside a UI update is worse.
        /// </summary>
        [Test]
        public void AMalformedPlaceholderDoesNotThrow()
        {
            Strings.UseForTests(NewTable(("broken", "Health {0}/{")));

            string result = null;
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => result = Strings.Format("broken", 1, 2));
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual("Health {0}/{", result);
        }

        [Test]
        public void AnUnknownLanguageFallsBackToEnglishRatherThanFailing()
        {
            Strings.ResetForTests();

            Assert.IsFalse(Strings.SetLanguage("zz"), "there is no 'zz' table");
            Assert.AreEqual("English", Strings.Active.LanguageName);
            Assert.AreEqual(english.Find(StringKeys.HudParry), Strings.Get(StringKeys.HudParry));
        }

        [Test]
        public void EnglishLoadsFromResourcesWithNoSetupCall()
        {
            Strings.ResetForTests();

            Assert.AreEqual(english.Find(StringKeys.QuestObjectiveComplete),
                Strings.Get(StringKeys.QuestObjectiveComplete),
                "a scene that never initializes localization must still render text");
        }

        [Test]
        public void GetWithNoKeyIsEmptyRatherThanAnError()
        {
            Assert.AreEqual(string.Empty, Strings.Get(null));
            Assert.AreEqual(string.Empty, Strings.Get(string.Empty));
        }

        // ------------------------------------------------------------- the pseudo-locale

        /// <summary>
        /// Until now there was exactly one table in the project, so every claim about
        /// switching languages and falling back was tested against a table built in the
        /// test itself. That proves <see cref="Strings"/>'s branching and nothing about
        /// the pipeline: whether a second table loads from Resources at all, whether the
        /// code that reads it survives non-ASCII text, and whether a key one language is
        /// missing actually reaches English at runtime.
        ///
        /// <c>Strings_qps</c> is a real second table. It is generated from English by
        /// <c>Tools/make-pseudo-locale.py</c>, it accents every letter and pads each
        /// string by about a third, and it deliberately omits three keys.
        /// </summary>
        private const string PseudoLanguage = "qps";

        [Test]
        public void ASecondLanguageActuallyLoadsFromResources()
        {
            Strings.ResetForTests();

            Assert.IsTrue(Strings.SetLanguage(PseudoLanguage),
                "Strings_qps should be in Resources/Localization. Regenerate it with "
                + "Tools/make-pseudo-locale.py if it has gone missing.");
            Assert.AreEqual(PseudoLanguage, Strings.Active.LanguageCode);
            Assert.AreNotEqual("English", Strings.Active.LanguageName);
        }

        [Test]
        public void TextInAnotherLanguageComesBackInThatLanguage()
        {
            Strings.ResetForTests();
            Strings.SetLanguage(PseudoLanguage);

            var translated = Strings.Get(StringKeys.HudParry);

            Assert.AreNotEqual(english.Find(StringKeys.HudParry), translated,
                "the pseudo-locale should not be returning English");
            Assert.IsTrue(translated.StartsWith("["),
                $"'{translated}' is not the pseudo-locale's text; something is still reading English.");
        }

        /// <summary>
        /// The fallback, walked for real. Three keys are missing from the pseudo-locale,
        /// and a player switched to it must see English for them rather than a raw key
        /// like <c>hud.combo</c> on their screen.
        /// </summary>
        [Test]
        public void AKeyMissingFromTheActiveLanguageFallsBackToEnglishText()
        {
            Strings.ResetForTests();
            Strings.SetLanguage(PseudoLanguage);

            Assert.IsFalse(Strings.Active.Has(StringKeys.HudCombo),
                "this test needs a key the pseudo-locale does not have; update OMITTED in "
                + "Tools/make-pseudo-locale.py and this assertion together.");

            var text = Strings.Get(StringKeys.HudCombo);

            Assert.AreEqual(english.Find(StringKeys.HudCombo), text);
            Assert.AreNotEqual(StringKeys.HudCombo, text,
                "falling back produced the key itself, so English was never consulted");
        }

        /// <summary>
        /// A placeholder that a translator reorders or mangles takes the frame with it
        /// unless it is caught. Here the numbering is intact, and the formatted result
        /// must contain the arguments rather than the literal braces.
        /// </summary>
        [Test]
        public void PlaceholdersSurviveTranslation()
        {
            Strings.ResetForTests();
            Strings.SetLanguage(PseudoLanguage);

            var formatted = Strings.Format(StringKeys.HudHealth, 61, 100);

            Assert.IsTrue(formatted.Contains("61") && formatted.Contains("100"),
                $"'{formatted}' lost its placeholders in translation.");
            Assert.IsFalse(formatted.Contains("{0}"),
                $"'{formatted}' still has an unfilled placeholder.");
        }

        /// <summary>
        /// Every key the pseudo-locale does carry must keep the same placeholder numbers
        /// as English. This is the check that catches the ordinary translation bug — a
        /// string that renders "Health {0}/{1}" to the player because one brace was
        /// dropped — and it will apply unchanged to the first real translation.
        /// </summary>
        [Test]
        public void EveryTranslatedStringKeepsEnglishsPlaceholders()
        {
            var pseudo = Resources.Load<StringTable>("Localization/Strings_" + PseudoLanguage);
            Assert.IsNotNull(pseudo, "the pseudo-locale table is missing from Resources");

            var placeholder = new System.Text.RegularExpressions.Regex(@"\{\d+\}");

            foreach (var entry in pseudo.Entries)
            {
                var source = english.Find(entry.Key);

                Assert.IsNotNull(source,
                    $"'{entry.Key}' is translated but no longer exists in English. Regenerate the pseudo-locale.");

                var expected = placeholder.Matches(source)
                    .Select(match => match.Value).OrderBy(value => value).ToArray();
                var actual = placeholder.Matches(entry.Value)
                    .Select(match => match.Value).OrderBy(value => value).ToArray();

                Assert.AreEqual(expected, actual,
                    $"'{entry.Key}': English has {expected.Length} placeholders and the translation has "
                    + $"{actual.Length}. A missing one renders as a literal brace on the player's screen.");
            }
        }

        /// <summary>
        /// Non-ASCII text has to survive the round trip through the asset importer. If
        /// the table were ever saved in the system codepage instead of UTF-8 this is
        /// where it shows up, rather than as mojibake in a screenshot.
        /// </summary>
        [Test]
        public void NonAsciiTextSurvivesBeingLoadedFromTheAsset()
        {
            Strings.ResetForTests();
            Strings.SetLanguage(PseudoLanguage);

            var text = Strings.Get(StringKeys.HudHealth);

            Assert.IsTrue(text.Any(character => character > 127),
                $"'{text}' came back as pure ASCII, so the accents did not survive the import.");
            Assert.IsFalse(text.Contains("?"),
                $"'{text}' contains a replacement character; the table was not read as UTF-8.");
        }

        private static StringTable NewTable(params (string Key, string Value)[] rows)
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            table.Configure("test", "Test", rows.Select(row => new StringEntry
            {
                Key = row.Key,
                Value = row.Value
            }));
            return table;
        }
    }
}
