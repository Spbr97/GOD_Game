using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>SPEC.md section 40's combat/boss cues this pass actually wires up.</summary>
    public enum SfxKind
    {
        WeaponImpact,
        Parry,
        Block,
        GuardBreak,
        DivineAbility,
        BossPhaseTransition,
        MemoryDiscovered,
        PuzzleSolved
    }

    /// <summary>
    /// Placeholder audio (SPEC.md section 40, TASK 018, section 78's
    /// placeholder-first rule): a short procedurally generated tone rather than an
    /// imported clip, since no audio asset exists yet for any of these cues and none
    /// can be authored without external tools. Each <see cref="SfxKind"/>'s tone is
    /// generated once and cached — the placeholder is fixed and cheap to reuse, only
    /// the emitting GameObject is per-call and self-destroying.
    /// </summary>
    public static class SfxSpawner
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<SfxKind, AudioClip> Cache = new();

        public static void Play(SfxKind kind, Vector3 position)
        {
            var clip = GetClip(kind);

            var go = new GameObject($"Sfx_{kind}");
            go.transform.position = position;

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.Play();

            // See Game.VFX.VfxSpawner.DestroyAfter: the delayed Destroy is runtime-only
            // and no-ops with a console error in EditMode, which real EditMode tests
            // (via MemoryPickup/PuzzleController) actually exercise.
            if (Application.isPlaying)
            {
                Object.Destroy(go, clip.length);
            }
        }

        private static AudioClip GetClip(SfxKind kind)
        {
            if (Cache.TryGetValue(kind, out var cached) && cached != null)
            {
                return cached;
            }

            var (frequency, duration) = ToneFor(kind);
            var clip = GenerateTone(kind, frequency, duration);
            Cache[kind] = clip;
            return clip;
        }

        private static AudioClip GenerateTone(SfxKind kind, float frequency, float duration)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = 1f - t / duration;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.5f;
            }

            var clip = AudioClip.Create($"PlaceholderTone_{kind}", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static readonly Dictionary<SfxKind, (float frequency, float duration)> Presets = new()
        {
            [SfxKind.WeaponImpact] = (180f, 0.12f),
            [SfxKind.Parry] = (880f, 0.15f),
            [SfxKind.Block] = (260f, 0.18f),
            [SfxKind.GuardBreak] = (110f, 0.35f),
            [SfxKind.DivineAbility] = (660f, 0.4f),
            [SfxKind.BossPhaseTransition] = (140f, 0.9f),
            [SfxKind.MemoryDiscovered] = (990f, 0.5f),
            [SfxKind.PuzzleSolved] = (740f, 0.6f),
        };

        private static (float frequency, float duration) ToneFor(SfxKind kind) => Presets[kind];
    }
}
