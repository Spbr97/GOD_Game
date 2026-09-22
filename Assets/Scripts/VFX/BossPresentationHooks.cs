using Game.Audio;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Fills <see cref="Game.AI.BossController"/>'s <c>onPhase3Entered</c>/
    /// <c>onDefeated</c> UnityEvent hooks with placeholder VFX/audio (SPEC.md
    /// section 36's "boss transformations", TASK 018) — exactly the wiring
    /// ARCHITECTURE.md's <c>BossController</c> entry says TASK 018 would supply,
    /// so the boss framework itself never grows a direct reference to either.
    /// </summary>
    public class BossPresentationHooks : MonoBehaviour
    {
        public void PlayTransformation()
        {
            VfxSpawner.Spawn(VfxKind.BossTransformation, transform.position);
            SfxSpawner.Play(SfxKind.BossPhaseTransition, transform.position);
        }

        public void PlayDefeat()
        {
            VfxSpawner.Spawn(VfxKind.BossTransformation, transform.position);
        }
    }
}
