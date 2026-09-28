using Game.Core;

namespace Game.Save
{
    /// <summary>
    /// Brings an older save up to <see cref="SaveData.CurrentVersion"/> (SPEC.md
    /// section 53: version migration is a named test case).
    ///
    /// The rules for adding a step:
    /// <list type="bullet">
    /// <item>Migrate forward only. A save is never written back to an older shape.</item>
    /// <item>One step per version, applied in order, each leaving the data valid.</item>
    /// <item>Never drop data you cannot interpret; leave it and let the next step decide.</item>
    /// <item>Add a fixture. <c>Assets/Tests/Fixtures</c> holds a real file written by the
    /// older build, and a test asserts what the step makes of it. A migration verified
    /// only against a hand-built object is verified against this build's idea of the old
    /// shape, which is the thing in doubt.</item>
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
                    case 1:
                        OneToTwo(data);
                        break;

                    default:
                        detail = $"no migration step exists from save version {data.Version}";
                        return false;
                }
            }

            GameLogger.Log(LogCategory.Game, $"Migrated a save from version {from} to {data.Version}.");
            return true;
        }

        /// <summary>
        /// Version 1 → 2 (TASK 041): move the single top-level position, rotation and
        /// checkpoint into a per-scene record for the scene the save was written in.
        ///
        /// A version 1 save was necessarily written in a single-gameplay-scene build, so
        /// the one position it holds belongs to <see cref="SaveData.SceneName"/> and to no
        /// other scene. The top-level fields are left in place rather than cleared: they
        /// still mean "the scene to resume in, and where", and <c>SaveBrowser</c> reads
        /// them.
        ///
        /// A version 1 save with no scene name is one written before the player reached
        /// gameplay, or a corrupt one. There is no scene to attribute the position to, so
        /// no record is added and the player arrives at the destination's default spawn.
        /// That is the behaviour version 1 already had.
        /// </summary>
        private static void OneToTwo(SaveData data)
        {
            if (!string.IsNullOrEmpty(data.SceneName))
            {
                var scene = data.EnsureStateFor(data.SceneName);
                scene.PlayerPosition = data.PlayerPosition;
                scene.PlayerRotation = data.PlayerRotation;
                scene.CheckpointId = data.CheckpointId;
            }

            data.Version = 2;
        }
    }
}
