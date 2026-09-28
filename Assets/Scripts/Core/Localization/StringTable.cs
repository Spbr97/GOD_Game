using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Localization
{
    /// <summary>One key and the text it stands for in this table's language.</summary>
    [Serializable]
    public class StringEntry
    {
        [Tooltip("A key from StringKeys. Never shown to a player.")]
        public string Key;

        [TextArea(1, 4)]
        [Tooltip("The text a player reads. Placeholders are {0}, {1}, … and must keep their numbers in every language.")]
        public string Value;

        [Tooltip("For whoever translates this: where it appears and anything they need to know.")]
        public string Note;
    }

    /// <summary>
    /// Every player-facing string in one language (SPEC.md section 73).
    ///
    /// A ScriptableObject rather than a JSON or CSV file because the rest of this
    /// project's content is authored as ScriptableObjects, and because Unity gives it a
    /// diffable text asset, an Inspector and asset references for free. Exporting to
    /// whatever format a translator wants is a conversion at the edge, not a reason to
    /// store it that way.
    ///
    /// Placeholders are positional — <c>{0}</c>, not <c>{name}</c> and not C# string
    /// interpolation. Interpolation is what the code did before TASK 042, and it cannot
    /// be translated at all: the text is baked into the assembly. Positional placeholders
    /// also let a translator reorder them, which languages genuinely need — Hindi puts
    /// the verb last.
    /// </summary>
    [CreateAssetMenu(fileName = "Strings_", menuName = "God Game/String Table")]
    public class StringTable : ScriptableObject
    {
        [Tooltip("An ISO 639-1 code: en, hi, …")]
        [SerializeField] private string languageCode = "en";

        [Tooltip("The language's own name for itself, for a language picker.")]
        [SerializeField] private string languageName = "English";

        [SerializeField] private List<StringEntry> entries = new();

        private Dictionary<string, string> lookup;

        public string LanguageCode => languageCode;

        public string LanguageName => languageName;

        public IReadOnlyList<StringEntry> Entries => entries;

        /// <summary>
        /// The text for <paramref name="key"/>, or null when this table has none. Null
        /// rather than the key, so a caller can tell "not translated" from "translated to
        /// the key by accident" and decide what to do.
        /// </summary>
        public string Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            Build();
            return lookup.TryGetValue(key, out var value) ? value : null;
        }

        public bool Has(string key) => Find(key) != null;

        private void Build()
        {
            if (lookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, string>(entries.Count);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                // First wins, and a duplicate is a content error the validator reports
                // rather than something to resolve silently here.
                lookup.TryAdd(entry.Key, entry.Value);
            }
        }

        /// <summary>Editor and tooling seam. Discards the cached lookup so a change takes effect.</summary>
        public void Configure(string code, string label, IEnumerable<StringEntry> values)
        {
            languageCode = code;
            languageName = label;
            entries = new List<StringEntry>(values);
            lookup = null;
        }

        private void OnValidate() => lookup = null;
    }
}
