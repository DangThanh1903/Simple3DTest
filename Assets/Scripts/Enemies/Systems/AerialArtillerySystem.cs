using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
            var pooledProjectiles = _pooledEnemyProjectileQuery.ToEntityArray(Allocator.Temp);
            var pooledProjectileIndex = 0;

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
                var projectilePrefab = aerialData.ProjectilePrefab;
                if (aerialState.ValueRO.ShootTimer > 0f ||
                    projectilePrefab == Entity.Null ||
                    !state.EntityManager.HasComponent<PlasmaBlastData>(projectilePrefab))
                {
                    continue;
                }

                SpawnEnemyProjectile(
                    ref state,
                    ecb,
                    projectilePrefab,
                    transform.Position,
                    toPlayer,
                    pooledProjectiles,
                    ref pooledProjectileIndex);

                aerialState.ValueRW.ShootTimer = aerialData.ShootCooldown;
            }

            pooledProjectiles.Dispose();
        }

        private void SpawnEnemyProjectile(
            ref SystemState state,
            EntityCommandBuffer ecb,
            Entity projectilePrefab,
            float3 spawnPosition,
            float2 toPlayer,
            NativeArray<Entity> pooledProjectiles,
            ref int pooledProjectileIndex)
        {
            var angle = math.atan2(toPlayer.y, toPlayer.x);
            var spawnTransform = LocalTransform.FromPositionRotation(spawnPosition, quaternion.Euler(0f, 0f, angle));
            var projectileData = state.EntityManager.GetComponentData<PlasmaBlastData>(projectilePrefab);
            var projectile = pooledProjectileIndex < pooledProjectiles.Length
                ? pooledProjectiles[pooledProjectileIndex++]
                : Entity.Null;

            if (projectile == Entity.Null)
            {
                projectile = ecb.Instantiate(projectilePrefab);
                if (!state.EntityManager.HasComponent<EnemyProjectileTag>(projectilePrefab))
                {
                    ecb.AddComponent<EnemyProjectileTag>(projectile);
                }

                if (!state.EntityManager.HasComponent<PooledEnemyProjectileTag>(projectilePrefab))
                {
                    ecb.AddComponent<PooledEnemyProjectileTag>(projectile);
                }

                if (!state.EntityManager.HasComponent<InitializeEnemyProjectileFlag>(projectilePrefab))
                {
                    ecb.AddComponent<InitializeEnemyProjectileFlag>(projectile);
                }
            }
            else
            {
                ecb.RemoveComponent<Disabled>(projectile);
                ecb.SetComponentEnabled<DestroyEntityFlag>(projectile, false);
            }

            ecb.SetComponent(projectile, new PlasmaBlastExpirationTimer
            {
                Value = projectileData.Lifetime
            });
            ecb.SetComponent(projectile, spawnTransform);
        }
    }
}
