using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;

namespace TMG.Survivors
{
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
}
