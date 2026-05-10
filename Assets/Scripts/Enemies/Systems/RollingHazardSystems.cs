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
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            var playerKnockback = SystemAPI.GetComponentRW<PlayerKnockback>(playerEntity);
            var playerFreeze = SystemAPI.GetComponentRW<PlayerFreeze>(playerEntity);

            foreach (var (transform, data, hazardState, entity) in SystemAPI
                         .Query<RefRW<LocalTransform>, RollingHazardData, RefRW<RollingHazardState>>()
                         .WithAll<RollingHazardTag>()
                         .WithPresent<DestroyEntityFlag>()
                         .WithEntityAccess())
            {
                var startPosition = transform.ValueRO.Position;
                var direction = math.lengthsq(hazardState.ValueRO.Direction) > 0.0001f
                    ? math.normalize(hazardState.ValueRO.Direction)
                    : new float2(1f, 0f);
                var endPosition = startPosition + new float3(direction * data.MoveSpeed * deltaTime, 0f);

                if (!hazardState.ValueRO.HasHitPlayer && TryGetSegmentCircleHit(
                        startPosition.xy,
                        endPosition.xy,
                        playerPosition.xy,
                        data.ContactRadius))
                {
                    hazardState.ValueRW.HasHitPlayer = true;
                    playerFreeze.ValueRW.RemainingTime = math.max(playerFreeze.ValueRO.RemainingTime, data.StunDuration);
                    playerKnockback.ValueRW = new PlayerKnockback
                    {
                        RemainingTime = data.KnockbackDuration,
                        Velocity = direction * data.KnockbackSpeed
                    };
                }

                transform.ValueRW.Position = endPosition;
                transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, quaternion.RotateZ(-data.MoveSpeed * deltaTime));

                hazardState.ValueRW.RemainingTime -= deltaTime;
                if (hazardState.ValueRO.RemainingTime <= 0f)
                {
                    SystemAPI.SetComponentEnabled<DestroyEntityFlag>(entity, true);
                }
            }
        }

        private static bool TryGetSegmentCircleHit(
            float2 start,
            float2 end,
            float2 center,
            float radius)
        {
            var segment = end - start;
            var segmentLengthSq = math.lengthsq(segment);
            var radiusSq = radius * radius;
            if (segmentLengthSq <= float.Epsilon)
            {
                return math.distancesq(start, center) <= radiusSq;
            }

            var centerToStart = start - center;
            var b = 2f * math.dot(centerToStart, segment);
            var c = math.lengthsq(centerToStart) - radiusSq;
            if (c <= 0f)
            {
                return true;
            }

            var discriminant = b * b - 4f * segmentLengthSq * c;
            if (discriminant < 0f)
            {
                return false;
            }

            var t = (-b - math.sqrt(discriminant)) / (2f * segmentLengthSq);
            return t >= 0f && t <= 1f;
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

}
