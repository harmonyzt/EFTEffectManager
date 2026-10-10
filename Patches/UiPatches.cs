using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace EFTEffectManager.Patches
{
    /// <summary>
    /// UI integration.
    /// Resolving health UI icons through EFTHardSettings...EffectIcons.EffectIcons[interfaceType].
    /// The table is saved and flushed.. if configured correctly
    /// </summary>
    internal static class UiPatches
    {
        [HarmonyPatch]
        public static class EffectIconsFlushPatch
        {
            private static MethodBase TargetMethod()
            {
                Type effectSprites = typeof(EFT.UI.StaticIcons).GetNestedType(
                    "EffectSprites", BindingFlags.Public | BindingFlags.NonPublic);
                if (effectSprites == null)
                {
                    throw new InvalidOperationException("[EFTEffectManager] StaticIcons.EffectSprites type not found.");
                }

                MethodBase method = effectSprites
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name.EndsWith("OnAfterDeserialize", StringComparison.Ordinal));
                if (method == null)
                {
                    throw new InvalidOperationException(
                        "[EFTEffectManager] EffectSprites.OnAfterDeserialize not found.");
                }

                return method;
            }

            private static void Postfix(EFT.UI.StaticIcons.EffectSprites __instance)
            {
                // Only remember the table because we cant dereference a PPtr while loading.
                IconStore.RememberTable(__instance.EffectIcons);
            }
        }

        [HarmonyPatch(
            typeof(EFT.LocalizationManager),
            nameof(EFT.LocalizationManager.LocalizedValue),
            new Type[] { typeof(string), typeof(string) })]
        public static class LocalizedValuePatch
        {
            private static bool Prefix(string id, ref string __result)
            {
                if (id != null && EffectsManager.DisplayNames.TryGetValue(id, out string displayName))
                {
                    __result = displayName;
                    return false; // skip vanilla lookup for our keys
                }

                return true;
            }
        }
    }
}