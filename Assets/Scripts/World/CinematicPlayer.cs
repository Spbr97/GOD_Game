using System;
using System.Collections;
using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>One beat of a cinematic: a line of subtitle text and how long it holds.</summary>
    [Serializable]
    public struct CinematicBeat
    {
        [TextArea(2, 4)]
        public string Subtitle;

        public float Duration;

        [Tooltip("Id of the narration line. No audio exists yet; see KNOWN_ISSUES.md.")]
        public string AudioNarrationId;
    }

    /// <summary>
    /// A skippable, subtitled cinematic (SPEC.md section 69, TASK 014): the "one
    /// cinematic" section 61 asks the vertical slice to contain.
    ///
    /// Deliberately minimal rather than a full timeline/track system: SPEC.md section
    /// 69 itself warns against making every story beat a cutscene, and the ambient
    /// world-state sequence (<see cref="SupernaturalEvent"/>) already demonstrates
    /// "gameplay storytelling" for the scenes that do not need a director. This class
    /// is for the scenes that do: it takes the world state (<see cref="GameManager.EnterCutscene"/>),
    /// plays subtitles at their own pace, and hands control back. A beat has no camera
    /// or actor data because nothing has needed one yet; both are natural additions to
    /// <see cref="CinematicBeat"/> once a cinematic wants them.
    /// </summary>
    public class CinematicPlayer : MonoBehaviour
    {
        [SerializeField] private string cinematicId = "CINEMATIC_UNNAMED";

        [Tooltip("Flag whose setting starts this cinematic. Leave empty to start only by direct call.")]
        [SerializeField] private string triggerFlag;

        [Tooltip("Flag set once the cinematic finishes, whether played out or skipped.")]
        [SerializeField] private string completionFlag;

        [Tooltip("Objective reported once the cinematic finishes. Optional.")]
        [SerializeField] private string objectiveId;

        [SerializeField] private CinematicBeat[] beats;

        private Coroutine routine;
        private bool skipRequested;
        private bool finished;

        public string CinematicId => cinematicId;
        public bool HasPlayed { get; private set; }
        public bool IsPlaying => routine != null;

        /// <summary>-1 before playing starts; the last beat's index once finished.</summary>
        public int CurrentBeatIndex { get; private set; } = -1;

        private void OnEnable()
        {
            EventBus.Subscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        private void OnPlayerDied(PlayerDiedEvent died)
        {
            // An external hazard or scripted kill can occur while the world is
            // frozen. Complete the beat and release cutscene state before respawn.
            if (IsPlaying)
            {
                ApplyEndStateImmediately();
            }
        }

        private void OnWorldFlagChanged(WorldFlagChangedEvent changed)
        {
            if (changed.Value && !string.IsNullOrEmpty(triggerFlag) && changed.Flag == triggerFlag)
            {
                Play();
            }
        }

        /// <summary>Starts the sequence. Ignored if it has already played, is playing, or has no beats.</summary>
        public bool Play()
        {
            if (HasPlayed || IsPlaying || beats == null || beats.Length == 0 || !isActiveAndEnabled)
            {
                return false;
            }

            skipRequested = false;
            routine = StartCoroutine(Sequence());
            return true;
        }

        /// <summary>
        /// Ends the cinematic on the spot — SPEC.md section 69's "skippable"
        /// requirement. Safe to call whether or not one is currently playing.
        /// </summary>
        public void Skip()
        {
            if (!IsPlaying)
            {
                return;
            }

            skipRequested = true;
        }

        /// <summary>
        /// Applies the cinematic's completed state immediately, with no sequence — for
        /// a save loaded after it already played, and for tests that must not wait out
        /// every beat's duration. See <see cref="Game.World.SupernaturalEvent.ApplyEndStateImmediately"/>
        /// for the same idea applied to the other TASK 003 scripted sequence.
        /// </summary>
        public void ApplyEndStateImmediately()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            Finish(skipped: false);
        }

        private IEnumerator Sequence()
        {
            HasPlayed = true;
            GameManager.Instance?.EnterCutscene();
            GameLogger.Log(LogCategory.Game, $"Cinematic '{cinematicId}' began.", this);
            EventBus.Publish(new CinematicStartedEvent(cinematicId, this));

            for (var i = 0; i < beats.Length; i++)
            {
                if (skipRequested)
                {
                    break;
                }

                CurrentBeatIndex = i;
                EventBus.Publish(new CinematicBeatShownEvent(cinematicId, i, beats[i].Subtitle));

                // Unscaled: EnterCutscene freezes Time.timeScale, and a beat's own pace
                // must not freeze along with the world it just paused.
                var remaining = Mathf.Max(0f, beats[i].Duration);
                while (remaining > 0f && !skipRequested)
                {
                    remaining -= Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            var wasSkipped = skipRequested;
            routine = null;

            if (wasSkipped)
            {
                EventBus.Publish(new CinematicSkippedEvent(cinematicId));
            }

            Finish(wasSkipped);
        }

        private void Finish(bool skipped)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            HasPlayed = true;
            CurrentBeatIndex = beats != null ? beats.Length - 1 : -1;

            GameManager.Instance?.ExitCutscene();

            if (!string.IsNullOrEmpty(completionFlag))
            {
                WorldState.Instance?.SetFlag(completionFlag);
            }

            if (!string.IsNullOrEmpty(objectiveId))
            {
                Game.Quests.QuestManager.Instance?.ReportObjective(objectiveId);
            }

            GameLogger.Log(LogCategory.Game, $"Cinematic '{cinematicId}' completed{(skipped ? " (skipped)" : string.Empty)}.", this);
            EventBus.Publish(new CinematicCompletedEvent(cinematicId));
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(string id, string startFlag, string endFlag, CinematicBeat[] cinematicBeats, string objective = null)
        {
            cinematicId = id;
            triggerFlag = startFlag;
            completionFlag = endFlag;
            beats = cinematicBeats;
            objectiveId = objective;
        }
    }
}
