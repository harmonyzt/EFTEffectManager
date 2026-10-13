using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.HealthSystem;
using UnityEngine;

namespace EFTEffectManager
{
    /// <summary>
    /// Configuration for a registered custom effect.
    /// </summary>
    public sealed class EffectOptions
    {
        /// <summary>
        /// Localized name shown in the health UI tooltip (raw type name otherwise).
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Path to the PNG icon 
        /// </summary>
        public string IconFilePath { get; set; }

        /// <summary>
        /// Assembly that hosts IconFilePath. Usually typeof(Plugin).Assembly.
        /// </summary>
        public Assembly IconAssembly { get; set; }

        /// <summary>Existing sprite, takes priority over the file icon <see cref="IconFilePath"/>.</summary>
        public Sprite IconSprite { get; set; }

        /// <summary>
        /// Removes the custom effect when a matching effect starts on the same part. Matches classes and marker interfaces. (For example typeof(IFracture) would invalidate whatever custom effect you’ve had on a limb).
        /// </summary>
        public List<Type> InvalidatedBy { get; } = new List<Type>();

        /// <summary>
        /// Effect types whose cure creates this effect on the same body part. Fed by meds and stimulators alike (a stim that force-residues a fracture counts exactly like a medkit).
        /// </summary>
        public List<Type> MedHealTriggers { get; } = new List<Type>();

        /// <summary>
        /// Create this effect on the same body part when a STIMULATOR (never a med) cures one of these effect types
        /// For mods that want to react differently to stims than to medkits. A stim cure feeds both lists.
        /// </summary>
        public List<Type> StimHealTriggers { get; } = new List<Type>();

        /// <summary>
        /// Same as StimHealTriggers, but only fires when the cure came from a stimulator - for mods that want to treat stims differently from medkits.
        /// </summary>
        public List<Type> StimDebuffTriggers { get; } = new List<Type>();

        /// <summary>
        /// EStimulatorBuffTypes that, when a stimulator buffs with them (HealthRate, SkillRate, StaminaRate, …), create this effect on EBodyPart.Common.
        /// </summary>
        public List<EStimulatorBuffType> StimBuffTriggers { get; } = new List<EStimulatorBuffType>();

        /// <summary>
        /// Temporary skill levels granted while this effect is Started - stackable with stims and disappear again
        /// (residue, removal, raid end). Negative values take levels away. Zero entries are ignored.
        /// </summary>
        public Dictionary<ESkillId, int> SkillBuffs { get; } = new Dictionary<ESkillId, int>();

        /// <summary>
        /// 0 = regen, 1 = health loss. Set it together with the three rates below and the toolkit both APPLIES them every frame (real ChangeHealth / ChangeEnergy / ChangeHydration, one random body part per tick for health - 1:1 EFT replica) and advertises them through the EFT’s SetHealthRatesPerSecond plumbing so the health UI shows them.
        /// </summary>
        public float? EffectKind { get; set; }

        /// <summary>Health per second pushed by the effect (positive = heal). See <see cref="EffectKind"/>.</summary>
        public float HealthRegenPerSecond { get; set; }

        /// <summary>Energy drained per second (positive = drain). See <see cref="EffectKind"/>.</summary>
        public float EnergyDrainPerSecond { get; set; }

        /// <summary>Hydration drained per second (positive = drain). See <see cref="EffectKind"/>.</summary>
        public float HydrationDrainPerSecond { get; set; }

        /// <summary>
        /// Optional effect strength passed to AddEffect.
        /// Null means the vanilla default of 1. Rate effects scale their pushed rates by it.
        /// </summary>
        public float? Strength { get; set; }

        /// <summary>
        /// Optional timing overrides passed to AddEffect.
        /// Null means "use the effect class Default values" - i.e. an effect overriding <c>DefaultWorkTime</c> in your effect class gets exactly that lifetime.
        /// </summary>
        public float? DelayTime { get; set; }

        /// <summary>
        /// Optional effect duration passed to AddEffect.
        /// Null means "use the effect class Default values".
        /// </summary>
        public float? WorkTime { get; set; }

        /// <summary>
        /// Optional effect residue time passed to AddEffect.
        /// Null means "use the effect class Default values".
        /// </summary>
        public float? ResidueTime { get; set; }

        /// <summary>Remove this effect when its body part is destroyed (default true).</summary>
        public bool RemoveOnBodyPartDestroyed { get; set; } = true;

        /// <summary>
        /// Keep the effect in the profile between raids.
        /// Requires IRestorable and a correctly typed OfflineEffectType with the same name and marker.
        /// </summary>
        public bool PersistAcrossRaids { get; set; } = true;

        /// <summary>Optional OfflineHealthController.Effect. Same class name and marker as the active effect.</summary>
        public Type OfflineEffectType { get; set; }

        /// <summary>Optional NetworkHealthController.Effect</summary>
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

            if (Strength.HasValue)
            {
                ValidateFinite(Strength.Value, nameof(Strength));
            }

            ValidateTime(DelayTime, nameof(DelayTime));
            ValidateTime(WorkTime, nameof(WorkTime));
            ValidateTime(ResidueTime, nameof(ResidueTime));

            foreach (EStimulatorBuffType buff in StimBuffTriggers)
                if (!Enum.IsDefined(typeof(EStimulatorBuffType), buff))
                {
                    throw new ArgumentException($"Unknown stim buff type: {buff}.");
                }

            // Validate skills too
            foreach (ESkillId skill in SkillBuffs.Keys)
            {
                if (!Enum.IsDefined(typeof(ESkillId), skill))
                {
                    throw new ArgumentException($"Unknown skill id: {skill}.");
                }
            }

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

        /// <summary>True when any rate is configured (i.e. the effect touches health/energy/hydration)</summary>
        internal bool HasRates =>
            !Mathf.Approximately(HealthRegenPerSecond, 0f)
            || !Mathf.Approximately(EnergyDrainPerSecond, 0f)
            || !Mathf.Approximately(HydrationDrainPerSecond, 0f);

        private static void ValidateFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentException($"EffectOptions.{name} must be finite.");
            }
        }

        private static void ValidateTime(float? value, string name)
        {
            if (value.HasValue && (float.IsNaN(value.Value) || value.Value < 0f))
            {
                throw new ArgumentException($"EffectOptions.{name} must be non-negative or positive infinity.");
            }
        }

        private static void ValidateEffectTypes(List<Type> types, string listName)
        {
            foreach (Type type in types)
            {
                if (type == null || !typeof(IHealthEffect).IsAssignableFrom(type))
                {
                    {
                        throw new ArgumentException(
                            $"EffectOptions.{listName} entries must implement IHealthEffect (got: {type?.FullName ?? "null"}).");
                    }
                }
            }
        }
    }
}