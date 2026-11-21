using BepInEx;

namespace EFTEffectManager
{
    [BepInPlugin(Guid, "EFTEffectManager", Version)]
    public sealed class EFTEffectManagerPlugin : BaseUnityPlugin
    {
        internal const string Guid = "com.eft.effectmanager";
        internal const string Version = "1.1.0";

        private void Awake()
        {
            EffectsManager.Init();
        }
    }
}
