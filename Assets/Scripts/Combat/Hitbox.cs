using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The damaging volume of a swing (SPEC.md TASK 002). Inactive by default; a
    /// <see cref="WeaponController"/> opens it for the active frames of an attack.
    ///
    /// Two things guard against duplicate damage. This component remembers which
    /// hurtboxes it has already touched during the current swing, and
    /// <see cref="HealthComponent"/> independently rejects a repeated
    /// <see cref="DamageData.AttackId"/>. The first stops repeated trigger callbacks,
    /// the second stops two hurtboxes on one enemy both landing the same swing.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        [SerializeField] private GameObject owner;
        [SerializeField] private LayerMask hittableLayers = ~0;

        [Tooltip("Hurtboxes of this same faction are not hit. Neutral hits everything.")]
        [SerializeField] private Faction faction = Faction.Neutral;

        private readonly HashSet<Hurtbox> hitThisSwing = new();
        private Collider volume;
        private DamageData template;
        private Vector3 previousCenter;

        public bool IsActive { get; private set; }

        /// <summary>Hurtboxes damaged during the current (or most recent) swing.</summary>
        public int HitCountThisSwing => hitThisSwing.Count;

        private void Awake()
        {
            EnsureResolved();
            volume.enabled = false;
        }

        /// <summary>
        /// Resolves the collider and owner on demand. Awake does not run in EditMode
        /// tests and does not re-run after a reparent, so this cannot live there alone.
        /// </summary>
        private void EnsureResolved()
        {
            if (volume == null)
            {
                volume = GetComponent<Collider>();
                volume.isTrigger = true;
            }

            if (owner == null)
            {
                owner = transform.root.gameObject;
            }
        }

        /// <summary>
        /// Opens the hitbox for one swing. The caller supplies the damage template;
        /// a fresh <see cref="DamageData.AttackId"/> is allocated here so every
        /// activation is a distinct attack for deduplication purposes.
        /// </summary>
        public void Activate(DamageData damageTemplate)
        {
            EnsureResolved();
            hitThisSwing.Clear();

            template = damageTemplate;
            template.Source = owner;
            template.AttackId = DamageData.NextAttackId();

            IsActive = true;
            volume.enabled = true;
            previousCenter = volume.bounds.center;

            // OnTriggerEnter does not fire for colliders that are already overlapping
            // when the volume is enabled, which is the common case for a short swing
            // against an enemy standing in contact. Sweep once on activation.
            SweepCurrentOverlaps();
        }

        public void Deactivate()
        {
            IsActive = false;
            if (volume != null)
            {
                volume.enabled = false;
            }
        }

        private void OnDisable()
        {
            Deactivate();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void LateUpdate()
        {
            if (!IsActive || volume == null)
            {
                return;
            }

            var bounds = volume.bounds;
            var displacement = bounds.center - previousCenter;
            if (displacement.sqrMagnitude > 0.000001f)
            {
                // The world-space bounds conservatively enclose rotated hitboxes.
                // Cast from the last sampled position so a thin target crossed
                // between physics steps cannot be skipped.
                var hits = Physics.BoxCastAll(previousCenter, bounds.extents,
                    displacement.normalized, Quaternion.identity, displacement.magnitude,
                    hittableLayers, QueryTriggerInteraction.Collide);
                foreach (var hit in hits)
                {
                    TryHit(hit.collider);
                }
            }

            SweepCurrentOverlaps();
            previousCenter = bounds.center;
        }

        private void SweepCurrentOverlaps()
        {
            var bounds = volume.bounds;
            var overlaps = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, hittableLayers, QueryTriggerInteraction.Collide);
            foreach (var other in overlaps)
            {
                TryHit(other);
            }
        }

        private void TryHit(Collider other)
        {
            if (!IsActive || other == null)
            {
                return;
            }

            if ((hittableLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            var hurtbox = other.GetComponent<Hurtbox>();
            if (hurtbox == null || hurtbox.Owner == owner)
            {
                return;
            }

            // No friendly fire. Two enemies swinging at the same player stand inside
            // each other's volumes, and without this they kill each other.
            if (faction != Faction.Neutral && hurtbox.Faction == faction)
            {
                return;
            }

            if (!hitThisSwing.Add(hurtbox))
            {
                return;
            }

            var damage = template;
            damage.HitPoint = other.ClosestPoint(transform.position);
            var toVictim = hurtbox.Owner.transform.position - (owner != null ? owner.transform.position : transform.position);
            damage.Direction = toVictim.sqrMagnitude > 0.0001f ? toVictim.normalized : transform.forward;

            if (hurtbox.ApplyDamage(damage))
            {
                GameLogger.Log(LogCategory.Combat, $"{owner?.name} hit {hurtbox.Owner.name} (attack {damage.AttackId}).", this);
            }
        }

        /// <summary>
        /// Sets the attacking entity without touching the layer mask. Needed because
        /// the default owner is <c>transform.root</c>, which is wrong as soon as an
        /// entity is parented under a grouping object: several enemies under one
        /// encounter would share a root, and an enemy's own hurtbox would no longer
        /// be recognised as its own, letting it damage itself.
        /// </summary>
        public void SetOwner(GameObject newOwner)
        {
            owner = newOwner;
        }

        /// <summary>Sets which side this attack belongs to, without touching the owner or layers.</summary>
        public void SetFaction(Faction attackFaction)
        {
            faction = attackFaction;
        }

        /// <summary>Editor/test seam.</summary>
        public void Configure(GameObject newOwner, LayerMask layers)
        {
            owner = newOwner;
            hittableLayers = layers;
        }
    }
}
