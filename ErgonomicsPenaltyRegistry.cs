using System;
using System.Collections.Generic;
using EFT;
using EFT.HealthSystem;

namespace EFTEffectManager
{
    /// <summary>
    /// Weapon ergonomics penalties. Each provider returns the fraction subtracted from weapon ergonomics (&lt;= 0).
    /// The sum is read live by the Player.ErgonomicsPenalty getter postfix - the value is never
    /// written into EFTs own fields, so vanilla rebuilds cannot wipe it.
    /// </summary>
    public sealed class ErgonomicsPenaltyRegistry
    {
        internal sealed class Entry
        {
            public string Name;
            public Func<IHealthController, float> Provider;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private Entry[] _providers = Array.Empty<Entry>();

        public int Count => _entries.Count;

        /// <summary>Registers (or replaces) a named ergonomics penalty provider. Call during plugin startup.</summary>
        public void Add(string name, Func<IHealthController, float> provider)
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
            if (index < 0) _entries.Add(value);
            else _entries[index] = value;
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

        /// <summary>Sum of every provider for the local raid player (0 = untouched).</summary>
        internal float TotalFor(Player player)
        {
            if (_entries.Count == 0
                || EffectsManager.RaidPlayer == null
                || !ReferenceEquals(EffectsManager.RaidPlayer, player))
            {
                return 0f;
            }

            float sum = 0f;
            foreach (Entry entry in _providers)
            {
                try
                {
                    float value = entry.Provider(player.HealthController);
                    if (float.IsNaN(value) || float.IsInfinity(value) || value > 0f)
                    {
                        throw new ArgumentException("Ergonomics penalty must be a finite and non-positive fraction.");
                    }

                    sum = Math.Max(-1f, sum + value);
                }
                catch (Exception e)
                {
                    EffectsManager.Log.LogError($"EFTEffectManager: ergonomics penalty '{entry.Name}' failed: {e}");
                }
            }

            return sum;
        }
    }
}