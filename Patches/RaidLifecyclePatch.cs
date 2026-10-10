using EFT;
using HarmonyLib;

namespace EFTEffectManager.Patches
{
    /// <summary>
    /// Attach the local raid controller and release it when its world is disposed.
    /// </summary>
    internal static class RaidLifecyclePatch
    {
        [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.OnGameStarted))]
        private static class RaidStarted
        {
            [HarmonyPostfix]
            private static void Postfix(GameWorld __instance)
            {
                EffectsManager.NotifyRaidStarted(__instance);
            }
        }

        [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.Dispose))]
        private static class WorldDisposing
        {
            [HarmonyPrefix]
            private static void Prefix(GameWorld __instance)
            {
                EffectsManager.NotifyRaidEnded(__instance);
            }
        }
    }
}