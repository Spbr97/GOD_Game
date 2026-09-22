using Game.Combat;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// A placeholder animation layer for the player (SPEC.md section 39, TASK 018,
    /// section 78's placeholder-first rule): procedural transform tweens on a
    /// <see cref="visual"/> child, blended with <see cref="Quaternion.Slerp"/> so
    /// states change smoothly rather than snapping (section 39's "use animation
    /// blending to prevent abrupt transitions") — the same placeholder-visual-cue
    /// idea <see cref="Game.AI.EnemyController.ApplyPhaseTint"/> and
    /// <see cref="Game.AI.EnemyCombatant"/>'s telegraph pulse already use for enemies,
    /// applied here to the player's locomotion and combat states instead of a real
    /// rig's clips.
    ///
    /// Deliberately does not touch attack timing. <see cref="WeaponController"/>'s
    /// windup/active/recovery durations are read from <see cref="Game.AI.EnemyArchetype"/>-style
    /// data and rescaled by <see cref="Game.Core.Difficulty"/> at swing time, not
    /// authored into a clip; this component only reads that timing (<c>IsSwinging</c>)
    /// to drive a cosmetic tilt; see KNOWN_ISSUES.md for why moving timing authority
    /// the other way — into real AnimationEvents — stays deferred until a rig exists
    /// to justify it.
    /// </summary>
    public class PlaceholderAnimator : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private float blendSpeed = 8f;
        [SerializeField] private float sprintLeanDegrees = 12f;
        [SerializeField] private float airborneLeanDegrees = 6f;
        [SerializeField] private float attackTiltDegrees = 20f;
        [SerializeField] private float dodgeSpinDegreesPerSecond = 900f;
        [SerializeField] private float guardCrouchScale = 0.85f;
        [SerializeField] private float hitFlinchDegrees = 15f;
        [SerializeField] private float hitFlinchSeconds = 0.15f;
        [SerializeField] private float deathCollapseDegrees = 80f;

        private PlayerController playerController;
        private CombatController combatController;
        private WeaponController weapon;

        private Quaternion baseRotation;
        private Vector3 baseScale;
        private float flinchTimer;
        private bool dead;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            combatController = GetComponent<CombatController>();
            weapon = GetComponentInChildren<WeaponController>(true);

            if (visual == null)
            {
                visual = transform;
            }

            baseRotation = visual.localRotation;
            baseScale = visual.localScale;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DamageAppliedEvent>(OnDamageApplied);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Subscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Unsubscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
        }

        private void OnDamageApplied(DamageAppliedEvent e)
        {
            if (e.Victim == gameObject && e.RemainingHealth > 0f)
            {
                flinchTimer = hitFlinchSeconds;
            }
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            if (e.Player == gameObject)
            {
                dead = true;
            }
        }

        private void OnPlayerRespawned(PlayerRespawnedEvent e)
        {
            if (e.Player == gameObject)
            {
                dead = false;
                flinchTimer = 0f;
                visual.localRotation = baseRotation;
                visual.localScale = baseScale;
            }
        }

        /// <summary>Test and tooling seam for wiring onto a component built without the Inspector.</summary>
        public void Configure(Transform visualTransform)
        {
            visual = visualTransform != null ? visualTransform : transform;
            baseRotation = visual.localRotation;
            baseScale = visual.localScale;
        }

        private void Update()
        {
            if (dead)
            {
                var collapsed = baseRotation * Quaternion.Euler(deathCollapseDegrees, 0f, 0f);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, collapsed, blendSpeed * Time.deltaTime);
                return;
            }

            if (flinchTimer > 0f)
            {
                flinchTimer -= Time.deltaTime;
            }

            if (combatController != null && combatController.IsDodging)
            {
                var spin = (Time.time * dodgeSpinDegreesPerSecond) % 360f;
                visual.localRotation = baseRotation * Quaternion.Euler(0f, spin, 0f);
                visual.localScale = Vector3.Lerp(visual.localScale, baseScale, blendSpeed * Time.deltaTime);
                return;
            }

            var targetEuler = Vector3.zero;
            var targetScale = baseScale;

            if (weapon != null && weapon.IsSwinging)
            {
                targetEuler.x = attackTiltDegrees;
            }
            else if (combatController != null && combatController.IsGuarding)
            {
                targetScale = new Vector3(baseScale.x, baseScale.y * guardCrouchScale, baseScale.z);
            }
            else if (playerController != null && playerController.IsSprinting)
            {
                targetEuler.x = -sprintLeanDegrees;
            }
            else if (playerController != null && !playerController.IsGrounded)
            {
                targetEuler.x = airborneLeanDegrees;
            }

            if (flinchTimer > 0f)
            {
                targetEuler.z += hitFlinchDegrees;
            }

            var targetRotation = baseRotation * Quaternion.Euler(targetEuler);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, targetRotation, blendSpeed * Time.deltaTime);
            visual.localScale = Vector3.Lerp(visual.localScale, targetScale, blendSpeed * Time.deltaTime);
        }
    }
}
