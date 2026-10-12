using System;
using System.Collections.Generic;
using EFT;
using EFT.HealthSystem;
using UnityEngine;

namespace EFTEffectManager
{
    public sealed class SpeedPenaltyRegistry
    {
        // Separate from EFT's Weight, Armor, injury and aiming causes.
        internal const Player.ESpeedLimit LimitCause = (Player.ESpeedLimit)0x45465445;

        private sealed class Entry
        {
            public string Name;
            public Func<IHealthController, float?> Provider;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private Entry[] _providers = Array.Empty<Entry>();
        private MovementContext _localContext;
        private float _combinedFactor = 1f;

        public int Count => _entries.Count;
        public float CombinedFactor => _combinedFactor;

        /// <summary>Return a maximum speed fraction, or null. The lowest fraction wins.</summary>
        public void Add(string name, Func<IHealthController, float?> provider)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Penalty name must be non-empty.", nameof(name));
            }

            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }
            
            EffectsManager.Init();
            
            int index = _entries.FindIndex(entry => entry.Name == name);
            var value = new Entry { Name = name, Provider = provider };

            if (index < 0)
            {
                _entries.Add(value);
            }
            else
            {
                _entries[index] = value;
            }
            
            _providers = _entries.ToArray();
            
            EffectsManager.RefreshPenalties(EffectsManager.RaidPlayer);
        }

        public bool Remove(string name)
        {
            int index = _entries.FindIndex(entry => entry.Name == name);
            if (index < 0)
            {
                return false;
            }
            
            _entries.RemoveAt(index);
            _providers = _entries.ToArray();
            
            EffectsManager.RefreshPenalties(EffectsManager.RaidPlayer);
            
            return true;
        }

        internal void Apply(Player player)
        {
            if (!EffectsManager.IsRaidPlayer(player) || player.MovementContext == null)
            {
                return;
            }

            if (_localContext != null && !ReferenceEquals(_localContext, player.MovementContext))
            {
                Reset();
            }

            float factor = 1f;
            foreach (Entry entry in _providers)
            {
                try
                {
                    float? value = entry.Provider(player.HealthController);
                    if (!value.HasValue) continue;
                    if (float.IsNaN(value.Value) || float.IsInfinity(value.Value))
                    {
                        throw new ArgumentException("Speed fraction must be finite.");
                    }
                    
                    factor = Mathf.Min(factor, Mathf.Clamp(value.Value, 0.01f, 1f));
                }
                catch (Exception e)
                {
                    EffectsManager.Log.LogError($"EFTEffectManager: speed provider '{entry.Name}' failed: {e}");
                }
            }
            
            _localContext = player.MovementContext;
            _combinedFactor = factor;
            
            RefreshLimit(_localContext);
        }

        internal void RefreshLimit(MovementContext context)
        {
            if (!ReferenceEquals(context, _localContext) || context == null)
            {
                return;
            }
            
            bool hasLimit = context._speedLimits.TryGetValue(LimitCause, out float oldLimit);
            float limit = context.MaxSpeed * _combinedFactor;

            if (_combinedFactor >= 1f && !hasLimit)
            {
                return;
            }

            if (_combinedFactor < 1f && hasLimit && Mathf.Approximately(limit, oldLimit))
            {
                return;
            }
            
            context.RemoveStateSpeedLimit(LimitCause);
            if (_combinedFactor < 1f)
            {
                context.AddStateSpeedLimit(limit, LimitCause);
            }
            
            // Call directly to unmark speed limit dirty and refresh the limit
            context.method_4();
        }

        internal float FactorFor(MovementContext context) =>
            context != null && ReferenceEquals(_localContext, context) ? _combinedFactor : 1f;

        internal void Reset()
        {
            MovementContext context = _localContext;
            
            _localContext = null;
            _combinedFactor = 1f;

            if (context == null || !context._speedLimits.ContainsKey(LimitCause))
            {
                return;
            }
            
            context.RemoveStateSpeedLimit(LimitCause);
            context.method_4();
        }
    }
}
