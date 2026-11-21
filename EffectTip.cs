using System;
using EFT.HealthSystem;
using UnityEngine;

namespace EFTEffectManager
{
    internal sealed class EffectTip : SimpleBuffDescription
    {
        private readonly IHealthEffect _effect;
        private readonly EffectOptions _options;
        private bool _providerErrorLogged;

        // EffectIcon skips entries whose Text is null/empty so keep placeholder
        internal EffectTip(IHealthEffect effect, EffectOptions options) : base(string.IsNullOrEmpty(options.Description) ? " " : options.Description)
        {
            _effect = effect;
            _options = options;
        }

        public override string NameDisplay
        {
            get
            {
                Func<IHealthEffect, string> provider = _options.DescriptionProvider;
                if (provider == null)
                {
                    return WithColor(_options.Description ?? string.Empty);
                }

                try
                {
                    string text = provider(_effect);

                    return WithColor(string.IsNullOrEmpty(text) ? _options.Description ?? string.Empty : text);
                }
                catch (Exception e)
                {
                    if (!_providerErrorLogged)
                    {
                        _providerErrorLogged = true;
                        EffectsManager.Log.LogError($"EFTEffectManager: description provider for '{_effect?.GetType().Name}' failed: {e}");
                    }

                    return WithColor(_options.Description ?? string.Empty);
                }
            }
        }

        private string WithColor(string text)
        {
            return _options.DescriptionColor.HasValue ? EffectsManager.Colorize(text, _options.DescriptionColor.Value) : text;
        }

        public override int TimeLeft
        {
            get
            {
                try
                {
                    float left = _effect.TimeLeft;
                    if (left <= 0f)
                    {
                        return 0;
                    }

                    if (float.IsInfinity(left) || left >= int.MaxValue)
                    {
                        return int.MaxValue;
                    }

                    return Mathf.RoundToInt(left);
                }
                catch
                {
                    return 0;
                }
            }
        }
    }
}
