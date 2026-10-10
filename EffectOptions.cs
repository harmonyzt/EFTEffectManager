using System;
using System.Collections.Generic;
using System.Reflection;
using EFT.HealthSystem;
using UnityEngine;

namespace EFTEffectManager
{
    /// <summary>
    /// Configuration for a registered custom effect.
    /// </summary>
    public sealed class EffectOptions
    {
        /// <summary>Localized name shown for the effect in the health UI tooltip (falls back to the raw type-name key).</summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// PNG file with the effect icon. Relative paths resolve next to
        /// <see cref="IconAssembly"/> (pass the assembly of the plugin that owns the effect);
        /// Absolute paths are used as-is.
        /// NOTE: A missing/invalid image is a user error and the effect will be hidden from the vanilla health UI.
        /// </summary>
        public string IconFilePath { get; set; }

        /// <summary>
        /// Assembly that hosts <see cref="IconFilePath"/> (usually <c>typeof(Plugin).Assembly</c>).
        /// When null it falls back to the plugin assembly passed to <c>EffectsManager.RegisterEffect&lt;T&gt;(Assembly, ...)</c> /
        /// <c>RegisterEffectsFrom(Assembly, ...)</c>.
        /// Mods in their own folder should always pass one of the two.
        /// </summary>
        public Assembly IconAssembly { get; set; }

        /// <summary>Explicit sprite. Takes priority over <see cref="IconFilePath"/>.</summary>
        public Sprite IconSprite { get; set; }

        /// <summary>
        /// Remove this effect when any effect assignable to one of these types STARTS
        /// on the same body part. E.g. typeof(IFracture) = a re-broken bone drops the
        /// effect you've created, or its state.
        /// </summary>
        public List<Type> InvalidatedBy { get; } = new List<Type>();

        /// <summary>
        /// Create this effect on the same body part when a med CURES one of these effect types.
        /// Triggers by a med heal (<see cref="ActiveHealthController.MedEffect"/>), and
        /// a stimulator / other med - every Stimulator buff that
        /// force-residues or removes effects so injecting a stim or using surgery counts exactly like using a medkit.
        /// Skipped for dead controllers.
        /// </summary>
        public List<Type> MedHealTriggers { get; } = new List<Type>();

        /// <summary>
        /// Create this effect on the same body part when a STIMULATOR (never a med) cures one of these effect types
        /// For mods that want to react differently to stims than to medkits. A stim cure feeds both lists.
        /// </summary>
        public List<Type> StimHealTriggers { get; } = new List<Type>();

        /// <summary>
        /// Create this effect on the same body part when a treatment DEBUFFS one of
        /// these effect types with a stimulator - i.e. the stim adds/refreshes the
        /// listed native effect. Choose harmful effect types for debuff-only behavior.
        /// Nothing fires when the stim fails to roll, is absorbed by immunity or no matching native effect is added or refreshed.
        /// </summary>
        public List<Type> StimDebuffTriggers { get; } = new List<Type>();

        /// <summary>
        /// Create this effect on <see cref="EBodyPart.Common"/> when a stimulator BUFFS
        /// with one of these buff types (PainKiller, Stimulator, Adrenaline).
        /// Fires on accepted activations whose native buff remains Active. One-shot entries use StimBuffChanged instead.
        /// </summary>
        public List<EStimulatorBuffType> StimBuffTriggers { get; } = new List<EStimulatorBuffType>();

        /// <summary>
        /// Health/energy/hydration rates applied while this effect is started.
        /// Leave <see cref="EffectKind"/> null for a plain effect with no rate changes.
        /// <list type="bullet">
        /// <item><see cref="HealthRegenPerSecond"/> - health per second, positive = healing.
        /// Negated in DEGRADATION mode (positive then means damage/sec).</item>
        /// <item><see cref="EnergyDrainPerSecond"/> / <see cref="HydrationDrainPerSecond"/>
        /// - drains per second, positive = draining in BOTH modes; pass a negative value for a gain.</item>
        /// </list>
        /// Ticks the real ChangeHealth / ChangeEnergy / ChangeHydration calls once per native RegularUpdate AND advertises the rates
        /// through the vanilla SetHealthRatesPerSecond plumbing so the health UI shows them. All values are multiplied by the effect's <see cref="Strength"/>.
        /// </summary>
        public float? EffectKind { get; set; }

        /// <summary>Health per second pushed by the effect (positive = heal). See <see cref="EffectKind"/>.</summary>
        public float HealthRegenPerSecond { get; set; }

        /// <summary>Energy drained per second (positive = drain). See <see cref="EffectKind"/>.</summary>
        public float EnergyDrainPerSecond { get; set; }

        /// <summary>Hydration drained per second (positive = drain). See <see cref="EffectKind"/>.</summary>
        public float HydrationDrainPerSecond { get; set; }

        /// <summary>
        /// Optional effect strength passed straight to AddEffect.
        /// Null (default) means the vanilla default of 1. Rate effects scale their pushed rates by it.
        /// </summary>
        public float? Strength { get; set; }

        /// <summary>
        /// Optional timing overrides passed straight to AddEffect. Null (default)
        /// means "use the effect class' Default* values" - e.g. an effect overriding <c>DefaultWorkTime</c> gets exactly that lifetime.
        /// </summary>
        public float? DelayTime { get; set; }

        public float? WorkTime { get; set; }
        public float? ResidueTime { get; set; }

        /// <summary>Remove this effect when its body part is destroyed (default true).</summary>
        public bool RemoveOnBodyPartDestroyed { get; set; } = true;

        /// <summary>
        /// Keep the effect in the profile between raids.
        /// Requires IRestorable and a correctly typed OfflineEffectType with the same name and marker.
        /// </summary>
        public bool PersistAcrossRaids { get; set; } = true;

        /// <summary>Optional OfflineHealthController.Effect counterpart. Same class name and marker as the active effect.</summary>
        public Type OfflineEffectType { get; set; }

        /// <summary>Optional NetworkHealthController.Effect counterpart. This alone does not establish multiplayer support.</summary>
        public Type NetworkEffectType { get; set; }

        /// <summary>True for a rate effect that actually pushes something.</summary>
        internal bool IsRateEffect => EffectKind.HasValue && HasRates;

        internal void Validate()
        {
            ValidateEffectTypes(InvalidatedBy, nameof(InvalidatedBy));
            ValidateEffectTypes(MedHealTriggers, nameof(MedHealTriggers));
            ValidateEffectTypes(StimHealTriggers, nameof(StimHealTriggers));
            ValidateEffectTypes(StimDebuffTriggers, nameof(StimDebuffTriggers));

            ValidateFinite(HealthRegenPerSecond, nameof(HealthRegenPerSecond));
            ValidateFinite(EnergyDrainPerSecond, nameof(EnergyDrainPerSecond));
            ValidateFinite(HydrationDrainPerSecond, nameof(HydrationDrainPerSecond));
            if (Strength.HasValue) ValidateFinite(Strength.Value, nameof(Strength));
            ValidateTime(DelayTime, nameof(DelayTime));
            ValidateTime(WorkTime, nameof(WorkTime));
            ValidateTime(ResidueTime, nameof(ResidueTime));
            foreach (EStimulatorBuffType buff in StimBuffTriggers)
                if (!Enum.IsDefined(typeof(EStimulatorBuffType), buff))
                    throw new ArgumentException($"Unknown stim buff type: {buff}.");

            if (EffectKind.HasValue && EffectKind.Value != 0f && EffectKind.Value != 1f)
            {
                throw new ArgumentException(
                    $"EffectOptions.EffectKind must be 0 (regen) or 1 (degradation), got {EffectKind.Value}.");
            }

            if (EffectKind.HasValue && !HasRates)
            {
                EffectsManager.Log.LogWarning(
                    "EFTEffectManager: EffectKind is set but every rate is 0 - the effect will not push any " +
                    "health/energy/hydration rate. Set HealthRegenPerSecond / EnergyDrainPerSecond / " +
                    "HydrationDrainPerSecond or drop EffectKind.");
            }
        }

        /// <summary>True when any rate is configured (i.e. the effect touches health/energy/hydration).</summary>
        internal bool HasRates =>
            !Mathf.Approximately(HealthRegenPerSecond, 0f)
            || !Mathf.Approximately(EnergyDrainPerSecond, 0f)
            || !Mathf.Approximately(HydrationDrainPerSecond, 0f);

        private static void ValidateFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException($"EffectOptions.{name} must be finite.");
        }

        private static void ValidateTime(float? value, string name)
        {
            if (value.HasValue && (float.IsNaN(value.Value) || value.Value < 0f))
                throw new ArgumentException($"EffectOptions.{name} must be non-negative or positive infinity.");
        }

        private static void ValidateEffectTypes(List<Type> types, string listName)
        {
            foreach (Type type in types)
            {
                if (type == null || !typeof(IHealthEffect).IsAssignableFrom(type))
                {
                    throw new ArgumentException(
                        $"EffectOptions.{listName} entries must implement IHealthEffect (got: {type?.FullName ?? "null"}).");
                }
            }
        }
    }
}