using UnityEngine;

namespace ScaryParty.Combat
{
    public enum WeaponType : byte
    {
        Unarmed = 0,
        Pistol = 1,
        Shotgun = 2
    }

    public enum WeaponHoldMode : byte
    {
        None = 0,
        OneHanded = 1,
        TwoHanded = 2
    }

    public enum DamageSourceType : byte
    {
        Environment = 0,
        EnemyTest = 1,
        Firearm = 2,
        Collision = 3
    }

    public struct DamageEvent
    {
        public int Amount;
        public DamageSourceType Source;
        public ulong InstigatorClientId;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamageServer(DamageEvent evt);
    }
}
