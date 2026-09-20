using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Generic publish/subscribe event bus (SPEC.md section 49) so systems can
    /// react to each other without holding direct references. Event payloads are
    /// plain structs/classes defined by each system (e.g. a future
    /// OnMemoryDiscovered payload in the Memory system).
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            handlers.TryGetValue(type, out var existing);
            handlers[type] = existing == null ? handler : Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!handlers.TryGetValue(type, out var existing))
            {
                return;
            }

            var combined = Delegate.Remove(existing, handler);
            if (combined == null)
            {
                handlers.Remove(type);
            }
            else
            {
                handlers[type] = combined;
            }
        }

        public static void Publish<T>(T payload)
        {
            if (handlers.TryGetValue(typeof(T), out var existing) && existing is Action<T> action)
            {
                action.Invoke(payload);
            }
        }

        /// <summary>Clears all subscriptions. Intended for scene reloads / editor tests only.</summary>
        public static void Clear()
        {
            handlers.Clear();
        }
    }
}
