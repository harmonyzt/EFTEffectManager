using System;
using System.Linq;
using EFT.HealthSystem;
using HarmonyLib;

namespace EFTEffectManager.Patches
{
    [HarmonyPatch(typeof(ActiveHealthController.Stimulator), nameof(ActiveHealthController.Stimulator.ActivateBuff))]
    internal static class StimulatorPatch
    {
        private sealed class EffectBefore
        {
            public IHealthEffect Effect;
            public bool Active;
            public float WorkTime;
            public float Strength;
        }

        private sealed class StimState
        {
            public ActiveHealthController Controller;
            public ActiveHealthController.Stimulator.Buff Buff;
            public EffectBefore[] Before;
            public StimState Previous;
            public bool Accepted;
        }

        [ThreadStatic] private static StimState _current;

        private static EffectBefore[] Snapshot(ActiveHealthController controller) => controller.Effects
            .Where(effect => effect.GetType().Assembly == typeof(ActiveHealthController).Assembly)
            .Select(effect => new EffectBefore
            {
                Effect = effect, Active = effect.Active,
                WorkTime = effect.WorkStateTime, Strength = effect.Strength
            }).ToArray();

        private static void Prefix(ActiveHealthController.Stimulator __instance,
            ActiveHealthController.Stimulator.Buff buff, bool activate, out StimState __state)
        {
            __state = null;
            if (!EffectsManager.IsRaidController(__instance.HealthController) || buff == null)
            {
                return;
            }

            __state = new StimState
            {
                Controller = __instance.HealthController,
                Buff = buff,
                Before = activate ? Snapshot(__instance.HealthController) : Array.Empty<EffectBefore>(),
                Previous = _current
            };

            _current = __state;
        }

        private static void Postfix(bool activate, StimState __state)
        {
            Restore(__state);
            if (__state == null || !__state.Accepted)
            {
                return;
            }

            try
            {
                // Complete both snapshots before dispatching callbacks that can add/remove effects.
                EffectBefore[] after = activate ? Snapshot(__state.Controller) : Array.Empty<EffectBefore>();
                IHealthEffect[] cured = __state.Before.Where(before => before.Active && !before.Effect.Active)
                    .Select(before => before.Effect).ToArray();

                var beforeByEffect = __state.Before.ToDictionary(before => before.Effect);
                IHealthEffect[] applied = after.Where(current =>
                        !beforeByEffect.TryGetValue(current.Effect, out EffectBefore before)
                        || before.WorkTime != current.WorkTime || before.Strength != current.Strength)
                    .Select(current => current.Effect).ToArray();

                foreach (IHealthEffect effect in cured)
                {
                    EffectsManager.NotifyStimHeal(__state.Controller, effect);
                }

                foreach (IHealthEffect effect in applied)
                {
                    EffectsManager.NotifyStimDebuff(__state.Controller, effect);
                }

                EffectsManager.NotifyStimBuff(__state.Controller, __state.Buff, activate);
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError($"EFTEffectManager: stim outcome handling failed: {e}");
            }
        }

        private static void Restore(StimState state)
        {
            if (state != null && ReferenceEquals(_current, state))
            {
                _current = state.Previous;
            }
        }

        private static Exception Finalizer(Exception __exception, StimState __state)
        {
            Restore(__state);
            
            return __exception;
        }

        // Chance and immunity failures return before this activation notification.
        // One-shot buffs can reach it with Active=false, so Active alone cannot prove acceptance
        [HarmonyPatch(typeof(BaseHealthController<ActiveHealthController.Effect>),
            nameof(BaseHealthController<ActiveHealthController.Effect>.StimulatorBuffEventCall))]
        private static class AcceptedBuff
        {
            private static void Postfix(BaseHealthController<ActiveHealthController.Effect> __instance,
                IStimulatorBuff buff, bool isUpdateCall)
            {
                if (!isUpdateCall && _current != null && ReferenceEquals(_current.Controller, __instance) &&
                    ReferenceEquals(_current.Buff, buff))
                {
                    _current.Accepted = true;
                }
            }
        }
    }
}