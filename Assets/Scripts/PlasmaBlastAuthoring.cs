using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.Burst;
using Unity.Mathematics;

namespace TMG.Survivors
{
    public struct PlasmaBlastData : IComponentData
    {
        public float MoveSpeed;
        public int AttackDamage;
    }

    public struct PlasmaBlastExpirationTimer : IComponentData
    {
        public float Value;
    }
    
    public class PlasmaBlastAuthoring : MonoBehaviour
    {
        public float MoveSpeed;
        public int AttackDamage;

        public float DestroyAfterTime;
        
        private class Baker : Baker<PlasmaBlastAuthoring>
        {
            public override void Bake(PlasmaBlastAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PlasmaBlastData
                {
                    MoveSpeed = authoring.MoveSpeed,
                    AttackDamage = authoring.AttackDamage
                });
                
                AddComponent(entity, new PlasmaBlastExpirationTimer
                {
                    Value = authoring.DestroyAfterTime
                });
                
                AddComponent<DestroyEntityFlag>(entity);
                SetComponentEnabled<DestroyEntityFlag>(entity, false);
            }
        }
    }

    public partial struct MovePlasmaBlastSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Destroy Plasma Blast After Time
            foreach (var (timer, entity) in SystemAPI.Query<RefRW<PlasmaBlastExpirationTimer>>().WithPresent<DestroyEntityFlag>().WithEntityAccess())
            {
                timer.ValueRW.Value -= deltaTime;
                if (timer.ValueRO.Value > 0) continue;
                SystemAPI.SetComponentEnabled<DestroyEntityFlag>(entity, true);
            }

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var raycastJob = new PlasmaBlastRaycastMoveJob
            {
                DeltaTime = deltaTime,
                PhysicsWorld = physicsWorld,
                EnemyProjectileLookup = SystemAPI.GetComponentLookup<EnemyProjectileTag>(true),
                EnemyLookup = SystemAPI.GetComponentLookup<EnemyTag>(true),
                PlayerLookup = SystemAPI.GetComponentLookup<PlayerTag>(true),
                DestroyEntityLookup = SystemAPI.GetComponentLookup<DestroyEntityFlag>(true),
                CommandBuffer = ecb.AsParallelWriter()
            };

            state.Dependency = raycastJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    public partial struct PlasmaBlastRaycastMoveJob : IJobEntity
    {
        [ReadOnly] public PhysicsWorld PhysicsWorld;
        [ReadOnly] public ComponentLookup<EnemyProjectileTag> EnemyProjectileLookup;
        [ReadOnly] public ComponentLookup<EnemyTag> EnemyLookup;
        [ReadOnly] public ComponentLookup<PlayerTag> PlayerLookup;
        [ReadOnly] public ComponentLookup<DestroyEntityFlag> DestroyEntityLookup;
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        public float DeltaTime;

        private void Execute(
            [EntityIndexInQuery] int sortKey,
            Entity entity,
            ref LocalTransform transform,
            in PlasmaBlastData data)
        {
            var start = transform.Position;
            var end = start + transform.Right() * data.MoveSpeed * DeltaTime;
            var isEnemyProjectile = EnemyProjectileLookup.HasComponent(entity);
            var target = FindCurrentOverlapTarget(start, isEnemyProjectile);
            if (target != Entity.Null)
            {
                ApplyHit(sortKey, entity, target, data.AttackDamage, start, ref transform);
                return;
            }

            var raycastInput = new RaycastInput
            {
                Start = start,
                End = end,
                Filter = new CollisionFilter
                {
                    BelongsTo = uint.MaxValue,
                    CollidesWith = uint.MaxValue
                }
            };

            if (PhysicsWorld.CastRay(raycastInput, out var hit) && IsValidTarget(hit.Entity, isEnemyProjectile))
            {
                ApplyHit(sortKey, entity, hit.Entity, data.AttackDamage, hit.Position, ref transform);
                return;
            }

            transform.Position = end;
        }

        private Entity FindCurrentOverlapTarget(float3 position, bool isEnemyProjectile)
        {
            var overlapInput = new PointDistanceInput
            {
                Position = position,
                MaxDistance = 0.05f,
                Filter = new CollisionFilter
                {
                    BelongsTo = uint.MaxValue,
                    CollidesWith = uint.MaxValue
                }
            };

            if (!PhysicsWorld.CalculateDistance(overlapInput, out var hit))
            {
                return Entity.Null;
            }

            return IsValidTarget(hit.Entity, isEnemyProjectile) ? hit.Entity : Entity.Null;
        }

        private void ApplyHit(
            int sortKey,
            Entity projectileEntity,
            Entity targetEntity,
            int damage,
            float3 hitPosition,
            ref LocalTransform transform)
        {
            transform.Position = hitPosition;
            CommandBuffer.AppendToBuffer(sortKey, targetEntity, new DamageThisFrame
            {
                Value = damage
            });
            CommandBuffer.SetComponentEnabled<DestroyEntityFlag>(sortKey, projectileEntity, true);
        }

        private bool IsValidTarget(Entity hitEntity, bool isEnemyProjectile)
        {
            if (!DestroyEntityLookup.HasComponent(hitEntity))
            {
                return false;
            }

            return isEnemyProjectile
                ? PlayerLookup.HasComponent(hitEntity)
                : EnemyLookup.HasComponent(hitEntity);
        }
    }
}
