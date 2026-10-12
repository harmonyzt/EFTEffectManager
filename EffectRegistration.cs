using System;
using System.Collections.Generic;
using System.Diagnostics;
using EFT;
using EFT.HealthSystem;

namespace EFTEffectManager
{
    /// <summary>
    /// Handle for a registered custom effect.
    /// This handle exposes typed add/remove/find helpers
    /// </summary>
    public sealed class EffectRegistration
    {
        public Type EffectType { get; }
        public Type InterfaceType { get; }
        public EffectOptions Options { get; }

        private readonly Func<ActiveHealthController, EBodyPart, IHealthEffect> _add;
        private readonly Action<ActiveHealthController, EBodyPart> _remove;
        private readonly Func<ActiveHealthController, EBodyPart, IHealthEffect> _find;
        private readonly Func<ActiveHealthController, EBodyPart, IHealthEffect> _findExisting;

        internal EffectRegistration(
            Type effectType,
            Type interfaceType,
            EffectOptions options,
            Func<ActiveHealthController, EBodyPart, IHealthEffect> add,
            Action<ActiveHealthController, EBodyPart> remove,
            Func<ActiveHealthController, EBodyPart, IHealthEffect> find,
            Func<ActiveHealthController, EBodyPart, IHealthEffect> findExisting)
        {
            EffectType = effectType;
            InterfaceType = interfaceType;
            Options = options;
            _add = add;
            _remove = remove;
            _find = find;
            _findExisting = findExisting;
        }

        /// <summary>Active instance of this effect on the body part, or null.</summary>
        public IHealthEffect Find(IHealthController controller, EBodyPart bodyPart)
        {
            ActiveHealthController active = controller as ActiveHealthController;
            
            return active == null ? null : _find(active, bodyPart);
        }

        /// <summary>Find an Added or Started instance, including a delayed effect.</summary>
        public IHealthEffect FindExisting(IHealthController controller, EBodyPart bodyPart) =>
            controller is ActiveHealthController active ? _findExisting(active, bodyPart) : null;

        /// <summary>
        /// Creates the effect on the body part (non-stackable effects reuse the existing instance and extend its native timer).
        /// </summary>
        public bool Add(IHealthController controller, EBodyPart bodyPart)
        {
            try
            {
                ActiveHealthController active = controller as ActiveHealthController;
                
                if (active == null || !active.IsAlive || !EffectsManager.IsRaidController(controller))
                {
                    return false;
                }

                if (Options.RemoveOnBodyPartDestroyed && bodyPart != EBodyPart.Common
                                                      && active.IsBodyPartDestroyed(bodyPart))
                {
                    return false;
                }

                if (_add(active, bodyPart) == null)
                {
                    return false;
                }
                
                EffectsManager.Log.LogInfo($"EFTEffectManager: '{EffectType.Name}' applied on {bodyPart}.");
                
                return true;
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError($"EFTEffectManager: adding '{EffectType.Name}' on {bodyPart} failed: {e}");
                
                return false;
            }
        }

        /// <summary>Force removes the effect from the body part if it exists.</summary>
        public void Remove(IHealthController controller, EBodyPart bodyPart)
        {
            try
            {
                ActiveHealthController active = controller as ActiveHealthController;
                if (active == null)
                {
                    return;
                }

                _remove(active, bodyPart);
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError(
                    $"EFTEffectManager: removing '{EffectType.Name}' from {bodyPart} failed: {e}");
            }
        }

        /// <summary>True when a just cured effect matches one of the MedHealTriggers types.</summary>
        internal bool IsMedHealTrigger(IHealthEffect cured)
        {
            return Matches(Options.MedHealTriggers, cured);
        }

        /// <summary>True when a stimulator debuffed with one of the StimDebuffTriggers types.</summary>
        internal bool IsStimDebuffTrigger(IHealthEffect effect)
        {
            return Matches(Options.StimDebuffTriggers, effect);
        }

        /// <summary>True when a stimulator buffed with one of the StimBuffTriggers types.</summary>
        internal bool IsStimBuffTrigger(EStimulatorBuffType buffType)
        {
            return Matches(Options.StimBuffTriggers, buffType);
        }

        /// <summary>True when a stimulated cure matches one of the StimHealTriggers types.</summary>
        internal bool IsStimHealTrigger(IHealthEffect cured)
        {
            return Matches(Options.StimHealTriggers, cured);
        }

        /// <summary>True when this effect pushes health/energy/hydration rates (regen or degradation).</summary>
        internal bool IsRateEffect => Options.IsRateEffect;

        /// <summary>
        /// Pushes the configured health/energy/hydration rates of THIS body part's instance through the vanilla rate plumbing.
        /// </summary>
        internal void ApplyRates(IHealthController controller, EBodyPart bodyPart)
        {
            ActiveHealthController active = controller as ActiveHealthController;
            if (active == null || !IsRateEffect)
            {
                return;
            }

            if (_find(active, bodyPart) is ActiveHealthController.Effect instance)
            {
                EffectRateHelper.Apply(instance, Options);
            }
        }

        /// <summary>True when a just started effect matches one of the InvalidatedBy types.</summary>
        internal bool IsInvalidating(IHealthEffect started)
        {
            return Matches(Options.InvalidatedBy, started);
        }

        private static bool Matches(List<Type> types, IHealthEffect effect)
        {
            if (types.Count == 0 || effect == null)
            {
                return false;
            }

            Type effectType = effect.GetType();
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i].IsAssignableFrom(effectType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Matches(List<EStimulatorBuffType> types, EStimulatorBuffType value)
        {
            if (types.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}