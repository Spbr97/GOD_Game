using UnityEngine;

namespace Game.World
{
    /// <summary>Checks whether a solid collider separates an observer and a target.</summary>
    public static class Visibility
    {
        public static bool HasLineOfSight(Transform observer, Vector3 origin,
            Transform target, Vector3 destination)
        {
            var ray = destination - origin;
            var distance = ray.magnitude;
            if (distance < 0.001f)
            {
                return true;
            }

            foreach (var hit in Physics.RaycastAll(origin, ray / distance, distance,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(observer)
                    || collider.transform.IsChildOf(target))
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
