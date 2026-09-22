using System.Collections.Generic;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>SPEC.md section 36's effect categories this pass actually wires up.</summary>
    public enum VfxKind
    {
        Fire,
        Spark,
        Dust,
        DivineEnergy,
        MemoryFragment,
        Glyph,
        BossTransformation
    }

    /// <summary>
    /// Placeholder VFX (SPEC.md section 36, TASK 018, section 78's placeholder-first
    /// rule): a short-lived coloured particle burst built entirely from a primitive
    /// <see cref="ParticleSystem"/> rather than an authored effect, so combat and
    /// world systems have something visible to react to without depending on final
    /// art. Every call is self-contained — spawn, play, destroy once finished — so a
    /// caller never has to track or clean up what it started.
    /// </summary>
    public static class VfxSpawner
    {
        private static Material sharedMaterial;

        public static GameObject Spawn(VfxKind kind, Vector3 position)
        {
            var go = new GameObject($"Vfx_{kind}");
            go.transform.position = position;

            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.startLifetime = LifetimeFor(kind);
            main.startSpeed = SpeedFor(kind);
            main.startSize = SizeFor(kind);
            main.startColor = ColourFor(kind);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, BurstCountFor(kind)) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SharedMaterial();

            particles.Play();
            DestroyAfter(go, main.duration + LifetimeFor(kind));
            return go;
        }

        /// <summary>
        /// <see cref="Object.Destroy(Object, float)"/>'s delayed form is runtime-only —
        /// it no-ops with a console error in EditMode, which real EditMode tests
        /// (<c>PuzzleTests</c>, via <see cref="Game.World.PuzzleController"/>) actually
        /// exercise. Left alone there instead: an EditMode test's isolated scene is
        /// short-lived anyway, and a caller inspecting the returned GameObject would
        /// otherwise have it vanish out from under it on an immediate destroy.
        /// </summary>
        private static void DestroyAfter(GameObject go, float seconds)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(go, seconds);
            }
        }

        private static Material SharedMaterial()
        {
            // Deliberately `== null`, not `??=`: a domain reload (recompile, or
            // entering/exiting Play mode) can destroy the cached Material as a native
            // object while the static C# field still references it — Unity's "fake
            // null". `??=`'s `is null` pattern does not call Unity's `==` override and
            // would miss that, leaving a destroyed material assigned to every renderer.
            if (sharedMaterial == null)
            {
                // "Sprites/Default" is built into every render pipeline (URP
                // included), so a placeholder particle needs no pipeline-specific
                // shader asset.
                sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            return sharedMaterial;
        }

        private static readonly Dictionary<VfxKind, (float lifetime, float speed, float size, int burst, Color colour)> Presets = new()
        {
            [VfxKind.Fire] = (0.6f, 1.2f, 0.25f, 10, new Color(1f, 0.45f, 0.1f)),
            [VfxKind.Spark] = (0.25f, 4f, 0.08f, 14, new Color(1f, 0.95f, 0.4f)),
            [VfxKind.Dust] = (0.8f, 0.6f, 0.3f, 8, new Color(0.6f, 0.55f, 0.45f, 0.6f)),
            [VfxKind.DivineEnergy] = (0.5f, 2f, 0.18f, 16, new Color(0.5f, 0.8f, 1f)),
            [VfxKind.MemoryFragment] = (1f, 0.8f, 0.15f, 10, new Color(0.8f, 0.6f, 1f)),
            [VfxKind.Glyph] = (1.2f, 0.3f, 0.2f, 8, new Color(1f, 0.85f, 0.3f)),
            [VfxKind.BossTransformation] = (1.5f, 1.5f, 0.35f, 30, new Color(0.7f, 0.1f, 0.9f)),
        };

        private static float LifetimeFor(VfxKind kind) => Presets[kind].lifetime;
        private static float SpeedFor(VfxKind kind) => Presets[kind].speed;
        private static float SizeFor(VfxKind kind) => Presets[kind].size;
        private static int BurstCountFor(VfxKind kind) => Presets[kind].burst;
        private static Color ColourFor(VfxKind kind) => Presets[kind].colour;
    }
}
