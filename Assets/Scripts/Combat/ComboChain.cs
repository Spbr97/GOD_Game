using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>One input in a combo chain (SPEC.md section 14).</summary>
    public enum ComboStep
    {
        Light,
        Heavy,
        Dodge,
        Parry,
        Ability
    }

    /// <summary>
    /// A named sequence of steps and the reward for landing it. The reward is a
    /// damage multiplier on the swing that completes the chain.
    /// </summary>
    [Serializable]
    public sealed class ComboChain
    {
        public string Name;
        public ComboStep[] Steps;
        public float DamageMultiplier = 1f;

        public ComboChain(string name, float damageMultiplier, params ComboStep[] steps)
        {
            Name = name;
            DamageMultiplier = damageMultiplier;
            Steps = steps;
        }

        /// <summary>
        /// The minimum set from SPEC.md section 14. Multipliers are placeholder
        /// tuning values; the finisher is not a chain and is handled separately.
        /// </summary>
        public static ComboChain[] DefaultChains()
        {
            return new[]
            {
                new ComboChain("Triple Light", 1.3f, ComboStep.Light, ComboStep.Light, ComboStep.Light),
                new ComboChain("Light Light Heavy", 1.6f, ComboStep.Light, ComboStep.Light, ComboStep.Heavy),
                new ComboChain("Light Heavy", 1.25f, ComboStep.Light, ComboStep.Heavy),
                new ComboChain("Double Heavy", 1.4f, ComboStep.Heavy, ComboStep.Heavy),
                new ComboChain("Dodge Strike", 1.2f, ComboStep.Dodge, ComboStep.Light),
                new ComboChain("Parry Riposte", 2f, ComboStep.Parry, ComboStep.Heavy),
                new ComboChain("Ability Strike", 1.3f, ComboStep.Ability, ComboStep.Light)
            };
        }
    }

    /// <summary>
    /// Records the player's recent combat inputs and matches them against a chain
    /// table. Replaces the TASK 002 swing counter, which could not tell a light from
    /// a heavy and so could not express any of the section 14 chains.
    ///
    /// Plain C# with time passed in, so the matching rules are testable in EditMode
    /// without a frame loop.
    /// </summary>
    public sealed class ComboTracker
    {
        private readonly List<ComboChain> chains;
        private readonly List<ComboStep> history = new();
        private readonly int historySize;
        private readonly float window;
        private float expiresAt = float.NegativeInfinity;

        /// <summary>The steps still inside the combo window, oldest first.</summary>
        public IReadOnlyList<ComboStep> History => history;

        /// <summary>The chain the most recent step completed, or null.</summary>
        public ComboChain Current { get; private set; }

        /// <param name="window">Seconds a step keeps the chain alive after it is recorded.</param>
        /// <param name="historySize">How many steps are remembered; at least the longest chain.</param>
        public ComboTracker(IEnumerable<ComboChain> chainTable, float window, int historySize = 4)
        {
            chains = new List<ComboChain>(chainTable ?? Array.Empty<ComboChain>());
            this.window = Mathf.Max(0f, window);
            this.historySize = Mathf.Max(1, historySize);
        }

        /// <summary>
        /// Adds a step. If the previous step's window has lapsed the history is
        /// cleared first, so a late input starts a new chain rather than finishing
        /// a stale one. <paramref name="linger"/> extends the window beyond the
        /// default — a swing passes its own duration so the window is measured from
        /// the end of the swing, not the button press.
        /// </summary>
        public ComboChain Record(ComboStep step, float now, float linger = 0f)
        {
            if (now > expiresAt)
            {
                history.Clear();
            }

            history.Add(step);
            while (history.Count > historySize)
            {
                history.RemoveAt(0);
            }

            expiresAt = now + window + Mathf.Max(0f, linger);
            Current = Match();
            return Current;
        }

        /// <summary>True when the window has lapsed and the next step starts fresh.</summary>
        public bool HasExpired(float now)
        {
            return now > expiresAt;
        }

        public void Reset()
        {
            history.Clear();
            Current = null;
            expiresAt = float.NegativeInfinity;
        }

        /// <summary>
        /// Returns the longest chain whose steps are the tail of the history.
        /// Longest wins so Light→Light→Heavy beats Light→Heavy when both fit.
        /// </summary>
        private ComboChain Match()
        {
            ComboChain best = null;
            foreach (var chain in chains)
            {
                if (chain?.Steps == null || chain.Steps.Length == 0 || chain.Steps.Length > history.Count)
                {
                    continue;
                }

                if (best != null && chain.Steps.Length <= best.Steps.Length)
                {
                    continue;
                }

                if (EndsWith(chain.Steps))
                {
                    best = chain;
                }
            }

            return best;
        }

        private bool EndsWith(ComboStep[] steps)
        {
            var offset = history.Count - steps.Length;
            for (var i = 0; i < steps.Length; i++)
            {
                if (history[offset + i] != steps[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
