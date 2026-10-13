using System;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace EFTEffectManager.Patches
{
    // SonicInfo.Camera is the raid camera, so everything below is measured from the listener.
    // Occluded shots never reach the player, so they are not a whizz and are dropped.
    internal static class BulletWhizzPatch
    {
        private static bool _attached;

        internal static void Attach()
        {
            if (_attached) return;

            BulletSoundsUtils.OnShoot += Shoot;
            _attached = true;
        }

        internal static void Detach()
        {
            if (!_attached) return;

            BulletSoundsUtils.OnShoot -= Shoot;
            _attached = false;
        }

        private static void Shoot(SonicBulletSoundPlayer.SonicInfo info)
        {
            try
            {
                if (EffectsManager.RaidPlayer == null || info == null || info.IsOccluded)
                {
                    return;
                }

                Camera camera = info.Camera != null ? info.Camera : Camera.main;
                if (camera == null)
                {
                    return;
                }

                Vector3 listener = camera.transform.position;
                Vector3 from = info.ShotPosition;
                Vector3 direction = info.ShotDirection;
                float sqrMagnitude = direction.sqrMagnitude;

                // closest approach of the shot's infinite line to the listener, never behind the muzzle
                float t = sqrMagnitude < 0.000001f
                    ? 0f
                    : Mathf.Max(0f, -Vector3.Dot(from - listener, direction) / sqrMagnitude);

                float distance = Vector3.Distance(from + t * direction, listener);
                Ammo ammo = info.Ammo;
                float initialSpeed = ammo != null ? ammo.InitialSpeed : 0f;

                EffectsManager.NotifyBulletWhizz(
                    new WhizzInfo(from, direction, listener, distance, ammo, initialSpeed, info.Delay));
            }
            catch (Exception e)
            {
                EffectsManager.Log.LogError($"EFTEffectManager: whizz handling failed: {e}");
            }
        }
    }
}
