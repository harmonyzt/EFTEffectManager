using EFT.HealthSystem;
using HarmonyLib;

namespace EFTEffectManager.Patches
{
    // StoreValues runs on every stim application
    [HarmonyPatch(typeof(ActiveHealthController.Stimulator), nameof(ActiveHealthController.Stimulator.StoreValues))]
    internal static class StimUsagePatch
    {
        private static void Prefix(ActiveHealthController.Stimulator __instance, string buffsName, string templateId, EBodyPart appliedTo) =>
            EffectsManager.NotifyStimApplied(__instance, buffsName, templateId, appliedTo);
    }
}