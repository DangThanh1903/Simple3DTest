using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace TMG.Survivors
{
    public partial struct AerialArtillerySystem : ISystem
    {
        private EntityQuery _pooledEnemyProjectileQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
            _pooledEnemyProjectileQuery = SystemAPI.QueryBuilder()
                .WithAll<PooledEnemyProjectileTag, EnemyProjectileTag, Disabled, PlasmaBlastData, LocalTransform>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build();
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

                SpawnEnemyProjectile(ref state, ecb, aerialData.ProjectilePrefab, transform.Position, toPlayer);

                aerialState.ValueRW.ShootTimer = aerialData.ShootCooldown;
            }
        }

        private void SpawnEnemyProjectile(
            ref SystemState state,
            EntityCommandBuffer ecb,
            Entity projectilePrefab,
            float3 spawnPosition,
            float2 toPlayer)
        {
            var angle = math.atan2(toPlayer.y, toPlayer.x);
            var spawnTransform = LocalTransform.FromPositionRotation(spawnPosition, quaternion.Euler(0f, 0f, angle));
            var projectile = GetPooledProjectile();

            if (projectile == Entity.Null)
            {
                projectile = ecb.Instantiate(projectilePrefab);
                ecb.AddComponent<EnemyProjectileTag>(projectile);
                ecb.AddComponent<PooledEnemyProjectileTag>(projectile);
                ecb.AddComponent<InitializeEnemyProjectileFlag>(projectile);
            }
            else
            {
                var projectileData = state.EntityManager.GetComponentData<PlasmaBlastData>(projectile);
                ecb.RemoveComponent<Disabled>(projectile);
                ecb.SetComponent(projectile, new PlasmaBlastExpirationTimer
                {
                    Value = projectileData.Lifetime
                });
                ecb.SetComponentEnabled<DestroyEntityFlag>(projectile, false);
            }

            ecb.SetComponent(projectile, spawnTransform);
        }

        private Entity GetPooledProjectile()
        {
            if (_pooledEnemyProjectileQuery.IsEmptyIgnoreFilter)
            {
                return Entity.Null;
            }

            var pooledProjectiles = _pooledEnemyProjectileQuery.ToEntityArray(Allocator.Temp);
            var projectile = pooledProjectiles.Length > 0 ? pooledProjectiles[0] : Entity.Null;
            pooledProjectiles.Dispose();
            return projectile;
        }
    }

    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial struct EnemyProjectileInitializationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var playerLayerMask = 1u << 6;
            var enemyLayerMask = 1u << 7;
            var query = SystemAPI.QueryBuilder()
                .WithAll<EnemyProjectileTag, InitializeEnemyProjectileFlag, PhysicsCollider>()
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
                    BelongsTo = enemyLayerMask,
                    CollidesWith = playerLayerMask
                });

                state.EntityManager.SetComponentData(entity, colliderValue);
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

    public partial struct StasisOverlordSystem : ISystem
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
            var playerFreeze = SystemAPI.GetComponentRW<PlayerFreeze>(playerEntity);

            foreach (var (transform, data, stasisState) in SystemAPI
                         .Query<RefRW<LocalTransform>, StasisOverlordData, RefRW<StasisOverlordState>>()
                         .WithAll<StasisOverlordTag>())
            {
                if (stasisState.ValueRO.BaseScale <= 0f)
                {
                    stasisState.ValueRW.BaseScale = transform.ValueRO.Scale;
                }

                stasisState.ValueRW.CastTimer -= deltaTime;
                transform.ValueRW.Scale = stasisState.ValueRO.BaseScale;

                var distanceToPlayerSq = math.distancesq(transform.ValueRO.Position.xy, playerPosition.xy);
                if (stasisState.ValueRO.CastTimer > 0f ||
                    distanceToPlayerSq > data.FreezeRange * data.FreezeRange)
                {
                    continue;
                }

                playerFreeze.ValueRW.RemainingTime = math.max(playerFreeze.ValueRO.RemainingTime, data.FreezeDuration);
                stasisState.ValueRW.CastTimer = data.CastCooldown;
                transform.ValueRW.Scale = stasisState.ValueRO.BaseScale * 1.35f;
            }
        }
    }

    public partial struct LightningStrikerSystem : ISystem
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

            foreach (var (transform, data, lightningState, entity) in SystemAPI
                         .Query<RefRW<LocalTransform>, LightningStrikerData, RefRW<LightningStrikerState>>()
                         .WithAll<LightningStrikerTag>()
                         .WithPresent<DestroyEntityFlag>()
                         .WithEntityAccess())
            {
                if (lightningState.ValueRO.BaseScale <= 0f)
                {
                    lightningState.ValueRW.BaseScale = transform.ValueRO.Scale;
                }

                lightningState.ValueRW.Timer -= deltaTime;

                if (lightningState.ValueRO.Phase == LightningStrikerPhase.Flying)
                {
                    transform.ValueRW.Position += new float3(data.FlySpeed * deltaTime, 0f, 0f);
                    transform.ValueRW.Scale = lightningState.ValueRO.BaseScale;

                    if (lightningState.ValueRO.Timer > 0f)
                    {
                        continue;
                    }

                    lightningState.ValueRW.Phase = LightningStrikerPhase.Locking;
                    lightningState.ValueRW.Timer = data.LockDuration;
                    lightningState.ValueRW.LockedTargetPosition = playerPosition;
                    continue;
                }

                if (lightningState.ValueRO.Phase == LightningStrikerPhase.Locking)
                {
                    transform.ValueRW.Scale = lightningState.ValueRO.BaseScale * 1.4f;

                    if (lightningState.ValueRO.Timer > 0f)
                    {
                        continue;
                    }

                    lightningState.ValueRW.Phase = LightningStrikerPhase.Striking;
                    lightningState.ValueRW.Timer = 0.15f;

                    var distanceToLockedTargetSq = math.distancesq(playerPosition.xy, lightningState.ValueRO.LockedTargetPosition.xy);
                    if (distanceToLockedTargetSq <= data.StrikeRadius * data.StrikeRadius)
                    {
                        playerDamageBuffer.Add(new DamageThisFrame
                        {
                            Value = data.StrikeDamage
                        });
                    }

                    continue;
                }

                transform.ValueRW.Position = lightningState.ValueRO.LockedTargetPosition;
                transform.ValueRW.Scale = lightningState.ValueRO.BaseScale * 2f;
                if (lightningState.ValueRO.Timer <= 0f)
                {
                    SystemAPI.SetComponentEnabled<DestroyEntityFlag>(entity, true);
                }
            }
        }
    }

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
