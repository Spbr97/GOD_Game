using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Reacts to combat's <see cref="EventBus"/> payloads with placeholder tones
    /// (SPEC.md section 40, TASK 018) — weapon impact, parry, block and guard break.
    /// A pure listener, the same one-way shape <c>Game.VFX.CombatVfx</c> uses:
    /// Combat never references Audio.
    /// </summary>
    public class CombatAudio : MonoBehaviour
    {
        private void OnEnable()
        {
            EventBus.Subscribe<DamageAppliedEvent>(OnDamageApplied);
            EventBus.Subscribe<ParryEvent>(OnParry);
            EventBus.Subscribe<AttackBlockedEvent>(OnAttackBlocked);
            EventBus.Subscribe<GuardBrokenEvent>(OnGuardBroken);
            EventBus.Subscribe<EmberStepUsedEvent>(OnEmberStepUsed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
            EventBus.Unsubscribe<ParryEvent>(OnParry);
            EventBus.Unsubscribe<AttackBlockedEvent>(OnAttackBlocked);
            EventBus.Unsubscribe<GuardBrokenEvent>(OnGuardBroken);
            EventBus.Unsubscribe<EmberStepUsedEvent>(OnEmberStepUsed);
        }

        private void OnDamageApplied(DamageAppliedEvent e)
        {
            if (e.Victim != null)
            {
                SfxSpawner.Play(SfxKind.WeaponImpact, e.Victim.transform.position);
            }
        }

        private void OnParry(ParryEvent e)
        {
            if (e.Defender != null)
            {
                SfxSpawner.Play(SfxKind.Parry, e.Defender.transform.position);
            }
        }

        private void OnAttackBlocked(AttackBlockedEvent e)
        {
            if (e.Defender != null)
            {
                SfxSpawner.Play(SfxKind.Block, e.Defender.transform.position);
            }
        }

        private void OnGuardBroken(GuardBrokenEvent e)
        {
            if (e.Defender != null)
            {
                SfxSpawner.Play(SfxKind.GuardBreak, e.Defender.transform.position);
            }
        }

        private void OnEmberStepUsed(EmberStepUsedEvent e)
        {
            if (e.Player != null)
            {
                SfxSpawner.Play(SfxKind.DivineAbility, e.Player.transform.position);
            }
        }
    }
}
