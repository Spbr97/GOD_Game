using Game.Audio;
using Game.VFX;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Tests for TASK 018's placeholder VFX/audio spawners. Both are pure, stateless
    /// factories with no scene dependency, so — unlike most of this project's
    /// presentation code — they need no isolated scene of their own.
    /// </summary>
    public class PresentationTests
    {
        [Test]
        public void VfxSpawner_BuildsAParticleSystemConfiguredForTheRequestedKind()
        {
            var go = VfxSpawner.Spawn(VfxKind.Spark, new Vector3(1f, 2f, 3f));
            try
            {
                Assert.AreEqual(new Vector3(1f, 2f, 3f), go.transform.position);

                var particles = go.GetComponent<ParticleSystem>();
                Assert.IsNotNull(particles);
                Assert.IsFalse(particles.main.loop);

                var emission = particles.emission;
                Assert.AreEqual(1, emission.burstCount);
                Assert.Greater(emission.GetBurst(0).count.constant, 0);

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                Assert.IsNotNull(renderer.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void VfxSpawner_DifferentKindsGetDifferentColours()
        {
            var fire = VfxSpawner.Spawn(VfxKind.Fire, Vector3.zero);
            var divine = VfxSpawner.Spawn(VfxKind.DivineEnergy, Vector3.zero);
            try
            {
                var fireColour = fire.GetComponent<ParticleSystem>().main.startColor.color;
                var divineColour = divine.GetComponent<ParticleSystem>().main.startColor.color;
                Assert.AreNotEqual(fireColour, divineColour);
            }
            finally
            {
                Object.DestroyImmediate(fire);
                Object.DestroyImmediate(divine);
            }
        }

        [Test]
        public void SfxSpawner_PlaysAGeneratedClipAtThePosition()
        {
            SfxSpawner.Play(SfxKind.Parry, new Vector3(4f, 5f, 6f));

            var go = GameObject.Find("Sfx_Parry");
            try
            {
                Assert.IsNotNull(go);
                Assert.AreEqual(new Vector3(4f, 5f, 6f), go.transform.position);

                var source = go.GetComponent<AudioSource>();
                Assert.IsNotNull(source.clip);
                Assert.Greater(source.clip.length, 0f);
                Assert.AreEqual(1, source.clip.channels);
            }
            finally
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void SfxSpawner_CachesTheSameClipAcrossCalls()
        {
            SfxSpawner.Play(SfxKind.Block, Vector3.zero);
            var first = GameObject.Find("Sfx_Block")?.GetComponent<AudioSource>()?.clip;
            Object.DestroyImmediate(GameObject.Find("Sfx_Block"));

            SfxSpawner.Play(SfxKind.Block, Vector3.zero);
            var second = GameObject.Find("Sfx_Block")?.GetComponent<AudioSource>()?.clip;
            Object.DestroyImmediate(GameObject.Find("Sfx_Block"));

            Assert.AreSame(first, second);
        }
    }
}
