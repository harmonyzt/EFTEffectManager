using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using EFT;
using HarmonyLib;

namespace EFTEffectManager.Patches
{
    // Native walk cap, separate sprint limit, ergonomics and sprint HUD input
    [HarmonyPatch]
    internal static class PenaltyPatches
    {
        [HarmonyPatch(typeof(MovementContext), nameof(MovementContext.OnStrengthSkillLevelChanged))]
        [HarmonyPostfix]
        private static void StrengthChanged_Postfix(MovementContext __instance)
        {
            try
            {
                EffectsManager.MovementSpeedPenalties.RefreshLimit(__instance);
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError($"EFTEffectManager: strength limit refresh failed: {e}");
            }
        }

        // EFT applies ordinary state limits to walking; sprint reads this separate limit
        [HarmonyPatch(typeof(MovementContext), nameof(MovementContext.StateSprintSpeedLimit), MethodType.Getter)]
        [HarmonyPostfix]
        private static void StateSprintSpeedLimit_Postfix(MovementContext __instance, ref float __result)
        {
            try
            {
                __result *= EffectsManager.MovementSpeedPenalties.FactorFor(__instance);
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError($"EFTEffectManager: sprint limit penalty failed: {e}");
            }
        }

        // The getter is what weapon code and EFTs own rebuilds read - add our sum on top,
        // never write into EFTs _ergonomicsPenalty field.
        [HarmonyPatch(typeof(Player), nameof(Player.ErgonomicsPenalty), MethodType.Getter)]
        [HarmonyPostfix]
        private static void ErgonomicsPenalty_Postfix(Player __instance, ref float __result)
        {
            try
            {
                __result += EffectsManager.ErgonomicsPenalties.TotalFor(__instance);
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError($"EFTEffectManager: ergonomics penalty failed: {e}");
            }
        }

        // Sprint reports raw input to the HUD. Keep it within the native walk cap.
        [HarmonyPatch(typeof(Player), nameof(Player.RaiseChangeSpeedEvent))]
        private static class RaiseChangeSpeedEventPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                System.Reflection.MethodBase getter = AccessTools.PropertyGetter(typeof(MovementContext),
                    nameof(MovementContext.CharacterMovementSpeed));
                System.Reflection.MethodInfo replacement =
                    AccessTools.Method(typeof(RaiseChangeSpeedEventPatch), nameof(ScaleMeterSpeed));

                int matches = 0;
                foreach (CodeInstruction instruction in codes)
                {
                    if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                        && Equals(instruction.operand, getter))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = replacement;
                        matches++;
                    }
                }

                if (matches != 1)
                {
                    throw new InvalidOperationException(
                        $"EFTEffectManager: expected exactly one CharacterMovementSpeed read in " +
                        $"{nameof(Player.RaiseChangeSpeedEvent)}, found {matches}.");
                }

                return codes;
            }

            private static float ScaleMeterSpeed(MovementContext context)
            {
                return EffectsManager.MovementSpeedPenalties.FactorFor(context) < 1f
                    ? UnityEngine.Mathf.Min(context.CharacterMovementSpeed, context.StateSpeedLimit)
                    : context.CharacterMovementSpeed;
            }
        }
    }
}