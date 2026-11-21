using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.HealthSystem;
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
                Type effectSprites = typeof(EFT.UI.StaticIcons).GetNestedType("EffectSprites", BindingFlags.Public | BindingFlags.NonPublic);
                if (effectSprites == null)
                {
                    throw new InvalidOperationException("[EFTEffectManager] StaticIcons.EffectSprites type not found.");
                }

                MethodBase method = effectSprites.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name.EndsWith("OnAfterDeserialize", StringComparison.Ordinal));
                if (method == null)
                {
                    throw new InvalidOperationException("[EFTEffectManager] EffectSprites.OnAfterDeserialize not found.");
                }

                return method;
            }

            private static void Postfix(EFT.UI.StaticIcons.EffectSprites __instance)
            {
                // Only remember the table because we cant dereference a PPtr while loading.
                IconStore.RememberTable(__instance.EffectIcons);
            }
        }

        /// <summary>
        /// Add the description line to every custom effect tooltip
        /// </summary>
        [HarmonyPatch(typeof(EffectDescription), MethodType.Constructor, new Type[] { typeof(IHealthEffect), typeof(bool), typeof(List<SimpleBuffDescription>) })]
        public static class EffectDescriptionTipPatch
        {
            private static void Postfix(EffectDescription __instance, IHealthEffect effect)
            {
                EffectsManager.AppendDescription(effect, __instance);
            }
        }

        [HarmonyPatch(typeof(EffectDescription), nameof(EffectDescription.Replace), new Type[] { typeof(List<SimpleBuffDescription>) })]
        public static class ReplaceBuffsPatch
        {
            private static void Prefix(EffectDescription __instance, ref List<SimpleBuffDescription> __0)
            {
                EffectTip tip = __instance.Buffs?.OfType<EffectTip>().FirstOrDefault();
                if (tip == null || __0 == null)
                {
                    return;
                }

                // Replace clears the old list before notifying the icon.
                var replacement = new List<SimpleBuffDescription>(__0);

                replacement.RemoveAll(buff => buff is EffectTip);
                replacement.Add(tip);

                __0 = replacement;
            }
        }

        [HarmonyPatch(typeof(BuffDescription), nameof(BuffDescription.NameDisplay), MethodType.Getter)]
        public static class BuffNamePatch
        {
            private static void Postfix(BuffDescription __instance, ref string __result)
            {
                string key = __instance.Text;
                if (key == null || !EffectsManager.TooltipOptions.TryGetValue(key, out EffectOptions options))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(options.DisplayName))
                {
                    __result = options.DisplayName;
                }

                if (options.NameColor.HasValue)
                {
                    __result = EffectsManager.Colorize(__result, options.NameColor.Value);
                }
            }
        }
    }
}
