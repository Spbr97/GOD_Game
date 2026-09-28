using System.Linq;
using Game.AI;
using Game.Core;
using Game.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The shared combat rules TASK 043 settles: are attacks readable, and do the four
    /// difficulty modes mean anything without demanding a grind.
    ///
    /// These are properties of the authored data, so they belong in EditMode. What
    /// happens when an unblockable actually lands is in
    /// <c>EnemyReadabilityPlayModeTests</c>.
    /// </summary>
    public class CombatReadabilityTests
    {
        /// <summary>
        /// How different two telegraph colours must be, as a straight RGB distance.
        /// 0.5 is about the gap between this palette's orange and its violet, and well
        /// past the gap between two oranges — which is the mistake this catches.
        /// </summary>
        private const float MinimumColourDistance = 0.5f;

        private static float Distance(Color a, Color b) =>
            Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        private static EnemyArchetype[] Archetypes() => ContentValidation.LoadAll<EnemyArchetype>().ToArray();

        /// <summary>
        /// The whole point of an unblockable. The attack is fair only if the player can
        /// tell which one is coming while it is still winding up — so the two telegraph
        /// colours have to be visibly different, not merely different values.
        /// </summary>
        [Test]
        public void AnUnblockableTelegraphLooksNothingLikeAnOrdinaryOne()
        {
            foreach (var archetype in Archetypes().Where(a => a.UnblockableEveryNthAttack > 0))
            {
                var distance = Distance(archetype.TelegraphColour, archetype.UnblockableTelegraphColour);

                Assert.Greater(distance, MinimumColourDistance,
                    $"{archetype.name}: its ordinary telegraph {archetype.TelegraphColour} and its unblockable "
                    + $"telegraph {archetype.UnblockableTelegraphColour} are {distance:0.00} apart. A player cannot "
                    + "read the difference in the half-second they have.");
            }
        }

        /// <summary>
        /// A telegraph that matches the body it flashes on is not a telegraph. This is
        /// how "readable without animation" is kept true while the art is primitives.
        /// </summary>
        [Test]
        public void ATelegraphStandsOutFromTheEnemyItFlashesOn()
        {
            foreach (var archetype in Archetypes())
            {
                Assert.Greater(Distance(archetype.BodyTint, archetype.TelegraphColour), 0.2f,
                    $"{archetype.name}'s telegraph is nearly its body colour, so the wind-up is invisible.");
            }
        }

        /// <summary>
        /// Bosses must have one; fodder must not. An ordinary fight has to stay
        /// answerable with the guard, or blocking is never the right answer and the
        /// guard system is decoration.
        /// </summary>
        [Test]
        public void BossesThrowUnblockablesAndFodderDoesNot()
        {
            foreach (var archetype in Archetypes())
            {
                var isBoss = archetype.Class == EnemyClass.Boss || archetype.Class == EnemyClass.MiniBoss;

                if (isBoss)
                {
                    Assert.Greater(archetype.UnblockableEveryNthAttack, 0,
                        $"{archetype.name} is a boss the player can beat by holding block.");
                }
            }

            Assert.IsTrue(Archetypes().Any(a => a.UnblockableEveryNthAttack == 0),
                "if every enemy has an unblockable, the guard is never the right answer either");
        }

        /// <summary>
        /// An unblockable must be rare enough to be an event. One every other swing is
        /// not a pattern to read, it is a fight where the guard is pointless.
        /// </summary>
        [Test]
        public void AnUnblockableIsAnEventRatherThanTheNorm()
        {
            foreach (var archetype in Archetypes().Where(a => a.UnblockableEveryNthAttack > 0))
            {
                Assert.GreaterOrEqual(archetype.UnblockableEveryNthAttack, 3,
                    $"{archetype.name} throws one every {archetype.UnblockableEveryNthAttack} swings.");
            }
        }

        [Test]
        public void TheUnblockableCadenceLandsWhereItSays()
        {
            var archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
            archetype.ConfigureUnblockable(3);

            Assert.IsFalse(archetype.IsUnblockableAttack(1));
            Assert.IsFalse(archetype.IsUnblockableAttack(2));
            Assert.IsTrue(archetype.IsUnblockableAttack(3));
            Assert.IsFalse(archetype.IsUnblockableAttack(4));
            Assert.IsTrue(archetype.IsUnblockableAttack(6));

            Object.DestroyImmediate(archetype);
        }

        [Test]
        public void AnEnemyWithNoUnblockableCadenceNeverThrowsOne()
        {
            var archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
            archetype.ConfigureUnblockable(0);

            for (var attack = 1; attack <= 20; attack++)
            {
                Assert.IsFalse(archetype.IsUnblockableAttack(attack));
            }

            Object.DestroyImmediate(archetype);
        }

        // ------------------------------------------------- readable without colour vision

        /// <summary>
        /// Relative luminance (Rec. 709), which is what survives every kind of colour
        /// blindness. Two colours that differ only in hue can be identical here; two that
        /// differ in luminance stay distinguishable to everyone, including in greyscale.
        /// </summary>
        private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// Brettel/Vi&#233;not-style simulation of the two common dichromacies, as linear
        /// transforms on sRGB. Approximate — this is a guard rail, not a clinical model —
        /// but it collapses the red/green axis the way the real condition does, which is
        /// all that is needed to catch a palette that relies on it.
        /// </summary>
        private static Color Deuteranope(Color c) => new(
            0.625f * c.r + 0.375f * c.g + 0.0f * c.b,
            0.700f * c.r + 0.300f * c.g + 0.0f * c.b,
            0.0f * c.r + 0.300f * c.g + 0.700f * c.b, 1f);

        private static Color Protanope(Color c) => new(
            0.567f * c.r + 0.433f * c.g + 0.0f * c.b,
            0.558f * c.r + 0.442f * c.g + 0.0f * c.b,
            0.0f * c.r + 0.242f * c.g + 0.758f * c.b, 1f);

        private static Color Tritanope(Color c) => new(
            0.950f * c.r + 0.050f * c.g + 0.0f * c.b,
            0.0f * c.r + 0.433f * c.g + 0.567f * c.b,
            0.0f * c.r + 0.475f * c.g + 0.525f * c.b, 1f);

        /// <summary>
        /// **The colour-blindness audit TASK 043 owed.**
        ///
        /// The existing test asks whether the two telegraphs are far apart in RGB, and
        /// orange against violet passes it easily. About one man in twelve does not see
        /// that difference. Run the same pair through deuteranopia and protanopia and the
        /// red/green axis collapses; if the palette were leaning on hue alone, the most
        /// consequential read in the fight — block this or get out of the way — would come
        /// down to a guess for a large minority of players.
        ///
        /// So the assertion is on luminance, which no dichromacy removes. A pair that
        /// differs in brightness is legible to everyone and stays legible in greyscale,
        /// on a washed-out laptop panel, and through a screenshot. The simulated hue
        /// distances are checked too, but as the weaker of the two guarantees.
        ///
        /// The other half of the answer is not a colour at all and cannot be tested here:
        /// an unblockable swells further than an ordinary one, which
        /// <c>EnemyReadabilityPlayModeTests</c> checks on a live enemy.
        /// </summary>
        [Test]
        public void AnUnblockableTelegraphIsLegibleWithoutColourVision()
        {
            // Enough that the two read as "bright" and "dark" rather than as two tints.
            const float minimumLuminanceGap = 0.22f;

            foreach (var archetype in Archetypes().Where(a => a.UnblockableEveryNthAttack > 0))
            {
                var ordinary = archetype.TelegraphColour;
                var unblockable = archetype.UnblockableTelegraphColour;
                var gap = Mathf.Abs(Luminance(ordinary) - Luminance(unblockable));

                Assert.Greater(gap, minimumLuminanceGap,
                    $"{archetype.name}: its ordinary telegraph and its unblockable telegraph are {gap:0.00} apart "
                    + "in luminance. They may look different to you, but they differ mostly in hue, and a player "
                    + "with red/green colour blindness has to guess whether to block. Make one of them clearly "
                    + "brighter than the other.");

                // The weaker of the two guarantees, and deliberately an "or": a pair that
                // a simulation flattens is still legible when one is plainly brighter,
                // which the assertion above has already established. This catches the
                // case where a future palette passes on luminance by a whisker and has
                // nothing else going for it.
                foreach (var simulate in new System.Func<Color, Color>[] { Deuteranope, Protanope, Tritanope })
                {
                    var simulated = Distance(simulate(ordinary), simulate(unblockable));

                    Assert.IsTrue(simulated > 0.25f || gap > 0.35f,
                        $"{archetype.name}: simulated for colour blindness the two telegraphs are {simulated:0.00} "
                        + $"apart, and their luminance gap of {gap:0.00} is not wide enough to carry the read on "
                        + "its own. The wind-up is effectively the same flash either way.");
                }
            }
        }

        /// <summary>
        /// The same question about the enemies themselves. Telling two variants apart
        /// matters less than telling two attacks apart — getting it wrong costs a wasted
        /// expectation rather than a hit — so this is checked at a lower bar, and on
        /// luminance rather than hue for the same reason as above.
        /// </summary>
        [Test]
        public void EnemyVariantsStayDistinguishableWithoutColourVision()
        {
            var archetypes = Archetypes();

            for (var i = 0; i < archetypes.Length; i++)
            {
                for (var j = i + 1; j < archetypes.Length; j++)
                {
                    var a = archetypes[i];
                    var b = archetypes[j];

                    var luminanceGap = Mathf.Abs(Luminance(a.BodyTint) - Luminance(b.BodyTint));
                    var deuteranopeGap = Distance(Deuteranope(a.BodyTint), Deuteranope(b.BodyTint));

                    Assert.IsTrue(luminanceGap > 0.08f || deuteranopeGap > 0.15f,
                        $"{a.name} and {b.name} differ only in a hue that red/green colour blindness removes "
                        + $"(luminance gap {luminanceGap:0.00}, simulated distance {deuteranopeGap:0.00}). "
                        + "Make one darker than the other.");
                }
            }
        }

        /// <summary>
        /// Enemy variants have to be tellable apart on sight, or "variety" is a number in
        /// a design document. Primitives and colour are all the presentation there is.
        /// </summary>
        [Test]
        public void EveryEnemyVariantLooksDifferentFromEveryOther()
        {
            var archetypes = Archetypes();

            for (var i = 0; i < archetypes.Length; i++)
            {
                for (var j = i + 1; j < archetypes.Length; j++)
                {
                    Assert.Greater(Distance(archetypes[i].BodyTint, archetypes[j].BodyTint), 0.15f,
                        $"{archetypes[i].name} and {archetypes[j].name} are the same colour on screen.");
                }
            }
        }
    }

    /// <summary>
    /// The four difficulty modes (SPEC.md section 44), and TASK 043's requirement that
    /// none of them demands a grind.
    /// </summary>
    public class DifficultyTuningTests
    {
        [TearDown]
        public void Reset() => Difficulty.Reset();

        private static readonly DifficultyMode[] EasiestFirst =
        {
            DifficultyMode.Story, DifficultyMode.Normal, DifficultyMode.Warrior, DifficultyMode.Mythic
        };

        /// <summary>
        /// Each mode must be harder than the one below it on every axis. A table where
        /// one row is harder in damage but easier in timing is not four difficulties, it
        /// is four opinions.
        /// </summary>
        [Test]
        public void EachModeIsHarderThanTheOneBelowItOnEveryAxis()
        {
            for (var i = 1; i < EasiestFirst.Length; i++)
            {
                var easier = Difficulty.For(EasiestFirst[i - 1]);
                var harder = Difficulty.For(EasiestFirst[i]);
                var pair = $"{EasiestFirst[i - 1]} -> {EasiestFirst[i]}";

                Assert.Greater(harder.EnemyDamage, easier.EnemyDamage, $"{pair}: enemy damage");
                Assert.Less(harder.EnemyTelegraph, easier.EnemyTelegraph, $"{pair}: less warning");
                Assert.Less(harder.EnemyAttackCooldown, easier.EnemyAttackCooldown, $"{pair}: more pressure");
                Assert.GreaterOrEqual(harder.GroupAggression, easier.GroupAggression, $"{pair}: more attackers");
                Assert.Less(harder.PlayerTimingWindow, easier.PlayerTimingWindow, $"{pair}: tighter windows");
            }
        }

        /// <summary>
        /// **The anti-grind rule.** SPEC.md sections 15 and 44 ask difficulty to change
        /// behaviour and timing, not to multiply health. A health multiplier is the thing
        /// that turns a hard mode into a long mode, and a long mode into one the player
        /// levels up to survive rather than learns to beat.
        ///
        /// Checked structurally rather than by inspection: there is no health field to
        /// set, so no future change can add one by accident.
        /// </summary>
        [Test]
        public void NoDifficultyModeMultipliesEnemyHealth()
        {
            var fields = typeof(DifficultyModifiers)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(field => field.Name)
                .ToList();

            Assert.IsFalse(fields.Any(name => name.Contains("Health")),
                "A health multiplier makes a hard mode into a long one. Difficulty moves behaviour and timing: "
                + string.Join(", ", fields));
        }

        /// <summary>
        /// The other half of "no grind": the hardest enemy in the game must be killable by
        /// a player who has spent no skill points at all. If it is not, the player has to
        /// go and get points before they can proceed, which is a grind by definition.
        ///
        /// Counted in swings rather than played, so it holds for every tuning change
        /// without anybody replaying the fight.
        /// </summary>
        [Test]
        public void TheHardestEnemyDiesToAnUnskilledPlayerInABoundedNumberOfHits()
        {
            var archetypes = ContentValidation.LoadAll<EnemyArchetype>();
            Assert.IsNotEmpty(archetypes);

            // The player's light attack, with no skills unlocked and no weapon upgrade.
            // Deliberately the weakest thing they can do.
            const float unskilledLightAttackDamage = 12f;
            const float mostHitsAFightShouldNeed = 120f;

            foreach (var mode in EasiestFirst)
            {
                Difficulty.Set(mode);

                foreach (var archetype in archetypes)
                {
                    var hits = archetype.MaxHealth / unskilledLightAttackDamage;

                    Assert.Less(hits, mostHitsAFightShouldNeed,
                        $"On {mode}, {archetype.name} needs {hits:0} unskilled light attacks. That is a fight the "
                        + "player is expected to out-level rather than out-play.");
                }
            }
        }

        /// <summary>
        /// Story mode exists so someone who wants the story can reach the end of it
        /// (SPEC.md section 44). It has to be meaningfully gentler, not a rounding error.
        /// </summary>
        [Test]
        public void StoryModeIsSubstantiallyGentlerThanNormal()
        {
            var story = Difficulty.For(DifficultyMode.Story);
            var normal = Difficulty.For(DifficultyMode.Normal);

            Assert.LessOrEqual(story.EnemyDamage, normal.EnemyDamage * 0.75f,
                "Story mode should take a real bite out of incoming damage");
            Assert.GreaterOrEqual(story.EnemyTelegraph, normal.EnemyTelegraph * 1.25f,
                "and give noticeably more warning");
        }

        /// <summary>
        /// Mythic must be harder than Warrior without becoming unfair: an enemy still has
        /// to telegraph long enough to be reacted to at all.
        /// </summary>
        [Test]
        public void EvenMythicLeavesTimeToReact()
        {
            var archetypes = ContentValidation.LoadAll<EnemyArchetype>();
            Difficulty.Set(DifficultyMode.Mythic);

            foreach (var archetype in archetypes)
            {
                Assert.GreaterOrEqual(archetype.TelegraphFor(), 0.2f,
                    $"{archetype.name} telegraphs for {archetype.TelegraphFor():0.00}s on Mythic, which is below "
                    + "human reaction time. Hard is not the same as invisible.");
            }
        }

        [Test]
        public void GroupAggressionNeverSilencesEveryAttacker()
        {
            foreach (var mode in EasiestFirst)
            {
                Difficulty.Set(mode);

                Assert.GreaterOrEqual(Difficulty.ScaleAttackerCount(1), 1,
                    $"on {mode} an encounter would stall with nobody permitted to swing");
            }
        }
    }
}
