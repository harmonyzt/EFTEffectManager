using System;
using System.Collections.Generic;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using UnityEngine;

namespace EFTEffectManager
{
    /// <summary>
    /// Health/energy/hydration rate plumbing for custom regen / degradation effects
    /// (see <see cref="EffectOptions.EffectKind"/>).
    ///
    /// <c>Effect.SetHealthRatesPerSecond</c> only ADVERTISES rates (buff UI + network sync) - the controller's HealRate / DamageRate
    /// are never read by gameplay code. Every vanilla rate-carrying effect (Stimulator buffs, Dehydration, Exhaustion, ...)
    /// applies the real change itself in its own RegularUpdate tick.
    /// </summary>

    // Useful Stuff
    // Sign rules ("health flips, drains stay drains"):
    // <see cref="EffectOptions.HealthRegenPerSecond"/> - positive = healing in REGEN mode, negated in DEGRADATION mode (positive then = damage/sec).
    // <see cref="EffectOptions.EnergyDrainPerSecond"/> / <see cref="EffectOptions.HydrationDrainPerSecond"/> - positive = draining in BOTH modes (negative = a gain).
    //
    // Everything is scaled by the effect's Strength.
    internal static class EffectRateHelper
    {
        private static readonly System.Random Shuffle = new System.Random();

        /// <summary>
        /// ADVERTISE: pushes the configured rates through the vanilla <c>Effect.SetHealthRatesPerSecond</c> (buff UI / network sync)
        /// </summary>
        internal static void Apply(ActiveHealthController.Effect effect, EffectOptions options)
        {
            if (effect == null || options == null || !options.IsRateEffect
                || !EffectsManager.IsRaidController(effect.HealthController))
            {
                return;
            }

            try
            {
                if (effect.State != EEffectState.Started)
                {
                    return;
                }

                float strength = effect.Strength;

                // Advertised rates: positive health = heal, positive energy/hydration = gain (the vanilla UI expects that orientation).
                float sign = options.EffectKind.Value >= 0.5f ? -1f : 1f;
                float health = options.HealthRegenPerSecond * sign * strength;
                float energy = -options.EnergyDrainPerSecond * strength;
                float hydration = -options.HydrationDrainPerSecond * strength;

                effect.SetHealthRatesPerSecond(health, energy, hydration, 0f);
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError(
                    $"EFTEffectManager: advertising rates for '{effect?.GetType().Name}' failed: {e}");
            }
        }

        /// <summary>
        /// APPLY: called from the RegularUpdate patch for every active effect - only
        /// registered rate effects do anything. Applies one tick of health (one
        /// random body part, vanilla stim pattern) plus energy/hydration.
        /// </summary>
        internal static void Tick(ActiveHealthController.Effect effect, float deltaTime)
        {
            if (deltaTime <= 0f
                || effect == null
                || effect.State != EEffectState.Started
                || EffectsManager.Registrations.Count == 0
                || !EffectsManager.TryGetRegistration(effect, out EffectRegistration registration)
                || !registration.IsRateEffect)
            {
                return;
            }

            try
            {
                EffectOptions options = registration.Options;
                float strength = effect.Strength;
                ActiveHealthController controller = effect.HealthController;
                if (controller == null || !controller.IsAlive || !EffectsManager.IsRaidController(controller))
                {
                    return;
                }

                TickHealth(controller, options, strength, deltaTime);

                float energy = -options.EnergyDrainPerSecond * strength * deltaTime;
                if (energy != 0f)
                {
                    controller.ChangeEnergy(energy);
                }

                float hydration = -options.HydrationDrainPerSecond * strength * deltaTime;
                if (hydration != 0f)
                {
                    controller.ChangeHydration(hydration);
                }
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError($"EFTEffectManager: rate tick for '{effect.GetType().Name}' failed: {e}");
            }
        }

        /// <summary>
        /// Re-pushes the advertised rates of every registered rate effect currently active on this body part
        /// </summary>
        internal static void ApplyAll(ActiveHealthController controller, EBodyPart bodyPart)
        {
            if (controller == null)
            {
                return;
            }

            foreach (EffectRegistration registration in EffectsManager.Registrations.Values)
            {
                registration.ApplyRates(controller, bodyPart);
            }
        }

        /// <summary>
        /// One tick of health on a single random real body part (exactly like EFT)
        /// </summary>
        private static void TickHealth(ActiveHealthController controller, EffectOptions options, float strength,
            float deltaTime)
        {
            float amount = options.HealthRegenPerSecond * strength * deltaTime;
            if (options.EffectKind.Value >= 0.5f)
            {
                // Degradation flips the health sign only
                amount = -amount;
            }

            if (Mathf.Approximately(amount, 0f))
            {
                return;
            }

            bool heal = amount > 0f;
            DamageInfo damageInfo = new DamageInfo
            {
                DamageType = heal ? EDamageType.Medicine : EDamageType.Stimulator,
                Damage = Mathf.Abs(amount),
            };

            IReadOnlyList<EBodyPart> parts = HealthHelper.RealBodyParts;
            int startIndex = Shuffle.Next(parts.Count);
            for (int i = 0; i < parts.Count; i++)
            {
                EBodyPart part = parts[(startIndex + i) % parts.Count];
                if (controller.IsBodyPartDestroyed(part))
                {
                    continue;
                }

                var health = controller.GetBodyPartHealth(part);
                if (heal ? health.AtMaximum : health.AtMinimum)
                {
                    continue;
                }

                controller.ChangeHealth(part, amount, damageInfo);
                if (!heal && controller.GetBodyPartHealth(part).AtMinimum)
                    controller.DestroyBodyPart(part, EDamageType.Stimulator);
                break;
            }
        }
    }
}