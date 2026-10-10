using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using EFT;
using EFT.HealthSystem;
using EFTEffectManager.Patches;
using HarmonyLib;

namespace EFTEffectManager
{
    public static class EffectsManager
    {
        internal const string HarmonyId = "com.eft.effectmanager.core";
        internal static readonly ManualLogSource Log = Logger.CreateLogSource("EFTEffectManager");
        private static readonly object InitLock = new object();
        private static bool _patched;
        private static float? _lastErgonomicsPenalty;
        private static GameWorld _raidWorld;

        public static readonly Dictionary<Type, EffectRegistration> Registrations =
            new Dictionary<Type, EffectRegistration>();

        internal static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>();

        public static SpeedPenaltyRegistry MovementSpeedPenalties { get; } = new SpeedPenaltyRegistry();
        public static ErgonomicsPenaltyRegistry ErgonomicsPenalties { get; } = new ErgonomicsPenaltyRegistry();
        public static Player RaidPlayer { get; private set; }
        public static bool DiagnosticsEnabled { get; set; }

        public static event Action<Player, IHealthEffect> LocalEffectAdded;
        public static event Action<Player, IHealthEffect> LocalEffectStarted;
        public static event Action<Player, IHealthEffect> LocalEffectUpdated;
        public static event Action<Player, IHealthEffect> LocalEffectResidued;
        public static event Action<Player, IHealthEffect> LocalEffectRemoved;
        public static event Action<Player, EBodyPart, EDamageType> LocalBodyPartDestroyed;
        public static event Action<ActiveHealthController, IHealthEffect> MedCuredEffect;
        public static event Action<ActiveHealthController, IHealthEffect> StimCuredEffect;
        public static event Action<ActiveHealthController, IHealthEffect> StimDebuffedEffect;

        /// <summary>Accepted native stim activation/deactivation. One-shot entries can have Active=false.</summary>
        public static event Action<ActiveHealthController, ActiveHealthController.Stimulator.Buff, bool> StimBuffChanged;

        /// <summary>Install shared patches once</summary>
        public static void Init()
        {
            lock (InitLock)
            {
                if (_patched) return;
                var harmony = new Harmony(HarmonyId);
                try
                {
                    harmony.PatchAll(typeof(EffectsManager).Assembly);
                    _patched = true;
                    Log.LogInfo("EFTEffectManager: shared patches installed.");
                }
                catch
                {
                    try
                    {
                        harmony.UnpatchSelf();
                    }
                    catch (Exception e)
                    {
                        Log.LogError($"EFTEffectManager: partial patch cleanup failed: {e}");
                    }

                    throw;
                }
            }
        }

        public static EffectRegistration RegisterEffect<T>(EffectOptions options = null)
            where T : ActiveHealthController.Effect, new() => RegisterEffect<T>(null, options);

        public static EffectRegistration RegisterEffect<T>(Assembly pluginAssembly, EffectOptions options = null)
            where T : ActiveHealthController.Effect, new()
        {
            // Call it in case if custom effect plugins didn't apply patches
            Init();
            
            Type effectType = typeof(T);
            if (Registrations.TryGetValue(effectType, out EffectRegistration existing))
            {
                return existing;
            }
            
            options = options ?? new EffectOptions();
            options.Validate();
            if (pluginAssembly != null && options.IconAssembly == null)
            {
                options.IconAssembly = pluginAssembly;
            }

            Type iface = HealthHelper.ExtractEffectInterface(new T());
            if (iface == typeof(IHealthEffect) || HealthHelper._effectNames.ContainsKey(iface))
            {
                throw new ArgumentException($"'{effectType.Name}' needs its own unused IHealthEffect marker interface.");
            }

            if (HealthHelper.EffectTypeCode._typeToByte.ContainsKey(effectType.Name)
                || HealthHelper.EffectActivator<ActiveHealthController>._constructors.ContainsKey(effectType.Name)
                || HealthHelper.EffectActivator<OfflineHealthController>._constructors.ContainsKey(effectType.Name)
                || HealthHelper.EffectActivator<NetworkHealthController>._constructors.ContainsKey(effectType.Name))
            {
                throw new ArgumentException($"Effect name '{effectType.Name}' is already registered. Use a unique class name.");
            }

            Func<object> offline = Counterpart(options.OfflineEffectType, typeof(OfflineHealthController.Effect),
                effectType, iface);
            Func<object> network = Counterpart(options.NetworkEffectType, typeof(NetworkHealthController.Effect),
                effectType, iface);
            
            if (options.PersistAcrossRaids && typeof(IRestorable).IsAssignableFrom(effectType) && offline == null)
            {
                throw new ArgumentException($"Persisting '{effectType.Name}' requires an OfflineEffectType with the same class name and marker interface.");
            }

            if (options.PersistAcrossRaids && typeof(IRestorable).IsAssignableFrom(effectType)
                                           && !typeof(IRestorable).IsAssignableFrom(options.OfflineEffectType))
            {
                throw new ArgumentException($"Offline counterpart '{options.OfflineEffectType.Name}' must implement IRestorable to preserve the effect in the profile.");
            }

            if (options.PersistAcrossRaids && !typeof(IRestorable).IsAssignableFrom(effectType))
            {
                Log.LogWarning($"EFTEffectManager: '{effectType.Name}' does not implement IRestorable and will not be saved.");
            }

            int code = -1;
            for (int i = byte.MaxValue; i >= 0; i--)
                if (!HealthHelper.EffectTypeCode._byteToType.ContainsKey((byte)i))
                {
                    code = i;
                    break;
                }

            // GG. Should never happen, for now.
            // If it does happen at some point, might as well say fuck it.
            if (code < 0)
            {
                throw new InvalidOperationException("No free health-effect type codes remain.");
            }

            var registration = new EffectRegistration(effectType, iface, options,
                (controller, part) => controller.AddEffect<T>(part, options.DelayTime, options.WorkTime, options.ResidueTime, options.Strength),
                (controller, part) => controller.FindExistingEffect<T>(part)?.ForceRemove(),
                (controller, part) => controller.FindActiveEffect<T>(part),
                (controller, part) => controller.FindExistingEffect<T>(part));

            HealthHelper.EffectTypeCode._typeToByte.Add(effectType.Name, (byte)code);
            HealthHelper.EffectTypeCode._byteToType.Add((byte)code, effectType.Name);
            HealthHelper.EffectActivator<ActiveHealthController>._constructors.Add(effectType.Name, () => new T());
            if (offline != null)
            {
                HealthHelper.EffectActivator<OfflineHealthController>._constructors.Add(effectType.Name, offline);
            }

            if (network != null)
            {
                HealthHelper.EffectActivator<NetworkHealthController>._constructors.Add(effectType.Name, network);
            }
            
            HealthHelper._effectNames.Add(iface, effectType.Name);
            if (!string.IsNullOrEmpty(options.DisplayName))
            {
                DisplayNames.Add(effectType.Name, options.DisplayName);
            }
            
            Registrations.Add(effectType, registration);
            
            // Boot our icon store in here after unity deserialization
            IconStore.Enqueue(iface, options, options.IconAssembly ?? effectType.Assembly);
            Log.LogInfo($"EFTEffectManager: registered '{effectType.Name}' (code {code}).");
            return registration;
        }

        private static Func<object> Counterpart(Type type, Type expectedBase, Type activeType, Type marker)
        {
            if (type == null) return null;
            if (type.IsAbstract || !expectedBase.IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null
                || type.Name != activeType.Name || !marker.IsAssignableFrom(type)
                || HealthHelper.ExtractEffectInterface((IHealthEffect)Activator.CreateInstance(type)) != marker)
            {
                throw new ArgumentException($"Counterpart '{type.FullName}' must derive {expectedBase.FullName}, have a public empty constructor, and use '{activeType.Name}' and its marker interface.");
            }
            
            return () => Activator.CreateInstance(type);
        }

        public static List<EffectRegistration> RegisterEffectsFrom(Assembly assembly,
            Func<Type, EffectOptions> optionsFactory = null)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }
            
            var result = new List<EffectRegistration>();
            MethodInfo method = typeof(EffectsManager).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(candidate => candidate.Name == nameof(RegisterEffect) && candidate.GetParameters().Length == 2);
            
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.ContainsGenericParameters
                                    || !typeof(ActiveHealthController.Effect).IsAssignableFrom(type) ||
                                    type.GetConstructor(Type.EmptyTypes) == null) continue;
                try
                {
                    result.Add((EffectRegistration)method.MakeGenericMethod(type).Invoke(null, new object[] { assembly, optionsFactory?.Invoke(type) ?? new EffectOptions() }));
                }
                catch (Exception e)
                {
                    Log.LogError($"EFTEffectManager: registration of '{type.FullName}' failed: {e}");
                }
            }

            return result;
        }

        internal static void NotifyRaidStarted(GameWorld world)
        {
            try
            {
                NotifyRaidEnded();
                Player player = world?.MainPlayer;
                if (!IsLocalPlayer(player) || player is HideoutPlayer
                                           || string.Equals(player.Location, "hideout",
                                               StringComparison.OrdinalIgnoreCase)
                                           || !(player.HealthController is PlayerHealthController))
                {
                    return;
                }
                
                RaidPlayer = player;
                _raidWorld = world;
                PlayerLifecyclePatch.Attach(player);
                IconStore.Flush();

                foreach (IHealthEffect effect in player.HealthController.GetAllEffects().ToArray())
                {
                    NotifyEffectStrengthChanged(effect);
                }

                RefreshPenalties(player);
                //Log.LogInfo("EFTEffectManager: raid session started.");
            }
            catch (Exception e)
            {
                NotifyRaidEnded();
                Log.LogError($"EFTEffectManager: raid start failed: {e}");
            }
        }

        internal static void NotifyRaidEnded(GameWorld world = null)
        {
            if (!ReferenceEquals(world, null) && !ReferenceEquals(world, _raidWorld)) return;
            PlayerLifecyclePatch.Detach();
            try
            {
                MovementSpeedPenalties.Reset();
            }
            catch (Exception e)
            {
                Log.LogError($"EFTEffectManager: speed cleanup failed: {e}");
            }
            finally
            {
                RaidPlayer = null;
                _raidWorld = null;
                _lastErgonomicsPenalty = null;
            }
        }

        internal static void NotifyLocalEffectAdded(Player player, IHealthEffect effect)
        {
            if (!IsRaidPlayer(player) || effect == null)
            {
                return;
            }
            
            Trace($"added {effect.GetType().Name} on {effect.BodyPart}");
            Raise(LocalEffectAdded, player, effect);
        }

        internal static void NotifyLocalEffectStarted(Player player, IHealthEffect effect)
        {
            if (!IsRaidPlayer(player) || effect == null) return;
            try
            {
                var controller = (ActiveHealthController)player.HealthController;
                foreach (EffectRegistration registration in Registrations.Values.ToArray())
                {
                    if (!registration.IsInvalidating(effect))
                    {
                        continue;
                    }
                    
                    registration.Remove(controller, effect.BodyPart);
                    
                    Trace($"{effect.GetType().Name} invalidated {registration.EffectType.Name} on {effect.BodyPart}");
                }

                NotifyEffectStrengthChanged(effect);
                IconStore.Flush();
                
                Trace($"started {effect.GetType().Name} on {effect.BodyPart}");
                Raise(LocalEffectStarted, player, effect);
            }
            catch (Exception e)
            {
                Log.LogError($"EFTEffectManager: start handling failed: {e}");
            }
            finally
            {
                RefreshPenalties(player);
            }
        }

        internal static void NotifyLocalEffectUpdated(Player player, IHealthEffect effect)
        {
            if (!IsRaidPlayer(player) || effect == null)
            {
                return;
            }
            
            NotifyEffectStrengthChanged(effect);
            
            Raise(LocalEffectUpdated, player, effect);
            RefreshPenalties(player);
        }

        internal static void NotifyLocalEffectResidued(Player player, IHealthEffect effect)
        {
            if (!IsRaidPlayer(player) || effect == null)
            {
                return;
            }
            
            Trace($"residued {effect.GetType().Name} on {effect.BodyPart}");
            Raise(LocalEffectResidued, player, effect);
            
            RefreshPenalties(player);
        }

        internal static void NotifyLocalEffectRemoved(Player player, IHealthEffect effect)
        {
            if (!IsRaidPlayer(player) || effect == null)
            {
                return;
            }
            
            Trace($"removed {effect.GetType().Name} on {effect.BodyPart}");
            Raise(LocalEffectRemoved, player, effect);
            RefreshPenalties(player);
        }

        internal static void NotifyLocalBodyPartDestroyed(Player player, EBodyPart part, EDamageType cause)
        {
            if (!IsRaidPlayer(player)) return;
            foreach (EffectRegistration registration in Registrations.Values.ToArray())
            {
                if (registration.Options.RemoveOnBodyPartDestroyed)
                {
                    registration.Remove(player.HealthController, part);
                }
            }
            
            Trace($"destroyed {part}, cause {cause}");
            Raise(LocalBodyPartDestroyed, player, part, cause);
            RefreshPenalties(player);
        }

        internal static void NotifyMedCured(ActiveHealthController controller, IHealthEffect effect) => NotifyCure(controller, effect, false);

        internal static void NotifyStimHeal(ActiveHealthController controller, IHealthEffect effect) => NotifyCure(controller, effect, true);

        private static void NotifyCure(ActiveHealthController controller, IHealthEffect effect, bool stim)
        {
            if (!IsRaidController(controller) || !controller.IsAlive || effect == null) return;
            foreach (EffectRegistration registration in Registrations.Values.ToArray())
            {
                if (registration.IsMedHealTrigger(effect) || (stim && registration.IsStimHealTrigger(effect)))
                {
                    registration.Add(controller, effect.BodyPart);
                }
            }
            
            Trace($"{(stim ? "stim" : "med")} cured {effect.GetType().Name} on {effect.BodyPart}");
            Raise(MedCuredEffect, controller, effect);

            if (stim)
            {
                Raise(StimCuredEffect, controller, effect);
            }
        }

        internal static void NotifyStimDebuff(ActiveHealthController controller, IHealthEffect effect)
        {
            if (!IsRaidController(controller) || !controller.IsAlive || effect == null) return;
            foreach (EffectRegistration registration in Registrations.Values.ToArray())
                if (registration.IsStimDebuffTrigger(effect))
                {
                    registration.Add(controller, effect.BodyPart);
                }
            
            Trace($"stim applied/refreshed {effect.GetType().Name} on {effect.BodyPart}");
            Raise(StimDebuffedEffect, controller, effect);
        }

        internal static void NotifyStimBuff(ActiveHealthController controller,
            ActiveHealthController.Stimulator.Buff buff, bool activate)
        {
            if (!IsRaidController(controller) || buff == null) return;
            if (activate && buff.Active && controller.IsAlive)
                foreach (EffectRegistration registration in Registrations.Values.ToArray())
                    if (registration.IsStimBuffTrigger(buff.Settings.BuffType))
                    {
                        registration.Add(controller, EBodyPart.Common);
                    }
            
            Trace($"stim {buff.Settings.BuffType}, activate={activate}, active={buff.Active}");
            Raise(StimBuffChanged, controller, buff, activate);
        }

        internal static void NotifyEffectStrengthChanged(IHealthEffect effect)
        {
            if (effect is ActiveHealthController.Effect instance && TryGetRegistration(effect, out EffectRegistration registration))
            {
                EffectRateHelper.Apply(instance, registration.Options);
            }
        }

        public static void RefreshPenalties(Player player)
        {
            if (!IsRaidPlayer(player)) return;
            try
            {
                MovementSpeedPenalties.Apply(player);
                float penalty = ErgonomicsPenalties.TotalFor(player);
                if (_lastErgonomicsPenalty != penalty)
                {
                    (player.HandsController as Player.FirearmController)?.RecalculateErgonomic();
                    player.ProceduralWeaponAnimation?.UpdateWeaponVariables();
                    _lastErgonomicsPenalty = penalty;
                }
            }
            catch (Exception e)
            {
                Log.LogError($"EFTEffectManager: penalty refresh failed: {e}");
            }
        }

        public static bool IsLocalPlayer(Player player) => player != null && player.IsYourPlayer;
        public static bool IsLocalController(IHealthController controller) => controller is PlayerHealthController;
        public static bool IsRaidPlayer(Player player) => player != null && ReferenceEquals(player, RaidPlayer);

        public static bool IsRaidController(IHealthController controller) => controller != null && RaidPlayer != null
            && ReferenceEquals(controller, RaidPlayer.HealthController);

        internal static bool TryGetRegistration(IHealthEffect effect, out EffectRegistration registration)
        {
            registration = null;
            return effect != null && Registrations.TryGetValue(effect.GetType(), out registration);
        }

        internal static void Trace(string message)
        {
            if (DiagnosticsEnabled) Log.LogInfo("EFTEffectManager: " + message);
        }

        private static void Raise<T1, T2>(Action<T1, T2> handlers, T1 a, T2 b)
        {
            if (handlers == null) return;
            foreach (Action<T1, T2> handler in handlers.GetInvocationList())
                try
                {
                    handler(a, b);
                }
                catch (Exception e)
                {
                    Log.LogError($"EFTEffectManager: subscriber '{handler.Method.Name}' failed: {e}");
                }
        }

        private static void Raise<T1, T2, T3>(Action<T1, T2, T3> handlers, T1 a, T2 b, T3 c)
        {
            if (handlers == null) return;
            foreach (Action<T1, T2, T3> handler in handlers.GetInvocationList())
                try
                {
                    handler(a, b, c);
                }
                catch (Exception e)
                {
                    Log.LogError($"EFTEffectManager: subscriber '{handler.Method.Name}' failed: {e}");
                }
        }
    }
}