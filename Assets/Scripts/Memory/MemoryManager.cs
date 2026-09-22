using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Dialogue;
using Game.Quests;
using UnityEngine;

namespace Game.Memory
{
    /// <summary>
    /// Holds which memories the player has and what state each is in (SPEC.md
    /// sections 19-21, TASK 003).
    ///
    /// The safety rules from SPEC.md section 20 are enforced here rather than left to
    /// callers: a memory marked Critical cannot be corrupted or forgotten, so no
    /// amount of ordinary gameplay can make story progression unreachable.
    ///
    /// Also reacts to <see cref="Game.Combat.EmberStepUsedEvent"/> (SPEC.md section 8.1,
    /// TASK 011) — a deliberate exception to Memory otherwise never referencing Combat,
    /// justified the same way <see cref="Game.AI.EnemyStagger"/> reacting to
    /// <see cref="Game.Combat.ParryEvent"/> already is: reading another layer's
    /// published payload, not a direct reference, and the exact narrative mechanic
    /// section 20 describes ("some divine powers consume memory"). See
    /// ARCHITECTURE.md's dependency list.
    /// </summary>
    public class MemoryManager : MonoBehaviour
    {
        private static MemoryManager instance;

        /// <summary>Resolved on first access; see <see cref="SceneSingleton"/>.</summary>
        public static MemoryManager Instance => SceneSingleton.Resolve(ref instance);

        [Tooltip("Every memory that exists, so states can be resolved by id from dialogue and save data.")]
        [SerializeField] private MemoryFragment[] catalogue;

        [Header("Ember Step's cost (SPEC.md section 20)")]
        [Tooltip("Overall integrity spent every time Ember Step is used.")]
        [SerializeField] private float emberStepIntegrityCost = 0.02f;

        [Tooltip("Every this many uses, one Optional memory the player knows is temporarily forgotten. Zero disables it.")]
        [SerializeField] private int emberStepUsesPerForget = 3;

        [Tooltip("Seconds before a memory Ember Step forgot comes back on its own.")]
        [SerializeField] private float temporaryForgetSeconds = 20f;

        private readonly Dictionary<string, MemoryState> states = new();
        private readonly Dictionary<string, MemoryFragment> byId = new();
        private readonly List<MemoryFragment> discovered = new();

        /// <summary>Overall memory integrity, 0..1 (SPEC.md section 20).</summary>
        public float Integrity { get; private set; } = 1f;

        public IReadOnlyList<MemoryFragment> Discovered => discovered;
        public int DiscoveredCount => discovered.Count;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                GameLogger.LogWarning(LogCategory.Memory, "A second MemoryManager was destroyed.", this);
                Destroy(this);
                return;
            }

            instance = this;
            BuildCatalogue();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueConsequenceEvent>(OnDialogueConsequence);
            EventBus.Subscribe<Game.Combat.EmberStepUsedEvent>(OnEmberStepUsed);

            // Dialogue asks "what state is memory X in?" through this hook so it does
            // not have to reference the Memory system (SPEC.md section 47).
            DialogueGraph.MemoryStateResolver = ResolveStateName;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueConsequenceEvent>(OnDialogueConsequence);
            EventBus.Unsubscribe<Game.Combat.EmberStepUsedEvent>(OnEmberStepUsed);

            if (DialogueGraph.MemoryStateResolver == ResolveStateName)
            {
                DialogueGraph.MemoryStateResolver = null;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public MemoryState GetState(string memoryId)
        {
            return !string.IsNullOrEmpty(memoryId) && states.TryGetValue(memoryId, out var state)
                ? state
                : MemoryState.Unknown;
        }

        public bool IsDiscovered(string memoryId)
        {
            return GetState(memoryId) != MemoryState.Unknown;
        }

        /// <summary>Every memory the player has a state for, for the save system to snapshot.</summary>
        public IReadOnlyDictionary<string, MemoryState> States => states;

        /// <summary>
        /// Puts one memory back where a save left it, without the discovery events,
        /// flags or quest reports that <see cref="Discover"/> fires. Restoring is not a
        /// discovery; the player already had this memory.
        ///
        /// The protection in <see cref="SetState"/> is deliberately not applied here.
        /// A save can only contain a state the rules already allowed, and refusing to
        /// restore one would silently change the player's save rather than protect it.
        /// </summary>
        public bool RestoreState(string memoryId, MemoryState state)
        {
            var memory = Find(memoryId);
            if (memory == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Memory,
                    $"could not restore memory '{memoryId}'",
                    "MemoryManager.RestoreState",
                    "no memory with that id is listed in the memory catalogue",
                    "the memory is dropped from the loaded save rather than failing the load",
                    this);
                return false;
            }

            states[memoryId] = state;

            if (state != MemoryState.Unknown && !discovered.Contains(memory))
            {
                discovered.Add(memory);
            }

            return true;
        }

        /// <summary>Sets overall integrity outright, for restoring a save.</summary>
        public void RestoreIntegrity01(float value)
        {
            Integrity = Mathf.Clamp01(value);
        }

        /// <summary>Forgets everything the player has found. For loading a save and for a new game.</summary>
        public void ClearAll()
        {
            states.Clear();
            discovered.Clear();
            Integrity = 1f;
        }

        public MemoryFragment Find(string memoryId)
        {
            return !string.IsNullOrEmpty(memoryId) && byId.TryGetValue(memoryId, out var memory) ? memory : null;
        }

        /// <summary>Discovers a memory by id, looked up in the catalogue.</summary>
        public bool Discover(string memoryId)
        {
            var memory = Find(memoryId);
            if (memory == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Memory,
                    $"could not discover memory '{memoryId}'",
                    "MemoryManager.Discover",
                    "no memory with that id is listed in the memory catalogue",
                    "nothing is granted; anything gated on this memory stays unavailable",
                    this);
                return false;
            }

            return Discover(memory);
        }

        /// <summary>
        /// Records a memory as found. Returns false if it was already discovered, so a
        /// pickup that fires twice grants one memory and reports one objective.
        /// </summary>
        public bool Discover(MemoryFragment memory)
        {
            if (memory == null)
            {
                return false;
            }

            var id = memory.MemoryId;
            if (IsDiscovered(id))
            {
                return false;
            }

            Register(memory);
            SetState(memory, memory.StateOnDiscovery);
            discovered.Add(memory);

            GameLogger.Log(LogCategory.Memory, $"Memory discovered: {memory.Title} ({id}).", this);

            if (!string.IsNullOrEmpty(memory.DiscoveryFlag))
            {
                WorldState.Instance?.SetFlag(memory.DiscoveryFlag);
            }

            if (!string.IsNullOrEmpty(memory.ObjectiveIdOnDiscovery))
            {
                QuestManager.Instance?.ReportObjective(memory.ObjectiveIdOnDiscovery);
            }

            EventBus.Publish(new MemoryDiscoveredEvent(memory));
            return true;
        }

        /// <summary>
        /// Moves a memory to a new state. Refuses to degrade a Critical memory
        /// (SPEC.md section 20: critical quest data must remain protected), logging the
        /// refusal rather than failing silently.
        /// </summary>
        public bool SetState(MemoryFragment memory, MemoryState newState)
        {
            if (memory == null)
            {
                return false;
            }

            var id = memory.MemoryId;
            var previous = GetState(id);
            if (previous == newState)
            {
                return false;
            }

            if (memory.IsProtected && IsDegraded(newState))
            {
                GameLogger.LogWarning(
                    LogCategory.Memory,
                    $"Refused to set critical memory '{id}' to {newState}; critical memories are protected.",
                    this);
                return false;
            }

            Register(memory);
            states[id] = newState;
            EventBus.Publish(new MemoryStateChangedEvent(memory, previous, newState));
            return true;
        }

        /// <summary>
        /// Corrupts one memory and spends a slice of overall integrity as its cost —
        /// SPEC.md section 20's "some divine powers consume memory", surfaced now as a
        /// player-triggerable action (the Memory Archive screen) ahead of the ability
        /// that will trigger it for real (ROADMAP TASK 011). Refused for a Critical
        /// memory by <see cref="SetState"/>'s own protection, in which case no integrity
        /// is spent either — a refused corruption must not still cost something.
        /// </summary>
        public bool Corrupt(MemoryFragment memory, float integrityCost = 0.1f)
        {
            if (!SetState(memory, MemoryState.Corrupted))
            {
                return false;
            }

            // Memory branch's "memory manipulation" skill (SPEC.md section 30, TASK
            // 016): a bonus fraction, clamped so an overzealous skill value could
            // reduce the cost but never turn it negative and refund integrity.
            var skillDiscount = 1f + (Game.Progression.SkillTreeManager.Instance?.GetBonus(
                Game.Progression.SkillEffectType.MemoryCorruptionCostMultiplier) ?? 0f);
            ReduceIntegrity(Mathf.Max(0f, integrityCost * skillDiscount));
            return true;
        }

        /// <summary>
        /// Reduces overall memory integrity, for divine powers that consume memory
        /// (SPEC.md section 20). Does not itself corrupt any memory — what integrity
        /// affects is cosmetic and is decided by the systems that read it.
        /// </summary>
        public void ReduceIntegrity(float amount)
        {
            if (amount > 0f)
            {
                SetIntegrity(Integrity - amount);
            }
        }

        public void RestoreIntegrity(float amount)
        {
            if (amount > 0f)
            {
                SetIntegrity(Integrity + amount);
            }
        }

        private void SetIntegrity(float value)
        {
            var next = Mathf.Clamp01(value);
            if (Mathf.Approximately(next, Integrity))
            {
                return;
            }

            Integrity = next;
            GameLogger.Log(LogCategory.Memory, $"Memory integrity is now {Integrity:0.00}.", this);
            EventBus.Publish(new MemoryIntegrityChangedEvent(Integrity));
        }

        /// <summary>The state name dialogue compares against, or "Unknown".</summary>
        private string ResolveStateName(string memoryId)
        {
            return GetState(memoryId).ToString();
        }

        private static bool IsDegraded(MemoryState state)
        {
            return state == MemoryState.Forgotten
                   || state == MemoryState.Corrupted
                   || state == MemoryState.FalseMemory;
        }

        private void OnDialogueConsequence(DialogueConsequenceEvent consequence)
        {
            if (consequence.Type == ConsequenceType.DiscoverMemory)
            {
                Discover(consequence.Target);
            }
        }

        /// <summary>
        /// Ember Step's cost: a small constant drain on overall integrity every use,
        /// and every <see cref="emberStepUsesPerForget"/>th use, one Optional memory
        /// the player currently knows is temporarily forgotten (SPEC.md section 20:
        /// "cosmetic memories can disappear"). Never touches a Supporting or Critical
        /// memory — those are not "minor".
        /// </summary>
        private void OnEmberStepUsed(Game.Combat.EmberStepUsedEvent used)
        {
            ReduceIntegrity(emberStepIntegrityCost);

            if (emberStepUsesPerForget <= 0 || used.TotalUses % emberStepUsesPerForget != 0)
            {
                return;
            }

            var memory = FindRandomKnownOptionalMemory();
            if (memory == null)
            {
                return;
            }

            var previousState = GetState(memory.MemoryId);
            if (!SetState(memory, MemoryState.Forgotten))
            {
                return;
            }

            GameLogger.Log(LogCategory.Memory,
                $"Ember Step's repeated use temporarily forgot '{memory.Title}'.", this);
            StartCoroutine(RestoreAfterDelay(memory, previousState, temporaryForgetSeconds));
        }

        private MemoryFragment FindRandomKnownOptionalMemory()
        {
            var candidates = new List<MemoryFragment>();
            for (var i = 0; i < discovered.Count; i++)
            {
                var memory = discovered[i];
                if (memory.Importance == MemoryImportance.Optional && GetState(memory.MemoryId) == MemoryState.Known)
                {
                    candidates.Add(memory);
                }
            }

            return candidates.Count == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// Restores a memory Ember Step temporarily forgot, but only if it is still
        /// exactly where that left it — if something else changed it in the meantime
        /// (the player corrupted it deliberately, say), that change owns the memory's
        /// state now, and this timer has nothing to undo.
        /// </summary>
        private IEnumerator RestoreAfterDelay(MemoryFragment memory, MemoryState restoreTo, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);

            if (GetState(memory.MemoryId) == MemoryState.Forgotten)
            {
                SetState(memory, restoreTo);
                GameLogger.Log(LogCategory.Memory, $"'{memory.Title}' is remembered again.", this);
            }
        }

        private void BuildCatalogue()
        {
            byId.Clear();
            if (catalogue == null)
            {
                return;
            }

            for (var i = 0; i < catalogue.Length; i++)
            {
                Register(catalogue[i]);
            }
        }

        private void Register(MemoryFragment memory)
        {
            if (memory == null)
            {
                return;
            }

            byId[memory.MemoryId] = memory;
        }

        /// <summary>Test and tooling seam for supplying the catalogue without the Inspector.</summary>
        public void Configure(MemoryFragment[] memories)
        {
            catalogue = memories;
            BuildCatalogue();
        }

        /// <summary>Test and tuning seam for Ember Step's memory cost.</summary>
        public void ConfigureEmberStepCost(float integrityCostPerUse, int usesPerForget, float forgetSeconds)
        {
            emberStepIntegrityCost = integrityCostPerUse;
            emberStepUsesPerForget = usesPerForget;
            temporaryForgetSeconds = forgetSeconds;
        }
    }
}
