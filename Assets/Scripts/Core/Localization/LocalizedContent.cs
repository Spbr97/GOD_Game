namespace Game.Core.Localization
{
    /// <summary>
    /// How text authored **as data** gets translated (SPEC.md section 73, TASK 040's
    /// localization decision).
    ///
    /// TASK 042 externalized every string a *script* shows a player. That left the larger
    /// half untouched: quest titles and descriptions, memory titles and bodies, item and
    /// skill names. Those live in ScriptableObjects, which is the right place for content,
    /// and a translator had no way to produce a second language short of duplicating every
    /// asset.
    ///
    /// **The decision (28 September 2026): each asset carries a localization key pointing
    /// into the shared <see cref="StringTable"/>.** The alternative was per-language asset
    /// variants — a Hindi copy of every quest — which keeps the Inspector readable and
    /// scales badly: every asset multiplies by the number of languages, and a fix to a
    /// quest's objectives has to be applied N times or it drifts. A key keeps the text
    /// centralised in one file per language, which is also the form a translator wants.
    ///
    /// Keys are derived by convention from the asset's key prefix, so an author writes one
    /// short string rather than one per field:
    ///
    /// <code>
    /// localizationKey: quest.q001
    ///   -> quest.q001.title
    ///   -> quest.q001.description
    /// </code>
    ///
    /// **A missing translation is not an error and must never look like one.** An asset
    /// with no key at all, or a key the active table has no entry for, returns the text the
    /// author typed into the asset. That is what makes this shippable incrementally: the
    /// game is fully playable in English with no keys assigned anywhere, which is exactly
    /// the state it is in today, and a key can be added per asset as translation proceeds.
    /// Contrast <see cref="Strings.Get"/>, which shows the key itself when it finds
    /// nothing — correct for code, where a missing key is a bug, and wrong here, where it
    /// is a to-do.
    /// </summary>
    public static class LocalizedContent
    {
        /// <summary>
        /// The translated text for one field of an asset, or <paramref name="authored"/>
        /// when there is none.
        /// </summary>
        /// <param name="keyPrefix">The asset's localization key, e.g. <c>quest.q001</c>. Empty means "not localized".</param>
        /// <param name="field">The field, e.g. <c>title</c>.</param>
        /// <param name="authored">What the author typed into the asset.</param>
        public static string Text(string keyPrefix, string field, string authored)
        {
            if (string.IsNullOrEmpty(keyPrefix) || string.IsNullOrEmpty(field))
            {
                return authored;
            }

            return Strings.Find(KeyFor(keyPrefix, field)) ?? authored;
        }

        /// <summary>
        /// The full key one field of an asset would use. Public because content validation
        /// checks these exist, and it must derive them the same way rather than
        /// reimplementing the convention.
        /// </summary>
        public static string KeyFor(string keyPrefix, string field) => keyPrefix + "." + field;
    }
}
