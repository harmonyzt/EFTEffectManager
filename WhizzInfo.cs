using EFT.InventoryLogic;
using UnityEngine;

namespace EFTEffectManager
{
    /// <summary>
    /// Delivered by <see cref="EffectsManager.OnBulletWhizz"/>
    /// </summary>
    public sealed class WhizzInfo
    {
        public Vector3 ShotPosition { get; }
        public Vector3 ShotDirection { get; }

        /// <summary>Listener position</summary>
        public Vector3 PlayerPosition { get; }

        /// <summary>How close shot's path came to the player, in meters</summary>
        public float Distance { get; }

        /// <summary>Distance from player to the guy who shot at us, in meters</summary>
        public float ShooterDistance { get; }

        /// <summary>Ammo that was fired (can be null!)</summary>
        public Ammo Ammo { get; }

        /// <summary>Muzzle speed of that ammo (0 if we don't know!)</summary>
        public float InitialSpeed { get; }

        /// <summary>Seconds until player hears the whew</summary>
        public float Delay { get; }

        internal WhizzInfo(Vector3 shotPosition, Vector3 shotDirection, Vector3 playerPosition, float distance,
            Ammo ammo, float initialSpeed, float delay)
        {
            ShotPosition = shotPosition;
            ShotDirection = shotDirection;
            PlayerPosition = playerPosition;
            Distance = distance;
            ShooterDistance = Vector3.Distance(playerPosition, shotPosition);
            Ammo = ammo;
            InitialSpeed = initialSpeed;
            Delay = delay;
        }
    }
}
