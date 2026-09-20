using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>Raised when a scripted supernatural event begins and when it finishes.</summary>
    public readonly struct SupernaturalEventStartedEvent
    {
        public readonly string EventId;

        public SupernaturalEventStartedEvent(string eventId)
        {
            EventId = eventId;
        }
    }

    public readonly struct SupernaturalEventCompletedEvent
    {
        public readonly string EventId;

        public SupernaturalEventCompletedEvent(string eventId)
        {
            EventId = eventId;
        }
    }

    /// <summary>
    /// The first supernatural event in Avarsha (SPEC.md section 68, scenes 5 and 7:
    /// everyone falls asleep, then the statues begin bleeding).
    ///
    /// Everything it does is a placeholder standing in for real VFX and animation
    /// (SPEC.md section 78): sleeping is a rotation, bleeding is a colour, and the
    /// change in the world is a light and fog shift following the colour language in
    /// SPEC.md section 35. The sequence, its ordering and the flags it sets are the
    /// real content; the presentation is meant to be replaced.
    /// </summary>
    public class SupernaturalEvent : MonoBehaviour
    {
        [SerializeField] private string eventId = "AVARSHA_FIRST_SLEEP";

        [Tooltip("Flag whose setting starts this event. Leave empty to start it only by direct call.")]
        [SerializeField] private string triggerFlag;

        [Tooltip("Flag set once the event finishes.")]
        [SerializeField] private string completionFlag = WorldFlags.FirstSupernaturalEvent;

        [Tooltip("Objective reported once the event finishes. Optional.")]
        [SerializeField] private string objectiveId;

        [Header("Cast")]
        [Tooltip("NPCs that collapse. Placeholder for a sleep animation.")]
        [SerializeField] private Transform[] sleepers;

        [Tooltip("Statues that bleed. Placeholder for a bleeding VFX.")]
        [SerializeField] private Renderer[] statues;

        [Header("Timing")]
        [SerializeField] private float sleepDuration = 1.2f;
        [SerializeField] private float pauseBeforeStatues = 0.8f;
        [SerializeField] private float bleedDuration = 1.5f;

        [Header("Atmosphere")]
        [SerializeField] private Light keyLight;
        [SerializeField] private Color drainedLightColour = new(0.55f, 0.58f, 0.72f);
        [SerializeField] private float drainedLightIntensity = 0.45f;
        [SerializeField] private Color bleedColour = new(0.5f, 0.05f, 0.07f);

        private Coroutine routine;

        public bool HasPlayed { get; private set; }
        public bool IsPlaying => routine != null;
        public string EventId => eventId;

        private void OnEnable()
        {
            EventBus.Subscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
        }

        private void OnWorldFlagChanged(WorldFlagChangedEvent changed)
        {
            if (changed.Value && !string.IsNullOrEmpty(triggerFlag) && changed.Flag == triggerFlag)
            {
                Play();
            }
        }

        /// <summary>Starts the sequence. Ignored if it has already played or is playing.</summary>
        public bool Play()
        {
            if (HasPlayed || IsPlaying || !isActiveAndEnabled)
            {
                return false;
            }

            routine = StartCoroutine(Sequence());
            return true;
        }

        /// <summary>
        /// Applies the event's end state immediately, with no sequence.
        ///
        /// This exists so a save loaded after the event, or a test that must not wait
        /// several seconds, lands in the same world state the sequence produces.
        /// </summary>
        public void ApplyEndStateImmediately()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            SetSleepersLyingDown(1f);
            SetStatueBleed(1f);
            SetAtmosphere(1f);
            Finish();
        }

        private IEnumerator Sequence()
        {
            HasPlayed = true;
            GameLogger.Log(LogCategory.Game, $"Supernatural event '{eventId}' began.", this);
            EventBus.Publish(new SupernaturalEventStartedEvent(eventId));

            // Scene 5: everyone falls asleep. The light drains at the same time, so the
            // player reads the two as one event rather than a lighting glitch.
            yield return Over(sleepDuration, t =>
            {
                SetSleepersLyingDown(t);
                SetAtmosphere(t);
            });

            yield return new WaitForSeconds(pauseBeforeStatues);

            // Scene 7: the statues begin bleeding.
            yield return Over(bleedDuration, SetStatueBleed);

            routine = null;
            Finish();
        }

        private void Finish()
        {
            HasPlayed = true;

            if (!string.IsNullOrEmpty(completionFlag))
            {
                WorldState.Instance?.SetFlag(completionFlag);
            }

            if (!string.IsNullOrEmpty(objectiveId))
            {
                Game.Quests.QuestManager.Instance?.ReportObjective(objectiveId);
            }

            GameLogger.Log(LogCategory.Game, $"Supernatural event '{eventId}' completed.", this);
            EventBus.Publish(new SupernaturalEventCompletedEvent(eventId));
        }

        private static IEnumerator Over(float duration, System.Action<float> step)
        {
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                step(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            step(1f);
        }

        private void SetSleepersLyingDown(float t)
        {
            if (sleepers == null)
            {
                return;
            }

            for (var i = 0; i < sleepers.Length; i++)
            {
                var sleeper = sleepers[i];
                if (sleeper == null)
                {
                    continue;
                }

                // Rotated about their own forward axis and lowered, so a capsule ends
                // up on its side rather than sunk into the ground.
                var yaw = sleeper.eulerAngles.y;
                sleeper.rotation = Quaternion.Euler(0f, yaw, Mathf.Lerp(0f, 90f, t));
            }
        }

        private void SetStatueBleed(float t)
        {
            if (statues == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            for (var i = 0; i < statues.Length; i++)
            {
                var statue = statues[i];
                if (statue == null)
                {
                    continue;
                }

                statue.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Color.Lerp(Color.white, bleedColour, t));
                statue.SetPropertyBlock(block);
            }
        }

        private void SetAtmosphere(float t)
        {
            if (keyLight == null)
            {
                return;
            }

            keyLight.color = Color.Lerp(Color.white, drainedLightColour, t);
            keyLight.intensity = Mathf.Lerp(1f, drainedLightIntensity, t);
        }

        /// <summary>Test and tooling seam for wiring the cast without the Inspector.</summary>
        public void Configure(string id, string startFlag, Transform[] sleepingNpcs, Renderer[] bleedingStatues, Light light)
        {
            eventId = id;
            triggerFlag = startFlag;
            sleepers = sleepingNpcs;
            statues = bleedingStatues;
            keyLight = light;
        }
    }
}
