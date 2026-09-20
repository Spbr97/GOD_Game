using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A trigger volume that damages anything standing in it on a fixed cadence
    /// (SPEC.md TASK 002 — environmental damage).
    ///
    /// Not named in the TASK 002 component list. It is here because "player can die"
    /// and "checkpoint restores player" are acceptance criteria, and nothing else in
    /// this task can damage the player: enemies have no AI until TASK 004. It is a
    /// real system rather than a test fixture — hazard volumes are needed for the
    /// temples regardless.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DamageVolume : MonoBehaviour
    {
        [SerializeField] private float damagePerTick = 15f;
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField] private DamageType damageType = DamageType.Environmental;
        [SerializeField] private LayerMask affectedLayers = ~0;

        private readonly Dictionary<Hurtbox, float> nextTickAt = new();

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerStay(Collider other)
        {
            if ((affectedLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            var hurtbox = other.GetComponentInParent<Hurtbox>();
            if (hurtbox == null)
            {
                return;
            }

            if (nextTickAt.TryGetValue(hurtbox, out var due) && Time.time < due)
            {
                return;
            }

            nextTickAt[hurtbox] = Time.time + tickInterval;

            // A fresh attack id per tick, so consecutive ticks are distinct attacks
            // and are not swallowed by the duplicate-damage guard.
            hurtbox.ApplyDamage(new DamageData
            {
                Amount = damagePerTick,
                Type = damageType,
                Source = gameObject,
                HitPoint = other.ClosestPoint(transform.position),
                Direction = Vector3.up,
                AttackId = DamageData.NextAttackId(),
                Unblockable = true
            });
        }

        private void OnTriggerExit(Collider other)
        {
            var hurtbox = other.GetComponentInParent<Hurtbox>();
            if (hurtbox != null)
            {
                nextTickAt.Remove(hurtbox);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            var volume = GetComponent<Collider>();
            if (volume != null)
            {
                Gizmos.DrawWireCube(volume.bounds.center, volume.bounds.size);
            }
        }
    }
}
