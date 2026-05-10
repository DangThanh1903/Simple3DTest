using Unity.Entities;
using Unity.Mathematics;

namespace TMG.Survivors
{
    public struct EnemyTag : IComponentData {}
    public struct ChasePlayerTag : IComponentData {}

    public struct MeleeChaserTag : IComponentData {}
    public struct SwiftSwarmTag : IComponentData {}
    public struct AerialArtilleryTag : IComponentData {}
    public struct VolatileVanguardTag : IComponentData {}
    public struct HeavyLeaperTag : IComponentData {}
    public struct StasisOverlordTag : IComponentData {}

    public struct RollingHazardTag : IComponentData {}
    public struct InitializeRollingHazardFlag : IComponentData {}
    public struct LightningStrikerTag : IComponentData {}
    public struct EnemyProjectileTag : IComponentData {}
    public struct InitializeEnemyProjectileFlag : IComponentData {}
    public struct PooledEnemyProjectileTag : IComponentData {}

    public struct RollingHazardData : IComponentData
    {
        public float Lifetime;
        public float MoveSpeed;
        public float ContactRadius;
        public float StunDuration;
        public float KnockbackSpeed;
        public float KnockbackDuration;
    }

    public struct RollingHazardState : IComponentData
    {
        public float RemainingTime;
        public float2 Direction;
        public bool HasHitPlayer;
    }

    public struct PlayerKnockback : IComponentData
    {
        public float RemainingTime;
        public float2 Velocity;
    }

    public struct AerialArtilleryData : IComponentData
    {
        public Entity ProjectilePrefab;
        public float MinimumDistance;
        public float PreferredDistance;
        public float ShootCooldown;
    }

    public struct AerialArtilleryState : IComponentData
    {
        public float ShootTimer;
    }

    public struct VolatileVanguardData : IComponentData
    {
        public float CountdownTime;
        public float ExplosionRadius;
        public int ExplosionDamage;
    }

    public struct VolatileVanguardState : IComponentData
    {
        public float RemainingTime;
        public float BlinkTimer;
        public float BaseScale;
        public bool BlinkExpanded;
    }

    public enum HeavyLeaperPhase : byte
    {
        Waiting,
        Leaping
    }

    public struct HeavyLeaperData : IComponentData
    {
        public float LeapInterval;
        public float LeapDuration;
        public float SlamRadius;
        public int SlamDamage;
    }

    public struct HeavyLeaperState : IComponentData
    {
        public HeavyLeaperPhase Phase;
        public float Timer;
        public float3 StartPosition;
        public float3 TargetPosition;
        public float BaseScale;
    }

    public struct StasisOverlordData : IComponentData
    {
        public float MinimumDistance;
        public float PreferredDistance;
        public float FreezeRange;
        public float FreezeDuration;
        public float CastCooldown;
    }

    public struct StasisOverlordState : IComponentData
    {
        public float CastTimer;
        public float BaseScale;
    }

    public enum LightningStrikerPhase : byte
    {
        Flying,
        Locking,
        Striking
    }

    public struct LightningStrikerData : IComponentData
    {
        public Entity WarningPrefab;
        public Entity StrikeFlashPrefab;
        public float FlyDuration;
        public float FlySpeed;
        public float LockDuration;
        public float StrikeFlashDuration;
        public float StrikeRadius;
        public int StrikeDamage;
    }

    public struct TimedVisualEffect : IComponentData
    {
        public float RemainingTime;
    }

    public struct LightningStrikerState : IComponentData
    {
        public LightningStrikerPhase Phase;
        public float Timer;
        public float3 LockedTargetPosition;
        public float BaseScale;
    }

    public struct EnemyAttackData : IComponentData
    {
        public int HitPoints;
        public float CooldownTime;
    }

    public struct EnemyCooldownExpirationTimestamp : IComponentData, IEnableableComponent
    {
        public double Value;
    }

    public struct GemPrefab : IComponentData
    {
        public Entity Value;
    }
}
