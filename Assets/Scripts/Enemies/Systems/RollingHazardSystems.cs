using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace TMG.Survivors
{
    public partial struct RollingHazardMoveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, data, hazardState, entity) in SystemAPI
                         .Query<RefRW<LocalTransform>, RollingHazardData, RefRW<RollingHazardState>>()
                         .WithAll<RollingHazardTag>()
                         .WithPresent<DestroyEntityFlag>()
                         .WithEntityAccess())
            {
                transform.ValueRW.Position += new float3(hazardState.ValueRO.Direction * data.MoveSpeed * deltaTime, 0f);
                transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, quaternion.RotateZ(-data.MoveSpeed * deltaTime));

                hazardState.ValueRW.RemainingTime -= deltaTime;
                if (hazardState.ValueRO.RemainingTime <= 0f)
                {
                    SystemAPI.SetComponentEnabled<DestroyEntityFlag>(entity, true);
                }
            }
        }
    }

    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial struct RollingHazardInitializationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var playerLayerMask = 1u << 6;
            var environmentLayerMask = 1u << 9;
            var query = SystemAPI.QueryBuilder()
                .WithAll<RollingHazardTag, InitializeRollingHazardFlag, PhysicsCollider>()
                .Build();
            var entities = query.ToEntityArray(state.WorldUpdateAllocator);
            var colliders = query.ToComponentDataArray<PhysicsCollider>(state.WorldUpdateAllocator);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var colliderValue = colliders[i];
                colliderValue.MakeUnique(entity, state.EntityManager);
                colliderValue.Value.Value.SetCollisionFilter(new CollisionFilter
                {
                    BelongsTo = environmentLayerMask,
                    CollidesWith = playerLayerMask
                });

                state.EntityManager.SetComponentData(entity, colliderValue);
                ecb.RemoveComponent<InitializeRollingHazardFlag>(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }

    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [UpdateBefore(typeof(AfterPhysicsSystemGroup))]
    public partial struct RollingHazardKnockbackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var knockbackJob = new RollingHazardKnockbackJob
            {
                HazardLookup = SystemAPI.GetComponentLookup<RollingHazardData>(true),
                HazardStateLookup = SystemAPI.GetComponentLookup<RollingHazardState>(true),
                PlayerLookup = SystemAPI.GetComponentLookup<PlayerTag>(true),
                PlayerKnockbackLookup = SystemAPI.GetComponentLookup<PlayerKnockback>()
            };

            var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();
            state.Dependency = knockbackJob.Schedule(simulationSingleton, state.Dependency);
        }
    }

    public struct RollingHazardKnockbackJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<RollingHazardData> HazardLookup;
        [ReadOnly] public ComponentLookup<RollingHazardState> HazardStateLookup;
        [ReadOnly] public ComponentLookup<PlayerTag> PlayerLookup;
        public ComponentLookup<PlayerKnockback> PlayerKnockbackLookup;

        public void Execute(CollisionEvent collisionEvent)
        {
            Entity hazardEntity;
            Entity playerEntity;

            if (HazardLookup.HasComponent(collisionEvent.EntityA) && PlayerLookup.HasComponent(collisionEvent.EntityB))
            {
                hazardEntity = collisionEvent.EntityA;
                playerEntity = collisionEvent.EntityB;
            }
            else if (HazardLookup.HasComponent(collisionEvent.EntityB) && PlayerLookup.HasComponent(collisionEvent.EntityA))
            {
                hazardEntity = collisionEvent.EntityB;
                playerEntity = collisionEvent.EntityA;
            }
            else
            {
                return;
            }

            var hazardData = HazardLookup[hazardEntity];
            var hazardState = HazardStateLookup[hazardEntity];
            var knockbackDirection = math.lengthsq(hazardState.Direction) > 0.0001f
                ? math.normalize(hazardState.Direction)
                : new float2(1f, 0f);

            PlayerKnockbackLookup[playerEntity] = new PlayerKnockback
            {
                RemainingTime = hazardData.KnockbackDuration,
                Velocity = knockbackDirection * hazardData.KnockbackSpeed
            };
        }
    }
}
