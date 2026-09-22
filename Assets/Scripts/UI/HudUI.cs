using Game.AI;
using Game.Combat;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The always-on HUD (SPEC.md section 42): health, stamina, divine energy, the
    /// lock-on target, the last combo performed, and a boss health bar (SPEC.md
    /// section 18) that appears only while a <see cref="BossController"/> encounter is
    /// live. The objective is <see cref="QuestTrackerUI"/>'s and the equipped ability
    /// still waits for the system behind it.
    ///
    /// Bars are polled rather than event-driven because stamina regenerates every
    /// frame and a bar that only moved on spend would look broken. Everything else
    /// arrives through <see cref="EventBus"/>.
    ///
    /// Every bar carries a text label, so no state is communicated by colour alone
    /// (SPEC.md section 43). <see cref="SetVisible"/> is the "HUD must be hideable"
    /// requirement; nothing binds it to an input yet.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        [SerializeField] private GameObject hudRoot;

        [Header("Bars")]
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private Text healthLabel;
        [SerializeField] private RectTransform staminaFill;
        [SerializeField] private Text staminaLabel;
        [SerializeField] private RectTransform divineFill;
        [SerializeField] private Text divineLabel;

        [Header("Combat readouts")]
        [SerializeField] private Text lockOnLabel;
        [SerializeField] private Text comboLabel;

        [Tooltip("How long a combo name stays on screen.")]
        [SerializeField] private float comboLingerSeconds = 1.2f;

        [Header("Boss (SPEC.md section 18)")]
        [Tooltip("Parent of the boss bar, hidden until a boss encounter starts.")]
        [SerializeField] private GameObject bossHealthRoot;

        [SerializeField] private RectTransform bossHealthFill;
        [SerializeField] private Text bossNameLabel;

        private HealthComponent health;
        private StaminaComponent stamina;
        private DivineEnergyComponent divine;
        private HealthComponent bossHealth;
        private float comboHideAt;
        private bool visible = true;

        public bool IsVisible => visible;

        private void OnEnable()
        {
            EventBus.Subscribe<LockOnChangedEvent>(OnLockOnChanged);
            EventBus.Subscribe<ComboPerformedEvent>(OnComboPerformed);
            EventBus.Subscribe<ParryEvent>(OnParry);
            EventBus.Subscribe<GuardBrokenEvent>(OnGuardBroken);
            EventBus.Subscribe<PlayerRespawnedEvent>(OnRespawned);
            EventBus.Subscribe<BossEncounterStartedEvent>(OnBossEncounterStarted);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);

            SetText(lockOnLabel, string.Empty);
            SetText(comboLabel, string.Empty);
            SetBossVisible(false);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LockOnChangedEvent>(OnLockOnChanged);
            EventBus.Unsubscribe<ComboPerformedEvent>(OnComboPerformed);
            EventBus.Unsubscribe<ParryEvent>(OnParry);
            EventBus.Unsubscribe<GuardBrokenEvent>(OnGuardBroken);
            EventBus.Unsubscribe<PlayerRespawnedEvent>(OnRespawned);
            EventBus.Unsubscribe<BossEncounterStartedEvent>(OnBossEncounterStarted);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void Update()
        {
            if (!visible)
            {
                return;
            }

            if (health == null)
            {
                ResolvePlayer();
            }

            if (health != null)
            {
                SetBar(healthFill, health.HealthFraction);
                SetText(healthLabel, $"Health  {health.CurrentHealth:0}/{health.MaxHealth:0}");
            }

            if (stamina != null)
            {
                SetBar(staminaFill, stamina.StaminaFraction);
                SetText(staminaLabel, $"Stamina  {stamina.CurrentStamina:0}/{stamina.MaxStamina:0}");
            }

            if (divine != null)
            {
                SetBar(divineFill, divine.EnergyFraction);
                SetText(divineLabel, $"Divine  {divine.CurrentEnergy:0}/{divine.MaxEnergy:0}");
            }

            if (comboHideAt > 0f && Time.unscaledTime >= comboHideAt)
            {
                comboHideAt = 0f;
                SetText(comboLabel, string.Empty);
            }

            if (bossHealth != null)
            {
                SetBar(bossHealthFill, bossHealth.HealthFraction);
            }
        }

        /// <summary>Hides or shows the whole HUD (SPEC.md section 42: HUD must be hideable).</summary>
        public void SetVisible(bool show)
        {
            visible = show;
            if (hudRoot != null)
            {
                hudRoot.SetActive(show);
            }
        }

        private void ResolvePlayer()
        {
            // The same lookup SaveManager uses: PlayerDeath marks the player, so the
            // HUD depends on nothing that would need a tag or a name.
            var player = FindAnyObjectByType<PlayerDeath>();
            if (player == null)
            {
                return;
            }

            health = player.GetComponent<HealthComponent>();
            stamina = player.GetComponent<StaminaComponent>();
            divine = player.GetComponent<DivineEnergyComponent>();
        }

        private void OnLockOnChanged(LockOnChangedEvent changed)
        {
            SetText(lockOnLabel, changed.Target != null ? $"Locked: {changed.Target.name}" : string.Empty);
        }

        private void OnComboPerformed(ComboPerformedEvent combo)
        {
            Flash($"{combo.ChainName}  x{combo.DamageMultiplier:0.##}");
        }

        private void OnParry(ParryEvent parry)
        {
            Flash(parry.Perfect ? "Perfect parry" : "Parry");
        }

        private void OnGuardBroken(GuardBrokenEvent broken)
        {
            Flash("Guard broken");
        }

        private void OnRespawned(PlayerRespawnedEvent respawned)
        {
            SetText(lockOnLabel, string.Empty);
            SetText(comboLabel, string.Empty);
        }

        private void OnBossEncounterStarted(BossEncounterStartedEvent started)
        {
            bossHealth = started.Boss != null ? started.Boss.GetComponent<HealthComponent>() : null;
            SetText(bossNameLabel, started.DisplayName);
            SetBossVisible(bossHealth != null);
        }

        private void OnBossDefeated(BossDefeatedEvent defeated)
        {
            if (bossHealth != null && defeated.Boss != null && bossHealth.gameObject != defeated.Boss)
            {
                return;
            }

            bossHealth = null;
            SetBossVisible(false);
        }

        private void SetBossVisible(bool show)
        {
            if (bossHealthRoot != null)
            {
                bossHealthRoot.SetActive(show);
            }
        }

        private void Flash(string message)
        {
            SetText(comboLabel, message);
            comboHideAt = Time.unscaledTime + comboLingerSeconds;
        }

        private static void SetBar(RectTransform fill, float fraction)
        {
            if (fill == null)
            {
                return;
            }

            var max = fill.anchorMax;
            max.x = Mathf.Clamp01(fraction);
            fill.anchorMax = max;
        }

        private static void SetText(Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
