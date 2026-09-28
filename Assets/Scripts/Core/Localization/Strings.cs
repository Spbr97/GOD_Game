using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Localization
{
    /// <summary>
    /// Where player-facing text comes from (SPEC.md section 73: all user-facing text must
    /// be externalized; never hard-code text into scripts).
    ///
    /// Static and self-loading, with no scene object and no initialization call, because
    /// the alternative is every UI script needing a reference to a manager that must
    /// exist in every scene before any of them wake — and the first scene to forget it
    /// renders a screen of blank labels. A missing table here degrades to showing the key
    /// instead, which is ugly and obvious and still lets the game run.
    ///
    /// English is loaded from <c>Resources/Localization/Strings_en</c>. Resources is the
    /// right tool for exactly this: a small asset that any scene may need before anything
    /// has had a chance to hand it a reference. SPEC.md section 74's warning against
    /// loading everything into memory is about the world, not about one table of text.
    /// </summary>
    public static class Strings
    {
        public const string DefaultLanguage = "en";

        private const string ResourceFolder = "Localization/Strings_";

        private static StringTable active;
        private static StringTable fallback;
        private static readonly HashSet<string> reportedMissing = new();

        /// <summary>The language currently in force. Always loaded on first use.</summary>
        public static StringTable Active
        {
            get
            {
                if (active == null)
                {
                    SetLanguage(DefaultLanguage);
                }

                return active;
            }
        }

        /// <summary>
        /// Switches language, falling back to English when that language has no table.
        /// Returns whether the requested language was found.
        /// </summary>
        public static bool SetLanguage(string languageCode)
        {
            fallback ??= Resources.Load<StringTable>(ResourceFolder + DefaultLanguage);

            var requested = string.IsNullOrEmpty(languageCode) || languageCode == DefaultLanguage
                ? fallback
                : Resources.Load<StringTable>(ResourceFolder + languageCode);

            reportedMissing.Clear();

            if (requested == null)
            {
                active = fallback;
                return false;
            }

            active = requested;
            return true;
        }

        /// <summary>
        /// The text for <paramref name="key"/>.
        ///
        /// Falls back to English, then to the key itself. Showing the key is deliberate:
        /// it is visible in a screenshot, greppable, and says exactly what is missing —
        /// where showing an empty string produces a blank label nobody can diagnose. Each
        /// missing key is logged once, not once per frame, because most of these are read
        /// from <c>Update</c>.
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var text = Active != null ? Active.Find(key) : null;

            if (text != null)
            {
                return text;
            }

            if (fallback != null && fallback != Active)
            {
                text = fallback.Find(key);
                if (text != null)
                {
                    return text;
                }
            }

            if (reportedMissing.Add(key))
            {
                GameLogger.LogWarning(LogCategory.Game, $"No string for key '{key}'; showing the key instead.");
            }

            return key;
        }

        /// <summary>
        /// The text for <paramref name="key"/>, or **null** when neither the active
        /// language nor English has one.
        ///
        /// The difference from <see cref="Get"/> is what happens on a miss, and both
        /// behaviours are wanted by different callers. A missing key in *code* is a bug,
        /// so Get shows the key and logs it. A missing key for *content* is an
        /// untranslated asset, which is the normal state of a game mid-translation — so
        /// <see cref="LocalizedContent"/> asks with this and falls back to the text the
        /// author typed, silently.
        /// </summary>
        public static string Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var text = Active != null ? Active.Find(key) : null;

            if (text != null)
            {
                return text;
            }

            return fallback != null && fallback != Active ? fallback.Find(key) : null;
        }

        /// <summary>
        /// The text for <paramref name="key"/> with its <c>{0}</c>, <c>{1}</c> …
        /// placeholders filled.
        ///
        /// A malformed placeholder in a translation throws inside <c>string.Format</c>,
        /// which would take the frame with it. Caught here and reported as the unformatted
        /// text: a label reading "Health {0}/{1}" is wrong, and a crash is worse.
        /// </summary>
        public static string Format(string key, params object[] arguments)
        {
            var text = Get(key);

            if (arguments == null || arguments.Length == 0)
            {
                return text;
            }

            try
            {
                return string.Format(text, arguments);
            }
            catch (System.FormatException exception)
            {
                if (reportedMissing.Add("format:" + key))
                {
                    GameLogger.LogWarning(LogCategory.Game,
                        $"The string for '{key}' has a malformed placeholder: {exception.Message}");
                }

                return text;
            }
        }

        /// <summary>Test seam. Points the lookup at a table built in code and forgets what was loaded.</summary>
        public static void UseForTests(StringTable table)
        {
            active = table;
            fallback = table;
            reportedMissing.Clear();
        }

        /// <summary>Test seam. Puts the lookup back to loading from Resources on next use.</summary>
        public static void ResetForTests()
        {
            active = null;
            fallback = null;
            reportedMissing.Clear();
        }
    }
}
