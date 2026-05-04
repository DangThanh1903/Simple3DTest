using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace TMG.Survivors
{
    public partial struct AerialArtillerySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            var ecbSystem = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (direction, aerialData, aerialState, transform) in SystemAPI
                         .Query<RefRW<CharacterMoveDirection>, AerialArtilleryData, RefRW<AerialArtilleryState>, LocalTransform>()
                         .WithAll<AerialArtilleryTag>())
            {
                var toPlayer = playerPosition.xy - transform.Position.xy;
                var distanceSq = math.lengthsq(toPlayer);
                var moveDirection = distanceSq > 0.0001f ? math.normalize(toPlayer) : float2.zero;
                var distance = math.sqrt(distanceSq);

                if (distance < aerialData.MinimumDistance)
                {
                    direction.ValueRW.Value = -moveDirection;
                }
                else if (distance > aerialData.PreferredDistance)
                {
                    direction.ValueRW.Value = moveDirection;
                }
                else
                {
                    direction.ValueRW.Value = float2.zero;
                }

                aerialState.ValueRW.ShootTimer -= deltaTime;
                if (aerialState.ValueRO.ShootTimer > 0f || aerialData.ProjectilePrefab == Entity.Null)
                {
                    continue;
                }

                var angle = math.atan2(toPlayer.y, toPlayer.x);
                var projectile = ecb.Instantiate(aerialData.ProjectilePrefab);
                ecb.SetComponent(projectile, LocalTransform.FromPositionRotation(transform.Position, quaternion.Euler(0f, 0f, angle)));
                ecb.AddComponent<EnemyProjectileTag>(projectile);
                ecb.AddComponent<InitializeEnemyProjectileFlag>(projectile);

                aerialState.ValueRW.ShootTimer = aerialData.ShootCooldown;
            }
        }
    }

    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial struct EnemyProjectileInitializationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var playerLayerMask = 1u << 6;
            var enemyLayerMask = 1u << 7;

            foreach (var (collider, entity) in SystemAPI
                         .Query<RefRW<PhysicsCollider>>()
                         .WithAll<EnemyProjectileTag, InitializeEnemyProjectileFlag>()
                         .WithEntityAccess())
            {
                var colliderValue = collider.ValueRW;
                colliderValue.MakeUnique(entity, state.EntityManager);
                colliderValue.Value.Value.SetCollisionFilter(new CollisionFilter
                {
                    BelongsTo = enemyLayerMask,
                    CollidesWith = playerLayerMask
                });

                collider.ValueRW = colliderValue;
                ecb.RemoveComponent<InitializeEnemyProjectileFlag>(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }

    public partial struct VolatileVanguardSystem : ISystem
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
            var playerDamageBuffer = SystemAPI.GetBuffer<DamageThisFrame>(playerEntity);

            foreach (var (transform, data, volatileState, entity) in SystemAPI
                         .Query<RefRW<LocalTransform>, VolatileVanguardData, RefRW<VolatileVanguardState>>()
                         .WithAll<VolatileVanguardTag>()
                         .WithPresent<DestroyEntityFlag>()
                         .WithEntityAccess())
            {
                volatileState.ValueRW.RemainingTime -= deltaTime;
                if (volatileState.ValueRO.BaseScale <= 0f)
                {
                    volatileState.ValueRW.BaseScale = transform.ValueRO.Scale;
                }

                var normalizedTime = math.saturate(volatileState.ValueRO.RemainingTime / data.CountdownTime);
                var blinkInterval = math.lerp(0.08f, 0.45f, normalizedTime);
                volatileState.ValueRW.BlinkTimer -= deltaTime;
                if (volatileState.ValueRO.BlinkTimer <= 0f)
                {
                    volatileState.ValueRW.BlinkExpanded = !volatileState.ValueRO.BlinkExpanded;
                    volatileState.ValueRW.BlinkTimer = blinkInterval;
                }

                var pulseScale = volatileState.ValueRO.BlinkExpanded ? 1.35f : 1f;
                transform.ValueRW.Scale = volatileState.ValueRO.BaseScale * pulseScale;

                if (volatileState.ValueRO.RemainingTime > 0f)
                {
                    continue;
                }

                var distanceToPlayerSq = math.distancesq(transform.ValueRO.Position.xy, playerPosition.xy);
                if (distanceToPlayerSq <= data.ExplosionRadius * data.ExplosionRadius)
                {
                    playerDamageBuffer.Add(new DamageThisFrame
                    {
                        Value = data.ExplosionDamage
                    });
                }

                SystemAPI.SetComponentEnabled<DestroyEntityFlag>(entity, true);
            }
        }
    }

    public partial struct HeavyLeaperSystem : ISystem
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
            var playerDamageBuffer = SystemAPI.GetBuffer<DamageThisFrame>(playerEntity);

            foreach (var (transform, direction, data, leaperState) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRW<CharacterMoveDirection>, HeavyLeaperData, RefRW<HeavyLeaperState>>()
                         .WithAll<HeavyLeaperTag>())
            {
                direction.ValueRW.Value = float2.zero;

                if (leaperState.ValueRO.BaseScale <= 0f)
                {
                    leaperState.ValueRW.BaseScale = transform.ValueRO.Scale;
                }

                leaperState.ValueRW.Timer -= deltaTime;
                if (leaperState.ValueRO.Phase == HeavyLeaperPhase.Waiting)
                {
                    transform.ValueRW.Scale = leaperState.ValueRO.BaseScale;
                    if (leaperState.ValueRO.Timer > 0f)
                    {
                        continue;
                    }

                    leaperState.ValueRW.Phase = HeavyLeaperPhase.Leaping;
                    leaperState.ValueRW.Timer = data.LeapDuration;
                    leaperState.ValueRW.StartPosition = transform.ValueRO.Position;
                    leaperState.ValueRW.TargetPosition = playerPosition;
                    continue;
                }

                var progress = 1f - math.saturate(leaperState.ValueRO.Timer / data.LeapDuration);
                var nextPosition = math.lerp(leaperState.ValueRO.StartPosition, leaperState.ValueRO.TargetPosition, progress);
                var arcScale = 1f + math.sin(progress * math.PI) * 0.75f;
                transform.ValueRW.Position = nextPosition;
                transform.ValueRW.Scale = leaperState.ValueRO.BaseScale * arcScale;

                if (leaperState.ValueRO.Timer > 0f)
                {
                    continue;
                }

                transform.ValueRW.Position = leaperState.ValueRO.TargetPosition;
                transform.ValueRW.Scale = leaperState.ValueRO.BaseScale;

                var distanceToPlayerSq = math.distancesq(transform.ValueRO.Position.xy, playerPosition.xy);
                if (distanceToPlayerSq <= data.SlamRadius * data.SlamRadius)
                {
                    playerDamageBuffer.Add(new DamageThisFrame
                    {
                        Value = data.SlamDamage
                    });
                }

                leaperState.ValueRW.Phase = HeavyLeaperPhase.Waiting;
                leaperState.ValueRW.Timer = data.LeapInterval;
            }
        }
    }
}
