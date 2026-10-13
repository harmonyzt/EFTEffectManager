using System;
using System.Linq;
using EFT.HealthSystem;
using HarmonyLib;

namespace EFTEffectManager.Patches
{
    [HarmonyPatch(typeof(ActiveHealthController.MedEffect), nameof(ActiveHealthController.MedEffect.Residue))]
    internal static class MedCurePatch
    {
        private static void Prefix(ActiveHealthController.MedEffect __instance, out IHealthEffect[] __state)
        {
            __state = EffectsManager.IsRaidController(__instance.HealthController)
                ? __instance.HealthController.Effects.Where(effect =>
                        effect.Active && effect.GetType().Assembly == typeof(ActiveHealthController).Assembly)
                    .Cast<IHealthEffect>().ToArray()
                : Array.Empty<IHealthEffect>();
        }

        private static void Postfix(ActiveHealthController.MedEffect __instance, IHealthEffect[] __state)
        {
            IHealthEffect[] cured = __state.Where(effect => !effect.Active).ToArray();
            foreach (IHealthEffect effect in cured)
                EffectsManager.NotifyMedCured(__instance.HealthController, effect);
        }
    }
}