using Game.AI;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// <see cref="BossController.PhaseFor"/>, the section 18 phase table, as a pure
    /// function — the same reasoning as <see cref="EnemyController.Decide"/>: no
    /// scene, no lifecycle, no EventBus needed to regression-test which phase a given
    /// health fraction lands in. The framework's wiring (encounter start, reward
    /// reveal, save persistence) needs a live scene and is covered by
    /// <c>BossPlayModeTests</c> instead, for the same OnEnable/EventBus reason
    /// <see cref="PuzzleTests"/>'s wiring moved to PlayMode.
    /// </summary>
    public class BossTests
    {
        [Test]
        public void Phase_StaysAtOneAboveBothThresholds()
        {
            Assert.AreEqual(1, BossController.PhaseFor(1f, 0.66f, 0.33f));
            Assert.AreEqual(1, BossController.PhaseFor(0.67f, 0.66f, 0.33f));
        }

        [Test]
        public void Phase_EntersTwoAtOrBelowTheSecondThreshold()
        {
            Assert.AreEqual(2, BossController.PhaseFor(0.66f, 0.66f, 0.33f));
            Assert.AreEqual(2, BossController.PhaseFor(0.4f, 0.66f, 0.33f));
        }

        [Test]
        public void Phase_EntersThreeAtOrBelowTheThirdThreshold()
        {
            Assert.AreEqual(3, BossController.PhaseFor(0.33f, 0.66f, 0.33f));
            Assert.AreEqual(3, BossController.PhaseFor(0f, 0.66f, 0.33f));
        }

        [Test]
        public void Phase_ASingleBigHitSkipsStraightToThree()
        {
            // Going from full health straight to below the phase 3 threshold in one hit
            // must not stop at phase 2 — the caller compares against the highest phase
            // the current health implies, not against the previous phase plus one.
            Assert.AreEqual(3, BossController.PhaseFor(0.1f, 0.66f, 0.33f));
        }

        [Test]
        public void Phase_ZeroThresholdsMeanNoPhaseChanges()
        {
            Assert.AreEqual(1, BossController.PhaseFor(0.5f, 0f, 0f));
            Assert.AreEqual(3, BossController.PhaseFor(0f, 0f, 0f));
        }
    }
}
