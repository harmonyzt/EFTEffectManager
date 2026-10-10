using EFT;
using EFT.HealthSystem;

namespace EFTEffectManager.Patches
{
    internal static class PlayerLifecyclePatch
    {
        private static Player _player;
        private static ActiveHealthController _controller;

        internal static void Attach(Player player)
        {
            Detach();
            _player = player;
            _controller = player?.HealthController as ActiveHealthController;
            if (_controller == null) return;

            _controller.EffectAddedEvent += Added;
            _controller.EffectStartedEvent += Started;
            _controller.EffectUpdatedEvent += Updated;
            _controller.EffectResidualEvent += Residued;
            _controller.EffectRemovedEvent += Removed;
            _controller.BodyPartDestroyedEvent += Destroyed;
        }

        internal static void Detach()
        {
            if (_controller != null)
            {
                _controller.EffectAddedEvent -= Added;
                _controller.EffectStartedEvent -= Started;
                _controller.EffectUpdatedEvent -= Updated;
                _controller.EffectResidualEvent -= Residued;
                _controller.EffectRemovedEvent -= Removed;
                _controller.BodyPartDestroyedEvent -= Destroyed;
            }

            _controller = null;
            _player = null;
        }

        private static void Added(IHealthEffect effect) => EffectsManager.NotifyLocalEffectAdded(_player, effect);
        private static void Started(IHealthEffect effect) => EffectsManager.NotifyLocalEffectStarted(_player, effect);
        private static void Updated(IHealthEffect effect) => EffectsManager.NotifyLocalEffectUpdated(_player, effect);
        private static void Residued(IHealthEffect effect) => EffectsManager.NotifyLocalEffectResidued(_player, effect);
        private static void Removed(IHealthEffect effect) => EffectsManager.NotifyLocalEffectRemoved(_player, effect);

        private static void Destroyed(EBodyPart part, EDamageType cause) =>
            EffectsManager.NotifyLocalBodyPartDestroyed(_player, part, cause);
    }
}