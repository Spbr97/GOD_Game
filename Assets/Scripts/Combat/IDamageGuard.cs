namespace Game.Combat
{
    /// <summary>
    /// A defence that gets the first look at incoming damage (SPEC.md section 15).
    /// <see cref="HealthComponent.TakeDamage"/> calls <see cref="TryGuard"/> after
    /// deduplication and before health changes. Returning true means the hit was
    /// fully absorbed — parried or blocked — and no damage is applied. Returning
    /// false lets the hit through, with whatever <paramref name="damage"/> now holds,
    /// so a guard can also reduce rather than cancel.
    /// </summary>
    public interface IDamageGuard
    {
        bool TryGuard(ref DamageData damage);
    }
}
