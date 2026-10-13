using EFT;
using EFT.Ballistics;

namespace EFTEffectManager
{
    /// <summary>
    /// Delivered by <see cref="EffectsManager.OnHit"/>
    /// </summary>
    public sealed class HitInfo
    {
        public Player Player { get; }
        public float Damage { get; }

        /// <summary>Body part that got hit</summary>
        public EBodyPart BodyPart { get; }

        /// <summary>What did it - Bullet, GrenadeFragment, Fall, Melee</summary>
        public EDamageType DamageType { get; }

        /// <summary>Part of the damage that armor / protection ate</summary>
        public float Absorbed { get; }

        /// <summary>What the projectile hit</summary>
        public MaterialType Material { get; }

        /// <summary>Bullet or grenade fragment</summary>
        public bool IsBallistic => DamageType == EDamageType.Bullet || DamageType == EDamageType.GrenadeFragment;

        internal HitInfo(Player player, float damage, EBodyPart bodyPart, EDamageType damageType, float absorbed,
            MaterialType material)
        {
            Player = player;
            Damage = damage;
            BodyPart = bodyPart;
            DamageType = damageType;
            Absorbed = absorbed;
            Material = material;
        }
    }
}
