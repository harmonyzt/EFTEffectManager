using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.HealthSystem;
using HarmonyLib;

namespace EFTEffectManager.Patches
{
    [HarmonyPatch]
    internal static class PersistencePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.Store),
                new[] { typeof(Profile.HealthInfo) });
            yield return AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.Store),
                new[] { typeof(GlobalConfiguration.TransitGlobalSettings), typeof(Profile.HealthInfo) });
            yield return AccessTools.Method(typeof(OfflineHealthController), nameof(OfflineHealthController.Store),
                new[] { typeof(Profile.HealthInfo) });
        }

        private static void Postfix(Profile.HealthInfo __result)
        {
            if (__result?.BodyParts == null) return;
            foreach (EffectRegistration registration in EffectsManager.Registrations.Values)
            {
                if (registration.Options.PersistAcrossRaids) continue;
                foreach (Profile.HealthInfo.BodyPartInfo part in __result.BodyParts.Values)
                    part.Effects?.Remove(registration.EffectType.Name);
            }
        }
    }
}