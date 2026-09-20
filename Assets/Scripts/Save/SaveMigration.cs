using Game.Core;

namespace Game.Save
{
    /// <summary>
    /// Brings an older save up to <see cref="SaveData.CurrentVersion"/> (SPEC.md
    /// section 53: version migration is a named test case).
    ///
    /// There is only one version so far, so there is nothing to migrate yet. The
    /// machinery exists now rather than later because the first migration is written
    /// under pressure — a player's save is already broken by then — and because the
    /// alternative is a version check scattered across whatever reads the data.
    ///
    /// The rules for adding a step, when the day comes:
    /// <list type="bullet">
    /// <item>Migrate forward only. A save is never written back to an older shape.</item>
    /// <item>One step per version, applied in order, each leaving the data valid.</item>
    /// <item>Never drop data you cannot interpret; leave it and let the next step decide.</item>
    /// </list>
    /// </summary>
    public static class SaveMigration
    {
        /// <summary>
        /// Upgrades <paramref name="data"/> in place. Returns false when the save is
        /// from a version this build cannot reach, with the reason in
        /// <paramref name="detail"/>.
        /// </summary>
        public static bool TryMigrate(SaveData data, out string detail)
        {
            detail = null;

            if (data == null)
            {
                detail = "there was no save data to migrate";
                return false;
            }

            if (data.Version == SaveData.CurrentVersion)
            {
                return true;
            }

            if (data.Version > SaveData.CurrentVersion)
            {
                detail = $"save version {data.Version} is newer than this build's {SaveData.CurrentVersion}";
                return false;
            }

            var from = data.Version;

            while (data.Version < SaveData.CurrentVersion)
            {
                switch (data.Version)
                {
                    // case 1: MigrateOneToTwo(data); break;

                    default:
                        detail = $"no migration step exists from save version {data.Version}";
                        return false;
                }
            }

            GameLogger.Log(LogCategory.Game, $"Migrated a save from version {from} to {data.Version}.");
            return true;
        }
    }
}
