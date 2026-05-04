using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace TMG.Survivors
{
    public struct EnemyTag : IComponentData {}
    public struct ChasePlayerTag : IComponentData {}

    public enum EnemyBehaviorType : byte
    {
        MeleeChaser = 0,
        SwiftSwarm = 1,
        AerialArtillery = 2,
        VolatileVanguard = 3,
        HeavyLeaper = 4,
        StasisOverlord = 5
    }

    public struct MeleeChaserTag : IComponentData {}
    public struct SwiftSwarmTag : IComponentData {}
    public struct AerialArtilleryTag : IComponentData {}
    public struct VolatileVanguardTag : IComponentData {}
    public struct HeavyLeaperTag : IComponentData {}
    public struct StasisOverlordTag : IComponentData {}

    public struct RollingHazardTag : IComponentData {}
    public struct LightningStrikerTag : IComponentData {}
    public struct EnemyProjectileTag : IComponentData {}
    public struct InitializeEnemyProjectileFlag : IComponentData {}

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
    
    [RequireComponent(typeof(CharacterAuthoring))]
    public class EnemyAuthoring : MonoBehaviour
    {
        public EnemyBehaviorType BehaviorType = EnemyBehaviorType.MeleeChaser;
        public int AttackDamage;
        public float CooldownTime;
        public GameObject GemPrefab;

        [Header("Aerial Artillery")]
        public GameObject AerialProjectilePrefab;
        public float AerialMinimumDistance = 7f;
        public float AerialPreferredDistance = 10f;
        public float AerialShootCooldown = 1.25f;

        [Header("Volatile Vanguard")]
        public float VolatileCountdownTime = 5f;
        public float VolatileExplosionRadius = 3f;
        public int VolatileExplosionDamage = 25;

        [Header("Heavy Leaper")]
        public float HeavyLeapInterval = 1.5f;
        public float HeavyLeapDuration = 0.65f;
        public float HeavySlamRadius = 3.5f;
        public int HeavySlamDamage = 20;
        
        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTag>(entity);
                AddBehaviorTag(entity, authoring);
                AddComponent(entity, new EnemyAttackData
                {
                    HitPoints = authoring.AttackDamage,
                    CooldownTime = authoring.CooldownTime
                });
                AddComponent<EnemyCooldownExpirationTimestamp>(entity);
                SetComponentEnabled<EnemyCooldownExpirationTimestamp>(entity, false);
                AddComponent(entity, new GemPrefab
                {
                    Value = GetEntity(authoring.GemPrefab, TransformUsageFlags.Dynamic)
                });
            }

            private void AddBehaviorTag(Entity entity, EnemyAuthoring authoring)
            {
                switch (authoring.BehaviorType)
                {
                    case EnemyBehaviorType.MeleeChaser:
                        AddComponent<MeleeChaserTag>(entity);
                        AddComponent<ChasePlayerTag>(entity);
                        break;
                    case EnemyBehaviorType.SwiftSwarm:
                        AddComponent<SwiftSwarmTag>(entity);
                        AddComponent<ChasePlayerTag>(entity);
                        break;
                    case EnemyBehaviorType.AerialArtillery:
                        AddComponent<AerialArtilleryTag>(entity);
                        AddComponent(entity, new AerialArtilleryData
                        {
                            ProjectilePrefab = GetOptionalEntity(authoring.AerialProjectilePrefab),
                            MinimumDistance = math.max(0f, authoring.AerialMinimumDistance),
                            PreferredDistance = math.max(authoring.AerialMinimumDistance, authoring.AerialPreferredDistance),
                            ShootCooldown = math.max(0.1f, authoring.AerialShootCooldown)
                        });
                        AddComponent(entity, new AerialArtilleryState
                        {
                            ShootTimer = 0f
                        });
                        break;
                    case EnemyBehaviorType.VolatileVanguard:
                        AddComponent<VolatileVanguardTag>(entity);
                        AddComponent(entity, new VolatileVanguardData
                        {
                            CountdownTime = math.max(0.1f, authoring.VolatileCountdownTime),
                            ExplosionRadius = math.max(0.1f, authoring.VolatileExplosionRadius),
                            ExplosionDamage = math.max(1, authoring.VolatileExplosionDamage)
                        });
                        AddComponent(entity, new VolatileVanguardState
                        {
                            RemainingTime = math.max(0.1f, authoring.VolatileCountdownTime),
                            BlinkTimer = 0f,
                            BaseScale = math.max(0.1f, authoring.transform.localScale.x),
                            BlinkExpanded = false
                        });
                        break;
                    case EnemyBehaviorType.HeavyLeaper:
                        AddComponent<HeavyLeaperTag>(entity);
                        AddComponent(entity, new HeavyLeaperData
                        {
                            LeapInterval = math.max(0.1f, authoring.HeavyLeapInterval),
                            LeapDuration = math.max(0.1f, authoring.HeavyLeapDuration),
                            SlamRadius = math.max(0.1f, authoring.HeavySlamRadius),
                            SlamDamage = math.max(1, authoring.HeavySlamDamage)
                        });
                        AddComponent(entity, new HeavyLeaperState
                        {
                            Phase = HeavyLeaperPhase.Waiting,
                            Timer = math.max(0.1f, authoring.HeavyLeapInterval),
                            StartPosition = float3.zero,
                            TargetPosition = float3.zero,
                            BaseScale = math.max(0.1f, authoring.transform.localScale.x)
                        });
                        break;
                    case EnemyBehaviorType.StasisOverlord:
                        AddComponent<StasisOverlordTag>(entity);
                        break;
                }
            }

            private Entity GetOptionalEntity(GameObject prefab)
            {
                return prefab == null ? Entity.Null : GetEntity(prefab, TransformUsageFlags.Dynamic);
            }
        }
    }

    public partial struct ChasePlayerMoveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position.xy;

            var moveToPlayerJob = new ChasePlayerMoveJob
            {
                PlayerPosition = playerPosition
            };

            state.Dependency = moveToPlayerJob.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(ChasePlayerTag))]
    public partial struct ChasePlayerMoveJob : IJobEntity
    {
        public float2 PlayerPosition;
        
        private void Execute(ref CharacterMoveDirection direction, in LocalTransform transform)
        {
            var vectorToPlayer = PlayerPosition - transform.Position.xy;
            direction.Value = math.lengthsq(vectorToPlayer) > 0.0001f ? math.normalize(vectorToPlayer) : float2.zero;
        }
    }

    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [UpdateBefore(typeof(AfterPhysicsSystemGroup))]
    public partial struct EnemyAttackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var elapsedTime = SystemAPI.Time.ElapsedTime;
            foreach (var (expirationTimestamp, cooldownEnabled) in SystemAPI.Query<EnemyCooldownExpirationTimestamp, EnabledRefRW<EnemyCooldownExpirationTimestamp>>())
            {
                if (expirationTimestamp.Value > elapsedTime) continue;
                cooldownEnabled.ValueRW = false;
            }

            var attackJob = new EnemyAttackJob
            {
                PlayerLookup = SystemAPI.GetComponentLookup<PlayerTag>(true),
                AttackDataLookup = SystemAPI.GetComponentLookup<EnemyAttackData>(true),
                CooldownLookup = SystemAPI.GetComponentLookup<EnemyCooldownExpirationTimestamp>(),
                DamageBufferLookup = SystemAPI.GetBufferLookup<DamageThisFrame>(),
                ElapsedTime = elapsedTime
            };

            var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();
            state.Dependency = attackJob.Schedule(simulationSingleton, state.Dependency);
        }
    }

    [BurstCompile]
    public struct EnemyAttackJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<PlayerTag> PlayerLookup;
        [ReadOnly] public ComponentLookup<EnemyAttackData> AttackDataLookup;
        public ComponentLookup<EnemyCooldownExpirationTimestamp> CooldownLookup;
        public BufferLookup<DamageThisFrame> DamageBufferLookup;

        public double ElapsedTime;
        
        public void Execute(CollisionEvent collisionEvent)
        {
            Entity playerEntity;
            Entity enemyEntity;

            if (PlayerLookup.HasComponent(collisionEvent.EntityA) && AttackDataLookup.HasComponent(collisionEvent.EntityB))
            {
                playerEntity = collisionEvent.EntityA;
                enemyEntity = collisionEvent.EntityB;
            }
            else if (PlayerLookup.HasComponent(collisionEvent.EntityB) && AttackDataLookup.HasComponent(collisionEvent.EntityA))
            {
                playerEntity = collisionEvent.EntityB;
                enemyEntity = collisionEvent.EntityA;
            }
            else
            {
                return;
            }

            if (CooldownLookup.IsComponentEnabled(enemyEntity)) return;

            var attackData = AttackDataLookup[enemyEntity];
            CooldownLookup[enemyEntity] = new EnemyCooldownExpirationTimestamp { Value = ElapsedTime + attackData.CooldownTime };
            CooldownLookup.SetComponentEnabled(enemyEntity, true);

            var playerDamageBuffer = DamageBufferLookup[playerEntity];
            playerDamageBuffer.Add(new DamageThisFrame
            {
                Value = attackData.HitPoints
            });
        }
    }
}
