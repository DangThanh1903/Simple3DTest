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
        
        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTag>(entity);
                AddBehaviorTag(entity, authoring.BehaviorType);
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

            private void AddBehaviorTag(Entity entity, EnemyBehaviorType behaviorType)
            {
                switch (behaviorType)
                {
                    case EnemyBehaviorType.MeleeChaser:
                        AddComponent<MeleeChaserTag>(entity);
                        break;
                    case EnemyBehaviorType.SwiftSwarm:
                        AddComponent<SwiftSwarmTag>(entity);
                        break;
                    case EnemyBehaviorType.AerialArtillery:
                        AddComponent<AerialArtilleryTag>(entity);
                        break;
                    case EnemyBehaviorType.VolatileVanguard:
                        AddComponent<VolatileVanguardTag>(entity);
                        break;
                    case EnemyBehaviorType.HeavyLeaper:
                        AddComponent<HeavyLeaperTag>(entity);
                        break;
                    case EnemyBehaviorType.StasisOverlord:
                        AddComponent<StasisOverlordTag>(entity);
                        break;
                }
            }
        }
    }

    public partial struct MeleeChaserMoveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position.xy;

            var moveToPlayerJob = new MeleeChaserMoveJob
            {
                PlayerPosition = playerPosition
            };

            state.Dependency = moveToPlayerJob.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(MeleeChaserTag))]
    public partial struct MeleeChaserMoveJob : IJobEntity
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
