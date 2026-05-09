using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace TMG.Survivors
{
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
