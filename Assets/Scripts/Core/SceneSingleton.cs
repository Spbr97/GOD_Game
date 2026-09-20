using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Backs the <c>Instance</c> property of the scene-level managers.
    ///
    /// Setting <c>Instance</c> in <c>Awake</c> alone is not enough: Unity does not
    /// guarantee the order Awake runs in, so a component that looks a manager up in
    /// its own Awake can find null even though the manager is sitting in the same
    /// scene. Unity also does not call Awake at all in EditMode, which makes such
    /// managers untestable. Resolving on first access fixes both — the same lesson as
    /// <c>Hurtbox</c> in TASK 002.
    ///
    /// The lookup only runs while the cached reference is null, so a correctly
    /// configured scene pays for it once.
    /// </summary>
    public static class SceneSingleton
    {
        public static T Resolve<T>(ref T cached) where T : Object
        {
            if (cached != null)
            {
                return cached;
            }

            cached = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            return cached;
        }
    }
}
