using System;
using System.Collections.Generic;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;

namespace EFTEffectManager
{
    /// <summary>
    /// Little helpers around <see cref="IHealthController"/>, the player and other minor stuff.
    /// </summary>
    public static partial class EffectsManager
    {
        /// <summary>
        /// Rolls a percentage chance where 0 = never - 100 always.
        /// </summary>
        public static bool RollChance(int chance)
        {
            if (chance <= 0)
            {
                return false;
            }

            if (chance >= 100)
            {
                return true;
            }

            return UnityEngine.Random.Range(0, 100) < chance;
        }

        /// <summary>
        /// Makes the player speak
        /// True when the roll passed and the speaker actually played something
        /// </summary>
        public static bool MakeCharacterSpeak(int chance = 100, EPhraseTrigger phrase = EPhraseTrigger.OnBeingHurt) =>
            MakeCharacterSpeak(RaidPlayer, chance, phrase);

        /// <summary>Same as <see cref="MakeCharacterSpeak(int, EPhraseTrigger)"/> but for an explicit player (works for bots too)</summary>
        public static bool MakeCharacterSpeak(Player player, int chance = 100,
            EPhraseTrigger phrase = EPhraseTrigger.OnBeingHurt)
        {
            if (player == null || player.Speaker == null || !RollChance(chance))
            {
                return false;
            }

            IHealthController health = player.HealthController;
            if (health == null || !health.IsAlive)
            {
                return false;
            }

            try
            {
                return player.Speaker.Play(phrase, player.HealthStatus, demand: true) != null;
            }
            catch (Exception e)
            {
                Log.LogError($"EFTEffectManager: pain grunt failed: {e}");

                return false;
            }
        }

        /// <summary>
        /// Makes the local player (or <paramref name="player"/>) say any voice line
        /// Rolls <paramref name="chance"/> percent first.
        /// </summary>
        public static bool SayPhrase(EPhraseTrigger phrase, int chance = 100, float delay = 0f, Player player = null)
        {
            Player target = player ?? RaidPlayer;
            if (target == null || target.Speaker == null || phrase == EPhraseTrigger.None || !RollChance(chance))
            {
                return false;
            }

            try
            {
                target.Say(phrase, demand: true, delay: delay);

                return true;
            }
            catch (Exception e)
            {
                Log.LogError($"EFTEffectManager: saying '{phrase}' failed: {e}");

                return false;
            }
        }

        /// <summary>Started instance of <typeparamref name="T"/> on the body part, or null.
        /// <see cref="EBodyPart.Common"/> means "any body part".</summary>
        public static T FindEffect<T>(IHealthController controller, EBodyPart bodyPart = EBodyPart.Common)
            where T : IHealthEffect
        {
            return controller == null ? default : controller.FindActiveEffect<T>(bodyPart);
        }

        /// <summary>True when the effect exists on the body part in Added OR Started state (delayed effects count).
        /// <see cref="EBodyPart.Common"/> means "any body part".</summary>
        public static bool HasEffect<T>(IHealthController controller, EBodyPart bodyPart = EBodyPart.Common)
            where T : IHealthEffect => controller != null && controller.FindExistingEffect<T>(bodyPart) != null;

        /// <summary>True while a light or heavy bleed exists on the body part (any body part by default).</summary>
        public static bool IsBleeding(IHealthController controller, EBodyPart bodyPart = EBodyPart.Common) =>
            HasEffect<IBleeding>(controller, bodyPart);

        /// <summary>True while a fracture is active on the body part (any body part by default).</summary>
        public static bool IsFractured(IHealthController controller, EBodyPart bodyPart = EBodyPart.Common) =>
            HasEffect<IFracture>(controller, bodyPart);

        /// <summary>True while the native Pain effect exists on the body part (any body part by default).</summary>
        public static bool HasPain(IHealthController controller, EBodyPart bodyPart = EBodyPart.Common) =>
            HasEffect<IPain>(controller, bodyPart);

        /// <summary>True while a painkiller effect exists on the body part (any body part by default).</summary>
        public static bool HasPainKiller(IHealthController controller, EBodyPart bodyPart = EBodyPart.Common) =>
            HasEffect<IPainKiller>(controller, bodyPart);

        /// <summary>True when a stimulator currently buffs with the given buff type (HealthRate, SkillRate, ...).</summary>
        public static bool HasStimulatorBuff(IHealthController controller, EStimulatorBuffType buffType) =>
            controller is ActiveHealthController active && active.HasStimulatorBuff(buffType);

        /// <summary>Normalized health of the body part, 0..1. <see cref="EBodyPart.Common"/> = the whole body.</summary>
        public static float GetHealthFraction(IHealthController controller, EBodyPart bodyPart = EBodyPart.Common) =>
            controller == null ? 0f : controller.GetBodyPartHealth(bodyPart).Normalized;

        /// <summary>Normalized energy, 0..1.</summary>
        public static float GetEnergyFraction(IHealthController controller) =>
            controller == null ? 0f : controller.Energy.Normalized;

        /// <summary>Normalized hydration, 0..1.</summary>
        public static float GetHydrationFraction(IHealthController controller) =>
            controller == null ? 0f : controller.Hydration.Normalized;

        /// <summary>True when the body part is blacked out.</summary>
        public static bool IsPartDestroyed(IHealthController controller, EBodyPart bodyPart) =>
            controller != null && controller.IsBodyPartDestroyed(bodyPart);

        /// <summary>
        /// Heals the body part through the EFTs health change. Skips destroyed limbs and limbs with full health.
        /// </summary>
        /// <returns>True when health actually went up.</returns>
        public static bool HealPart(IHealthController controller, EBodyPart bodyPart, float amount)
        {
            if (amount <= 0f || !TryActive(controller, out ActiveHealthController active)
                             || active.IsBodyPartDestroyed(bodyPart))
            {
                return false;
            }

            return Guarded("heal part", () =>
            {
                ValueStruct health = active.GetBodyPartHealth(bodyPart);
                float missing = health.Maximum - health.Current;
                if (missing <= 0f)
                {
                    return false;
                }

                float before = health.Current;
                active.ChangeHealth(bodyPart, amount < missing ? amount : missing,
                    BuildDamageInfo(EDamageType.Medicine, amount));

                return active.GetBodyPartHealth(bodyPart).Current > before;
            });
        }

        /// <summary>
        /// Damages the body part through the EFT pipeline - pain roll, bleed/fracture rolls,
        /// over-damage spread to other limbs, limb destruction and death.
        /// </summary>
        /// <returns>True when the body lost health (or the character died).</returns>
        public static bool DamagePart(IHealthController controller, EBodyPart bodyPart, float amount,
            EDamageType damageType = EDamageType.Undefined)
        {
            if (amount <= 0f || !TryActive(controller, out ActiveHealthController active))
            {
                return false;
            }

            return Guarded("damage part", () =>
            {
                float before = active.GetBodyPartHealth(EBodyPart.Common).Current;
                active.ApplyDamage(bodyPart, amount, BuildDamageInfo(damageType, amount));

                return active.GetBodyPartHealth(EBodyPart.Common).Current < before;
            });
        }

        /// <summary>
        /// Brings a blacked-out limb back the way a surgery kit does.
        /// Head and chest can never be restored this way
        /// </summary>
        /// <param name="healthPenalty">Max HP the limb keeps, 0..1 (0.5 = half HP).</param>
        /// <returns>True when the limb was restored.</returns>
        public static bool RestoreBodyPart(IHealthController controller, EBodyPart bodyPart,
            float healthPenalty = 0.5f) =>
            TryActive(controller, out ActiveHealthController active)
            && Guarded("restore body part", () => active.RestoreBodyPart(bodyPart, healthPenalty));

        /// <summary>
        /// Brings a blacked-out body part back at FULL HP and clears its bleeds. Works on head and chest too.
        /// </summary>
        public static bool FullRestoreBodyPart(IHealthController controller, EBodyPart bodyPart) =>
            TryActive(controller, out ActiveHealthController active)
            && Guarded("full restore body part", () => active.FullRestoreBodyPart(bodyPart));

        /// <summary>
        /// Heals every body part to full and removes blacked-out states
        /// </summary>
        /// <returns>True when the whole body is at full HP afterwards.</returns>
        public static bool RestoreFullHealth(IHealthController controller)
        {
            if (!TryActive(controller, out ActiveHealthController active))
            {
                return false;
            }

            return Guarded("restore full health", () =>
            {
                active.RestoreFullHealth();

                return active.GetBodyPartHealth(EBodyPart.Common).AtMaximum;
            });
        }

        /// <summary>Applies painkiller effect.</summary>
        /// <returns>True when the painkiller effect exists afterwards.</returns>
        public static bool GivePainKiller(IHealthController controller)
        {
            if (!TryActive(controller, out ActiveHealthController active))
            {
                return false;
            }

            return Guarded("give painkiller", () =>
            {
                active.DoPainKiller();

                return HasPainKiller(active);
            });
        }

        /// <summary>
        /// Put the native Pain effect on the body part
        /// </summary>
        /// <returns>True when the pain effect exists on the body part afterwards.</returns>
        public static bool ApplyPain(IHealthController controller, EBodyPart bodyPart, float? duration = null,
            float? residueTime = null, float? strength = null)
        {
            if (!TryActive(controller, out ActiveHealthController active) || !(active.DamageCoeff > 0f))
            {
                return false;
            }

            return Guarded("apply pain", () =>
            {
                active.AddEffect<ActiveHealthController.Pain>(bodyPart, 0f, duration, residueTime, strength);

                return HasPain(active, bodyPart);
            });
        }

        /// <summary>
        /// Makes the body part bleed. Immunity or anything that blocks bleeds is neglected.
        /// Also remove fresh wound if it exists on that body part.
        /// </summary>
        /// <param name="heavy">True for a heavy bleed.</param>
        /// <returns>True when the body part is bleeding afterwards.</returns>
        public static bool ApplyBleed(IHealthController controller, EBodyPart bodyPart, bool heavy = false)
        {
            if (!TryActive(controller, out ActiveHealthController active))
            {
                return false;
            }

            return Guarded("apply bleed", () =>
            {
                active.DoBleed(heavy, bodyPart);

                return IsBleeding(active, bodyPart);
            });
        }

        /// <summary>
        /// Breaks the limb. Only arms and legs can break, blacked out limbs are skipped.
        /// </summary>
        /// <returns>True when the limb is fractured afterwards.</returns>
        public static bool ApplyFracture(IHealthController controller, EBodyPart bodyPart)
        {
            if (!TryActive(controller, out ActiveHealthController active))
            {
                return false;
            }

            return Guarded("apply fracture", () =>
            {
                active.DoFracture(bodyPart);

                return IsFractured(active, bodyPart);
            });
        }

        /// <summary>
        /// Removes every bleed on the body part.
        /// <see cref="EBodyPart.Common"/> = every body part.
        /// </summary>
        /// <returns>True when no bleed is left on the body afterwards.</returns>
        public static bool ClearBleeds(IHealthController controller, EBodyPart bodyPart = EBodyPart.Common)
        {
            if (!TryActive(controller, out ActiveHealthController active))
            {
                return false;
            }

            return Guarded("clear bleeds", () =>
            {
                active.RemoveBleedingEffects(bodyPart);

                return !IsBleeding(active, bodyPart);
            });
        }

        /// <summary>
        /// Removes every non-permanent negative effect on the body part - Fractures, bleeds, pain, custom effects, etc.
        /// Still triggers <see cref="EffectOptions.MedHealTriggers"/>.
        /// <see cref="EBodyPart.Common"/> = every body part.
        /// </summary>
        /// <returns>True when the call ran.</returns>
        public static bool ClearNegativeEffects(IHealthController controller, EBodyPart bodyPart = EBodyPart.Common) =>
            TryActive(controller, out ActiveHealthController active)
            && Guarded("clear negative effects", () =>
            {
                active.RemoveNegativeEffects(bodyPart);

                return true;
            });

        /// <summary>
        /// Adds temporary skill levels to the same pool stimulator buffs write to, e.g.
        /// <c>AddSkillBonus(hc, ESkillId.Strength, 5)</c> until it is removed again.
        /// Negative values take levels away. Stacks with stims and other bonuses.
        /// </summary>
        /// <returns>True when the bonus was applied.</returns>
        public static bool AddSkillBonus(IHealthController controller, ESkillId skill, int points) =>
            ChangeSkillBonus(controller, skill, points);

        /// <summary>
        /// Takes back a bonus added with <see cref="AddSkillBonus"/> - pass the SAME <paramref name="points"/> value you passed there.
        /// Only removes what this API added.
        /// </summary>
        /// <returns>True when something was taken back.</returns>
        public static bool RemoveSkillBonus(IHealthController controller, ESkillId skill, int points)
        {
            if (points == 0)
            {
                return false;
            }

            int take = ClampToBonusPool(points, GetSkillBonus(controller, skill));

            return take != 0 && ChangeSkillBonus(controller, skill, -take);
        }

        /// <summary>Skill levels currently added by this API on the controller (0 = none of ours).</summary>
        public static int GetSkillBonus(IHealthController controller, ESkillId skill)
        {
            if (controller == null
                || !SkillBonusPool.TryGetValue(controller, out Dictionary<ESkillId, int> bonuses))
            {
                return 0;
            }

            bonuses.TryGetValue(skill, out int value);

            return value;
        }

        /// <summary>
        /// Effective skill level exactly like the game counts it (level + every active bonus, stimulator buffs included)
        /// </summary>
        public static int GetSkillSummaryLevel(IHealthController controller, ESkillId skill) =>
            FindSkill(controller, skill)?.SummaryLevel ?? 0;

        private static bool TryActive(IHealthController controller, out ActiveHealthController active)
        {
            active = controller as ActiveHealthController;

            return active != null && active.IsAlive;
        }

        private static bool Guarded(string action, Func<bool> call)
        {
            try
            {
                return call();
            }
            catch (Exception e)
            {
                Log.LogError($"EFTEffectManager: {action} failed: {e}");

                return false;
            }
        }

        private static DamageInfo BuildDamageInfo(EDamageType damageType, float damage) => new DamageInfo
        {
            DamageType = damageType,
            Damage = damage,
        };

        /// <summary>Our skill contribution per controller and skill. Never mixes with other sources (stims)</summary>
        private static readonly Dictionary<IHealthController, Dictionary<ESkillId, int>> SkillBonusPool =
            new Dictionary<IHealthController, Dictionary<ESkillId, int>>();

        /// <summary>What every single effect instance has applied, so it can take exactly that back</summary>
        private static readonly Dictionary<IHealthEffect, EffectSkillBonus> EffectSkillBonuses =
            new Dictionary<IHealthEffect, EffectSkillBonus>();

        private sealed class EffectSkillBonus
        {
            internal ActiveHealthController Controller;
            internal readonly Dictionary<ESkillId, int> Applied = new Dictionary<ESkillId, int>();
        }

        /// <summary>
        /// Applies the <see cref="EffectOptions.SkillBuffs"/> of a registered effect while it is Started
        /// (calling it again only re-applies a changed configuration)
        /// </summary>
        internal static void ApplyEffectSkillBonuses(IHealthEffect effect)
        {
            if (!(effect is ActiveHealthController.Effect instance)
                || instance.State != EEffectState.Started
                || !TryGetRegistration(effect, out EffectRegistration registration)
                || registration.Options.SkillBuffs.Count == 0)
            {
                return;
            }

            ActiveHealthController controller = instance.HealthController;
            if (controller == null || !IsRaidController(controller) || !controller.IsAlive)
            {
                return;
            }

            try
            {
                if (!EffectSkillBonuses.TryGetValue(effect, out EffectSkillBonus ledger))
                {
                    ledger = new EffectSkillBonus { Controller = controller };
                    EffectSkillBonuses[effect] = ledger;
                }

                foreach (KeyValuePair<ESkillId, int> pair in registration.Options.SkillBuffs)
                {
                    if (pair.Value == 0)
                    {
                        continue;
                    }

                    ledger.Applied.TryGetValue(pair.Key, out int current);
                    if (current == pair.Value)
                    {
                        continue;
                    }

                    if (!ChangeSkillBonus(controller, pair.Key, pair.Value - current))
                    {
                        return;
                    }

                    ledger.Applied[pair.Key] = pair.Value;
                }
            }
            catch (Exception e)
            {
                Log.LogError($"EFTEffectManager: skill bonuses for '{effect.GetType().Name}' failed: {e}");
            }
        }

        /// <summary>Takes back everything the effect applied (residue, removal - whatever ends the Started state).</summary>
        internal static void RevertEffectSkillBonuses(IHealthEffect effect)
        {
            if (effect == null || !EffectSkillBonuses.TryGetValue(effect, out EffectSkillBonus ledger))
            {
                return;
            }

            EffectSkillBonuses.Remove(effect);
            foreach (KeyValuePair<ESkillId, int> pair in ledger.Applied)
            {
                if (pair.Value != 0)
                {
                    RemoveSkillBonus(ledger.Controller, pair.Key, pair.Value);
                }
            }
        }

        /// <summary>Reverts every bonus this API added (raid end / controller teardown).</summary>
        internal static void ClearAllSkillBonuses()
        {
            foreach (KeyValuePair<IHealthController, Dictionary<ESkillId, int>> controller in SkillBonusPool)
            {
                foreach (KeyValuePair<ESkillId, int> pair in controller.Value)
                {
                    if (pair.Value == 0)
                    {
                        continue;
                    }

                    Skill target = FindSkill(controller.Key, pair.Key);
                    if (target != null)
                    {
                        target.Buff -= pair.Value;
                    }
                }
            }

            SkillBonusPool.Clear();
            EffectSkillBonuses.Clear();
        }

        private static bool ChangeSkillBonus(IHealthController controller, ESkillId skill, int points)
        {
            if (points == 0 || !TryActive(controller, out ActiveHealthController active))
            {
                return false;
            }

            return Guarded($"skill bonus {skill}", () =>
            {
                Skill target = FindSkill(active, skill);
                if (target == null)
                {
                    Log.LogWarning($"EFTEffectManager: skill '{skill}' does not exist on the player - bonus skipped.");

                    return false;
                }

                target.Buff += points;
                TrackSkillBonus(active, skill, points);

                return true;
            });
        }

        /// <summary>Keeps a removal inside what we actually added (positive pool = we only hold bonuses).</summary>
        private static int ClampToBonusPool(int points, int pool)
        {
            if (points > 0)
            {
                return Math.Min(points, Math.Max(pool, 0));
            }

            return points < 0 ? Math.Max(points, Math.Min(pool, 0)) : 0;
        }

        private static void TrackSkillBonus(IHealthController controller, ESkillId skill, int delta)
        {
            if (!SkillBonusPool.TryGetValue(controller, out Dictionary<ESkillId, int> bonuses))
            {
                bonuses = new Dictionary<ESkillId, int>();
                SkillBonusPool[controller] = bonuses;
            }

            bonuses.TryGetValue(skill, out int current);
            int value = current + delta;
            if (value == 0)
            {
                bonuses.Remove(skill);
            }
            else
            {
                bonuses[skill] = value;
            }

            if (bonuses.Count == 0)
            {
                SkillBonusPool.Remove(controller);
            }
        }

        private static Skill FindSkill(IHealthController controller, ESkillId skill)
        {
            SkillManager manager = (controller as ActiveHealthController)?._skills;
            Skill[] skills = manager?.Skills;
            if (skills == null)
            {
                return null;
            }

            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null && skills[i].Id == skill)
                {
                    return skills[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Buff settings of a stim buff set - the <see cref="StimUsage.BuffsName"/> key from the game
        /// settings, i.e. what a stim of that kind actually carries (HealthRate, SkillRate, PainKiller)
        /// Returns null for empty / unknown keys.
        /// </summary>
        public static EffectsSettings.StimulatorSettings.StimulatorBuffSettings[] GetStimBuffSettings(
            string buffsName)
        {
            if (string.IsNullOrEmpty(buffsName))
            {
                return null;
            }

            try
            {
                Dictionary<string, EffectsSettings.StimulatorSettings.StimulatorBuffSettings[]> buffs =
                    ActiveHealthController.Effect.EffectsSettings?.Stimulator?.Buffs;

                return buffs != null && buffs.TryGetValue(buffsName,
                    out EffectsSettings.StimulatorSettings.StimulatorBuffSettings[] settings)
                    ? settings
                    : null;
            }
            catch (Exception e)
            {
                Log.LogError($"EFTEffectManager: reading stim buff set '{buffsName}' failed: {e}");

                return null;
            }
        }

        /// <summary>
        /// Buff types of a stim buff set (HealthRate, SkillRate).
        /// Empty when unknown.
        /// </summary>
        public static EStimulatorBuffType[] GetStimBuffTypes(string buffsName)
        {
            EffectsSettings.StimulatorSettings.StimulatorBuffSettings[] settings = GetStimBuffSettings(buffsName);
            if (settings == null || settings.Length == 0)
            {
                return Array.Empty<EStimulatorBuffType>();
            }

            var types = new EStimulatorBuffType[settings.Length];
            for (int i = 0; i < settings.Length; i++)
            {
                types[i] = settings[i].BuffType;
            }

            return types;
        }

        public static float GetStamina(Player player) =>
            player?.Physical?.Stamina != null ? player.Physical.Stamina.Current : 0f;

        public static float GetMaxStamina(Player player) =>
            player?.Physical?.Stamina != null ? player.Physical.Stamina.TotalCapacity.Value : 0f;

        /// <summary>Normalized stamina of the body, 0..1.</summary>
        public static float GetStaminaFraction(Player player)
        {
            float max = GetMaxStamina(player);
            if (max <= 0f)
            {
                return 0f;
            }

            float fraction = GetStamina(player) / max;

            return fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
        }

        /// <summary>True while the body is exhausted</summary>
        public static bool IsExhausted(Player player) =>
            player?.Physical?.Stamina != null && player.Physical.Stamina.Exhausted;

        /// <summary>
        /// Moves the stamina by 'amount' clamped between 0 and the max
        /// </summary>
        /// <returns>True when the pool actually changed</returns>
        public static bool AddStamina(Player player, float amount)
        {
            PhysicalBase physical = player?.Physical;
            if (physical?.Stamina == null || amount == 0f)
            {
                return false;
            }

            return Guarded("add stamina", () =>
            {
                float current = physical.Stamina.Current;
                float max = physical.Stamina.TotalCapacity.Value;
                float next = current + amount;
                if (next > max)
                {
                    next = max;
                }
                else if (next < 0f)
                {
                    next = 0f;
                }

                if (next == current)
                {
                    return false;
                }

                physical.Stamina.Current = next;

                return true;
            });
        }

        public static float GetStaminaRestoreRate(Player player) =>
            player?.Physical != null ? player.Physical.StaminaRestoreRate : 0f;

        /// <summary>
        /// Writes a new stamina restore rate and returns the previous one, so you can revert it later on
        /// </summary>
        /// <returns>The rate that was in effect before the call (0 when the player is unavailable)</returns>
        public static float SetStaminaRestoreRate(Player player, float rate)
        {
            PhysicalBase physical = player?.Physical;
            if (physical == null)
            {
                return 0f;
            }

            float previous = physical.StaminaRestoreRate;
            if (previous != rate)
            {
                physical.StaminaRestoreRate = rate;
            }

            return previous;
        }

        /// <summary>
        /// Shifts the stamina restore rate and returns the previous value to take the boost back when you want
        /// </summary>
        public static float AddStaminaRestoreRate(Player player, float delta)
        {
            PhysicalBase physical = player?.Physical;
            if (physical == null || delta == 0f)
            {
                return 0f;
            }

            float previous = physical.StaminaRestoreRate;
            physical.StaminaRestoreRate = previous + delta;

            return previous;
        }
    }
}