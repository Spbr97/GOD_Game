using System;
using System.Collections;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A brazier the player lights by interacting with it (SPEC.md section 26 —
    /// pressure-plate-style element matching, fire-themed for Agniya's temple, TASK
    /// 012's first puzzle). Burns out on its own after <see cref="burnSeconds"/>, so
    /// lighting every brazier in a group requires visiting them within that window
    /// rather than one at a time at leisure — a rule the player can see by watching one
    /// burn down, not a hidden combination (SPEC.md section 26's design rule).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FireBrazier : Interactable, IPuzzleElement
    {
        [Tooltip("Seconds this brazier stays lit before it needs relighting. Zero means it never goes out on its own.")]
        [SerializeField] private float burnSeconds = 8f;

        [Tooltip("Renderer tinted to show lit/unlit. Resolved from children if unset.")]
        [SerializeField] private Renderer visual;

        [SerializeField] private Color litColour = new(1f, 0.55f, 0.15f);
        [SerializeField] private Color unlitColour = new(0.25f, 0.2f, 0.18f);

        private MaterialPropertyBlock propertyBlock;
        private Coroutine burnRoutine;

        public bool IsLit { get; private set; }
        public bool IsSatisfied => IsLit;

        public event Action Changed;

        public override string Prompt => IsLit ? $"{DisplayName} is already lit" : $"Light {DisplayName}";
        public override bool CanInteract => !IsLit && base.CanInteract;

        private void Awake()
        {
            if (visual == null)
            {
                visual = GetComponentInChildren<Renderer>();
            }

            ApplyTint();
        }

        public override void Interact(GameObject interactor)
        {
            Light();
        }

        /// <summary>Lights the brazier. Public so tests, a scripted event or another ability can light one without an interaction.</summary>
        public void Light()
        {
            if (IsLit)
            {
                return;
            }

            IsLit = true;
            ApplyTint();
            Changed?.Invoke();

            if (burnSeconds > 0f)
            {
                if (burnRoutine != null)
                {
                    StopCoroutine(burnRoutine);
                }

                burnRoutine = StartCoroutine(BurnOutAfter(burnSeconds));
            }
        }

        /// <summary>Puts the brazier out immediately, whatever the reason. Public for tests and future puzzle pieces (a water jet, a gust of wind) that douse fire.</summary>
        public void Extinguish()
        {
            if (!IsLit)
            {
                return;
            }

            IsLit = false;

            if (burnRoutine != null)
            {
                StopCoroutine(burnRoutine);
                burnRoutine = null;
            }

            ApplyTint();
            Changed?.Invoke();
        }

        private IEnumerator BurnOutAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            burnRoutine = null;
            Extinguish();
        }

        private void ApplyTint()
        {
            if (visual == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            visual.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", IsLit ? litColour : unlitColour);
            visual.SetPropertyBlock(propertyBlock);
        }

        /// <summary>Test and tooling seam.</summary>
        public void ConfigureBrazier(float burnDuration)
        {
            burnSeconds = burnDuration;
        }
    }
}
