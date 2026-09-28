using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A door out of one gameplay scene and into another (TASK 041) — the temple
    /// entrance in Avarsha, and the way back out of the temple.
    ///
    /// It knows the destination scene's name and an arrival point's id, and nothing
    /// else. It does not save, does not load the scene itself and does not touch the
    /// progression managers: it publishes <see cref="SceneTravelRequestedEvent"/> and
    /// the save system answers. That keeps SPEC.md section 58's one-way dependency
    /// intact, and it means a cinematic or a scripted story beat can send the player
    /// somewhere by publishing the same event without there being a door at all.
    ///
    /// An exit is an <see cref="Interactable"/> rather than a walk-through trigger on
    /// purpose. Travel discards the scene the player is standing in; doing that because
    /// somebody backed into a collider during a fight is the kind of thing players
    /// rightly call a bug, and the inherited <c>requiredFlags</c>/<c>blockingFlags</c>
    /// give content a way to lock a door until the story opens it.
    /// </summary>
    public class SceneExit : Interactable
    {
        [Header("Destination")]
        [Tooltip("Scene name as it appears in Build Settings, without a path or extension.")]
        [SerializeField] private string targetScene;

        [Tooltip("The SceneSpawnPoint id to arrive at, in the target scene.")]
        [SerializeField] private string targetSpawnId;

        [Tooltip("Set when this exit is used. Optional; lets content react to a first visit.")]
        [SerializeField] private string flagOnUse;

        public string TargetScene => targetScene;

        public string TargetSpawnId => targetSpawnId;

        public string FlagOnUse => flagOnUse;

        /// <summary>
        /// Refused while a load is already in flight or travel is pending, on top of the
        /// inherited flag gates. Pressing Interact twice on a door is ordinary player
        /// behaviour and must not queue two journeys.
        /// </summary>
        public override bool CanInteract =>
            !string.IsNullOrEmpty(targetScene)
            && !GameSceneManager.IsLoading
            && !SceneTravel.IsTravelling
            && base.CanInteract;

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract)
            {
                return;
            }

            if (!string.IsNullOrEmpty(flagOnUse))
            {
                SetFlag(flagOnUse);
            }

            GameLogger.Log(LogCategory.Game,
                $"Exit '{name}' leaving for '{targetScene}' at spawn '{targetSpawnId}'.", this);

            EventBus.Publish(new SceneTravelRequestedEvent(targetScene, targetSpawnId, $"exit '{name}'"));
        }

        /// <summary>Every exit in the loaded scenes, for validation and for tests.</summary>
        public static List<SceneExit> All()
        {
            var found = new List<SceneExit>();
            var exits = Object.FindObjectsByType<SceneExit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < exits.Length; i++)
            {
                found.Add(exits[i]);
            }

            return found;
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(string scene, string spawnId, string useFlag = null)
        {
            targetScene = scene;
            targetSpawnId = spawnId;
            flagOnUse = useFlag;
        }
    }
}
