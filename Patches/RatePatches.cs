using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using EFT.HealthSystem;
using HarmonyLib;

namespace EFTEffectManager.Patches
{
    [HarmonyPatch(typeof(ActiveHealthController.Effect), nameof(ActiveHealthController.Effect.ManualUpdate))]
    internal static class RatePatches
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var original = AccessTools.Method(typeof(ActiveHealthController.Effect),
                nameof(ActiveHealthController.Effect.RegularUpdate));
            var replacement = AccessTools.Method(typeof(RatePatches), nameof(Update));
            int matches = 0;

            foreach (var code in codes)
            {
                if (!code.Calls(original))
                {
                    continue;
                }

                code.opcode = OpCodes.Call;
                code.operand = replacement;

                matches++;
            }

            if (matches != 1)
            {
                throw new InvalidOperationException($"Expected one Effect.RegularUpdate call, found {matches}.");
            }

            return codes;
        }

        // Use EFT and run before the final Started -> Residued transition.
        // This also covers plugin overrides without patching their assemblies
        private static void Update(ActiveHealthController.Effect effect, float deltaTime)
        {
            effect.RegularUpdate(deltaTime);
            if (EffectsManager.TryGetRegistration(effect, out EffectRegistration registration))
            {
                EffectRateHelper.Apply(effect, registration.Options);
            }

            EffectRateHelper.Tick(effect, deltaTime);
        }
    }
}