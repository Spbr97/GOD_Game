using Game.Core;

namespace Game.DevTools
{
    /// <summary>
    /// The gate in front of every developer tool (SPEC.md section 52: "Debug mode
    /// must be disabled in release builds").
    ///
    /// Two separate questions, deliberately:
    ///
    /// <see cref="IsAvailableInThisBuild"/> is compiled in or out. In a release player
    /// it is a constant false, so the branches behind it are dead code the IL2CPP
    /// stripper removes — the tools are not merely hidden from the player, they are
    /// not in the binary to be found. That is what section 52 asks for, and a runtime
    /// flag alone would not deliver it.
    ///
    /// <see cref="IsEnabled"/> is the developer's own switch inside a build where the
    /// tools do exist. It starts off, so a stray key in a playtest cannot teleport
    /// anyone, and nothing in the game reads it except the console and the overlay.
    ///
    /// The namespace is <c>Game.DevTools</c> rather than <c>Game.Debug</c>: a
    /// namespace called Debug would shadow <c>UnityEngine.Debug</c> for every file in
    /// it, and the first <c>Debug.Log</c> written here would not compile.
    /// </summary>
    public static class DebugMode
    {
        /// <summary>
        /// Whether developer tools exist in this build at all. A compile-time
        /// constant, not a setting.
        /// </summary>
        public static bool IsAvailableInThisBuild =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        /// <summary>Whether the tools are currently switched on. Always false where they do not exist.</summary>
        public static bool IsEnabled { get; private set; }

        /// <summary>
        /// Turns the tools on. Returns false in a release build, where there is
        /// nothing to turn on — callers should report that rather than pretend.
        /// </summary>
        public static bool Enable()
        {
            if (!IsAvailableInThisBuild)
            {
                return false;
            }

            if (!IsEnabled)
            {
                IsEnabled = true;
                GameLogger.LogWarning(LogCategory.Game, "Debug mode enabled. This is not available in a release build.");
            }

            return true;
        }

        public static void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            IsEnabled = false;
            GameLogger.Log(LogCategory.Game, "Debug mode disabled.");
        }

        public static bool Toggle()
        {
            if (IsEnabled)
            {
                Disable();
                return false;
            }

            return Enable();
        }

        /// <summary>
        /// Resets the switch between PlayMode tests. A static that survives a domain
        /// reload would otherwise leak one test's debug state into the next.
        /// </summary>
        public static void ResetForTests() => IsEnabled = false;
    }
}
