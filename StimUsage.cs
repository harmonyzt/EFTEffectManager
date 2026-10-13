using System;
using EFT;
using EFT.HealthSystem;

namespace EFTEffectManager
{
    /// <summary>
    /// On stimulator apply
    /// Delivered by <see cref="EffectsManager.StimApplied"/> 
    /// </summary>
    public sealed class StimUsage
    {
        /// <summary>The stim effect that was created or refreshed by this usage.</summary>
        public ActiveHealthController.Stimulator Effect { get; }

        /// <summary>
        /// Buff set key from the game settings - the "kind" of stim (adrenaline, ETG, propital live here as their own keys).
        /// </summary>
        public string BuffsName { get; }

        /// <summary>Item template id of the injector. Empty for non-item sources (ie our skill elite perks I guess?).</summary>
        public string TemplateId { get; }

        /// <summary>Body part the stim was applied to (Head for injectors)</summary>
        public EBodyPart AppliedTo { get; }

        /// <summary>True when this usage came from an actual injector item</summary>
        public bool IsItem => !string.IsNullOrEmpty(TemplateId);

        /// <summary>Buff settings this stim carries, straight from the game settings (null when the buff set is unknown)</summary>
        public EffectsSettings.StimulatorSettings.StimulatorBuffSettings[] Buffs { get; }

        /// <summary>
        /// Buff types carried by this stim (HealthRate, SkillRate, StaminaRat)
        /// Empty when unknown.
        /// </summary>
        public EStimulatorBuffType[] BuffTypes { get; }

        internal StimUsage(ActiveHealthController.Stimulator effect, string buffsName, string templateId,
            EBodyPart appliedTo)
        {
            Effect = effect;
            BuffsName = buffsName ?? string.Empty;
            TemplateId = templateId ?? string.Empty;
            AppliedTo = appliedTo;

            Buffs = EffectsManager.GetStimBuffSettings(BuffsName);

            EStimulatorBuffType[] types = Array.Empty<EStimulatorBuffType>();
            if (Buffs != null && Buffs.Length > 0)
            {
                types = new EStimulatorBuffType[Buffs.Length];
                
                for (int i = 0; i < Buffs.Length; i++)
                {
                    types[i] = Buffs[i].BuffType;
                }
            }

            BuffTypes = types;
        }

        /// <summary>True when the stim carries this buff type (HealthRate, SkillRate)</summary>
        public bool HasBuff(EStimulatorBuffType buffType)
        {
            for (int i = 0; i < BuffTypes.Length; i++)
            {
                if (BuffTypes[i] == buffType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}