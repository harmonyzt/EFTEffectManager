using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace EFTEffectManager
{
    /// <summary>
    /// Icon storage for registered effects
    /// </summary>
    public static class IconStore
    {
        private static readonly Dictionary<Type, Func<Sprite>> Pending = new Dictionary<Type, Func<Sprite>>();

        /// <summary>Table captured from EffectSprites.OnAfterDeserialize.</summary>
        private static Dictionary<Type, Sprite> _table;

        private static FieldInfo _hardSettingsInstanceField;
        private static bool _hardSettingsFieldResolved;

        /// <summary>Number of icon registrations still waiting for the game icon table.</summary>
        public static int PendingCount => Pending.Count;

        internal static void Enqueue(Type interfaceType, EffectOptions options, Assembly hostAssembly)
        {
            Func<Sprite> factory;
            if (options.IconSprite != null)
            {
                Sprite sprite = options.IconSprite;
                factory = () => sprite;
            }
            else if (!string.IsNullOrEmpty(options.IconFilePath))
            {
                string filePath = options.IconFilePath;
                factory = () => LoadSpriteFromFile(filePath, hostAssembly);
            }
            else
            {
                // Effects without an icon still work, but vanilla health UI hides them.
                return;
            }

            Pending[interfaceType] = factory;
            Flush();
        }

        /// <summary>Manually map an effect interface to an icon sprite.</summary>
        public static void RegisterIcon(Type interfaceType, Sprite sprite)
        {
            if (interfaceType == null || sprite == null)
            {
                return;
            }

            Pending[interfaceType] = () => sprite;
            Flush();
        }

        /// <summary>
        /// Remembers the icon table from the patched EffectSprites.OnAfterDeserialize
        /// </summary>
        internal static void RememberTable(Dictionary<Type, Sprite> table)
        {
            if (table != null)
            {
                _table = table;
            }
        }

        /// <summary>
        /// Moves queued icons into the vanilla table (idempotent, never throws).
        /// Only call OUTSIDE Unity deserialization
        /// </summary>
        public static void Flush()
        {
            if (Pending.Count == 0)
            {
                return;
            }

            try
            {
                Dictionary<Type, Sprite> table = TryGetTable();
                if (table == null)
                {
                    return;
                }

                List<Type> completed = new List<Type>();
                foreach (KeyValuePair<Type, Func<Sprite>> pair in Pending)
                {
                    Sprite sprite = null;
                    try
                    {
                        sprite = pair.Value();
                    }
                    catch (Exception e)
                    {
                        EffectsManager.Log.LogError($"EFTEffectManager: icon factory for {pair.Key.Name} failed: {e}");
                    }

                    if (sprite == null)
                    {
                        // A failed file does not need to be retried every event.
                        EffectsManager.Log.LogWarning(
                            $"EFTEffectManager: no usable icon for {pair.Key.Name} - it will not be shown in the health UI.");
                        completed.Add(pair.Key);
                        continue;
                    }

                    table[pair.Key] = sprite;
                    completed.Add(pair.Key);
                    EffectsManager.Log.LogInfo($"EFTEffectManager: icon registered for {pair.Key.Name}.");
                }

                foreach (Type type in completed)
                {
                    Pending.Remove(type);
                }
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError($"EFTEffectManager: icon flush failed: {e}");
            }
        }

        /// <summary>
        /// Resolves the icon table WITHOUT touching the loading EFTHardSettings.Instance property
        /// Falls back to the table captured from EffectSprites.OnAfterDeserialize
        /// </summary>
        private static Dictionary<Type, Sprite> TryGetTable()
        {
            try
            {
                if (!_hardSettingsFieldResolved)
                {
                    _hardSettingsInstanceField = typeof(EFTHardSettings)
                        .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
                    _hardSettingsFieldResolved = true;
                }

                EFTHardSettings loaded = _hardSettingsInstanceField?.GetValue(null) as EFTHardSettings;
                if (loaded != null && loaded.StaticIcons != null && loaded.StaticIcons.EffectIcons != null)
                {
                    Dictionary<Type, Sprite> table = loaded.StaticIcons.EffectIcons.EffectIcons;
                    if (table != null)
                    {
                        _table = table;
                        return _table;
                    }
                }
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogWarning($"EFTEffectManager: icon table probe failed: {e.Message}");
            }

            return _table;
        }

        /// <summary>
        /// Loads a PNG into a Sprite
        /// </summary>
        public static Sprite LoadSpriteFromFile(string path, Assembly hostAssembly = null)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                if (!Path.IsPathRooted(path))
                {
                    Assembly baseAssembly = hostAssembly ?? typeof(IconStore).Assembly;
                    string baseDirectory = Path.GetDirectoryName(baseAssembly.Location);
                    path = Path.Combine(baseDirectory ?? string.Empty, path);
                }

                if (!File.Exists(path))
                {
                    EffectsManager.Log.LogWarning($"EFTEffectManager: icon file not found: {path}");
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(bytes))
                {
                    EffectsManager.Log.LogInfo($"EFTEffectManager: loaded icon from '{path}'.");
                    return Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0u,
                        SpriteMeshType.FullRect);
                }

                EffectsManager.Log.LogWarning($"EFTEffectManager: could not decode image '{path}'.");
                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError($"EFTEffectManager: error loading icon '{path}': {e}");
            }

            return null;
        }
    }
}