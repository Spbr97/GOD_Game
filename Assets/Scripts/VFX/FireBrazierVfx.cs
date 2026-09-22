using Game.World;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// A placeholder fire particle burst (SPEC.md section 36) alongside
    /// <see cref="FireBrazier"/>'s existing lit/unlit tint, fired each time it lights.
    /// A companion component rather than a change to <see cref="FireBrazier"/> itself,
    /// reacting to its public <see cref="FireBrazier.Changed"/> event the same way
    /// <see cref="Game.World.PuzzleController"/> does, so the puzzle logic stays
    /// untouched by a purely cosmetic addition.
    /// </summary>
    [RequireComponent(typeof(FireBrazier))]
    public class FireBrazierVfx : MonoBehaviour
    {
        private FireBrazier brazier;

        private void Awake()
        {
            brazier = GetComponent<FireBrazier>();
        }

        private void OnEnable()
        {
            brazier.Changed += OnBrazierChanged;
        }

        private void OnDisable()
        {
            brazier.Changed -= OnBrazierChanged;
        }

        private void OnBrazierChanged()
        {
            if (brazier.IsLit)
            {
                VfxSpawner.Spawn(VfxKind.Fire, transform.position);
            }
        }
    }
}
