using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Published by whatever the player used to leave a scene — a door, a trigger, a
    /// cinematic. Deliberately an event rather than a call into the save system:
    /// carrying progression across the load is the save system's job (SPEC.md section
    /// 58 — Save depends on everything and nothing depends on Save), so a door asks for
    /// travel and never learns who arranges it.
    /// </summary>
    public readonly struct SceneTravelRequestedEvent
    {
        public readonly string TargetScene;

        /// <summary>The <c>SceneSpawnPoint</c> id to arrive at. Empty means "wherever the destination puts a new arrival".</summary>
        public readonly string SpawnId;

        /// <summary>For the log and for a loading screen. Not player-facing wording.</summary>
        public readonly string Reason;

        public SceneTravelRequestedEvent(string targetScene, string spawnId, string reason = null)
        {
            TargetScene = targetScene;
            SpawnId = spawnId;
            Reason = reason;
        }
    }

    /// <summary>Raised once travel has been accepted and the load is about to begin.</summary>
    public readonly struct SceneTravelStartedEvent
    {
        public readonly string FromScene;
        public readonly string TargetScene;
        public readonly string SpawnId;

        public SceneTravelStartedEvent(string fromScene, string targetScene, string spawnId)
        {
            FromScene = fromScene;
            TargetScene = targetScene;
            SpawnId = spawnId;
        }
    }

    /// <summary>
    /// Raised in the destination scene once progression has been carried over and the
    /// player has been placed. <see cref="PlacedAtSpawn"/> is false when the named
    /// spawn point could not be found, which is a content error worth a test.
    /// </summary>
    public readonly struct SceneArrivedEvent
    {
        public readonly string SceneName;
        public readonly string SpawnId;
        public readonly bool PlacedAtSpawn;

        public SceneArrivedEvent(string sceneName, string spawnId, bool placedAtSpawn)
        {
            SceneName = sceneName;
            SpawnId = spawnId;
            PlacedAtSpawn = placedAtSpawn;
        }
    }

    /// <summary>
    /// The hand-off across a gameplay scene load (TASK 041).
    ///
    /// Static for the same reason <c>SaveManager.PendingLoad</c> is: the scene being
    /// left and the scene being entered never coexist as loaded objects that could pass
    /// this to each other. Nothing here is written to disk — a save file records where
    /// the player is, not that they were in transit, because a save taken mid-transit is
    /// exactly what <see cref="SaveBlockReason"/> exists to prevent.
    ///
    /// <see cref="TryConsume"/> is keyed on the destination scene's name so an arrival
    /// intended for one scene cannot be applied in another. Without that check a failed
    /// load, or a player who reaches the Main Menu instead, leaves a spawn id sitting in
    /// a static that the next gameplay scene would honour — which is one of the ways a
    /// player ends up standing somewhere the save never said they were.
    /// </summary>
    public static class SceneTravel
    {
        /// <summary>
        /// The reason string <c>SaveManager</c> blocks saves with while travel is in
        /// flight. A save written between "the door opened" and "the destination is
        /// live" would record a half-torn-down scene: SPEC.md section 31's rule against
        /// saving during a critical state transition.
        /// </summary>
        public const string SaveBlockReason = "the world is changing around you";

        public static string PendingScene { get; private set; }

        public static string PendingSpawnId { get; private set; }

        /// <summary>True between <see cref="Begin"/> and <see cref="TryConsume"/> or <see cref="Clear"/>.</summary>
        public static bool IsTravelling => !string.IsNullOrEmpty(PendingScene);

        public static void Begin(string targetScene, string spawnId)
        {
            if (string.IsNullOrEmpty(targetScene))
            {
                GameLogger.LogWarning(LogCategory.Game, "Ignored scene travel with no destination.");
                return;
            }

            PendingScene = targetScene;
            PendingSpawnId = spawnId;
        }

        /// <summary>
        /// Takes the pending arrival if it was meant for <paramref name="sceneName"/>,
        /// clearing it either way it matches. A mismatch leaves the record alone and
        /// returns false, so a scene that is not the destination neither steals the
        /// arrival nor discards it.
        /// </summary>
        public static bool TryConsume(string sceneName, out string spawnId)
        {
            spawnId = null;

            if (!IsTravelling || !string.Equals(PendingScene, sceneName, System.StringComparison.Ordinal))
            {
                return false;
            }

            spawnId = PendingSpawnId;
            Clear();
            return true;
        }

        /// <summary>
        /// Abandons a pending arrival. Called when a load fails and when the game
        /// returns to the Main Menu, both of which end the journey.
        /// </summary>
        public static void Clear()
        {
            PendingScene = null;
            PendingSpawnId = null;
        }
    }
}
