using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Reacts to combat's <see cref="EventBus"/> payloads with placeholder particle
    /// bursts (SPEC.md section 36, TASK 018) — sparks on a parry, dust on a broken
    /// guard, divine energy on Ember Step. A pure listener, the same one-way shape
    /// <see cref="Game.UI.HudUI"/> already uses: Combat never references VFX.
    /// </summary>
    public class CombatVfx : MonoBehaviour
    {
        private void OnEnable()
        {
            EventBus.Subscribe<ParryEvent>(OnParry);
            EventBus.Subscribe<GuardBrokenEvent>(OnGuardBroken);
            EventBus.Subscribe<EmberStepUsedEvent>(OnEmberStepUsed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ParryEvent>(OnParry);
            EventBus.Unsubscribe<GuardBrokenEvent>(OnGuardBroken);
            EventBus.Unsubscribe<EmberStepUsedEvent>(OnEmberStepUsed);
        }

        private void OnParry(ParryEvent e)
        {
            if (e.Attacker != null)
            {
                VfxSpawner.Spawn(VfxKind.Spark, e.Attacker.transform.position);
            }
        }

        private void OnGuardBroken(GuardBrokenEvent e)
        {
            if (e.Defender != null)
            {
                VfxSpawner.Spawn(VfxKind.Dust, e.Defender.transform.position);
            }
        }

        private void OnEmberStepUsed(EmberStepUsedEvent e)
        {
            if (e.Player != null)
            {
                VfxSpawner.Spawn(VfxKind.DivineEnergy, e.Player.transform.position);
            }
        }
    }
}
