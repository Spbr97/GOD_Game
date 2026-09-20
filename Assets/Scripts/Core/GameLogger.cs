using UnityEngine;

namespace Game.Core
{
    public enum LogCategory
    {
        Game,
        Player,
        Combat,
        AI,
        Quest,
        Dialogue,
        Memory,
        Save,
        UI,
        Audio,
        Performance,
        Error
    }

    /// <summary>
    /// Categorized logging per SPEC.md section 51. Every error log should answer:
    /// what failed, where, why, and what fallback occurred.
    /// </summary>
    public static class GameLogger
    {
        public static void Log(LogCategory category, string message, Object context = null)
        {
            Debug.Log($"[{category.ToString().ToUpperInvariant()}] {message}", context);
        }

        public static void LogWarning(LogCategory category, string message, Object context = null)
        {
            Debug.LogWarning($"[{category.ToString().ToUpperInvariant()}] {message}", context);
        }

        public static void LogError(LogCategory category, string message, Object context = null)
        {
            Debug.LogError($"[{category.ToString().ToUpperInvariant()}] {message}", context);
        }

        public static void LogFallback(LogCategory category, string whatFailed, string where, string why, string fallback, Object context = null)
        {
            LogWarning(category, $"FAILED: {whatFailed} | WHERE: {where} | WHY: {why} | FALLBACK: {fallback}", context);
        }
    }
}
