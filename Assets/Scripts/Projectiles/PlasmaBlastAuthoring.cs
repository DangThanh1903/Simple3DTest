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
        public float Lifetime;
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
                    AttackDamage = authoring.AttackDamage,
                    Lifetime = authoring.DestroyAfterTime
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
        private const float ProjectileRadius = 0.5f;
        private const float PlayerHitRadius = 0.6f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PlayerTag>();
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
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var colliderCastJob = new PlasmaBlastColliderCastMoveJob
            {
                DeltaTime = deltaTime,
                PlayerEntity = playerEntity,
                PlayerPosition = playerPosition,
                EnemyProjectileHitRadius = ProjectileRadius + PlayerHitRadius,
                PhysicsWorld = physicsWorld,
                EnemyProjectileLookup = SystemAPI.GetComponentLookup<EnemyProjectileTag>(true),
                EnemyLookup = SystemAPI.GetComponentLookup<EnemyTag>(true),
                DestroyEntityLookup = SystemAPI.GetComponentLookup<DestroyEntityFlag>(true),
                CommandBuffer = ecb.AsParallelWriter()
            };

            state.Dependency = colliderCastJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    public partial struct PlasmaBlastColliderCastMoveJob : IJobEntity
    {
        private const float OverlapTolerance = 0.01f;

        [ReadOnly] public PhysicsWorld PhysicsWorld;
        [ReadOnly] public ComponentLookup<EnemyProjectileTag> EnemyProjectileLookup;
        [ReadOnly] public ComponentLookup<EnemyTag> EnemyLookup;
        [ReadOnly] public ComponentLookup<DestroyEntityFlag> DestroyEntityLookup;
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        public float DeltaTime;
        public Entity PlayerEntity;
        public float3 PlayerPosition;
        public float EnemyProjectileHitRadius;

        private void Execute(
            [EntityIndexInQuery] int sortKey,
            Entity entity,
            ref LocalTransform transform,
            in PhysicsCollider collider,
            in PlasmaBlastData data)
        {
            var start = transform.Position;
            var end = start + transform.Right() * data.MoveSpeed * DeltaTime;
            var isEnemyProjectile = EnemyProjectileLookup.HasComponent(entity);
            if (isEnemyProjectile &&
                TryGetSegmentCircleHit(start.xy, end.xy, PlayerPosition.xy, EnemyProjectileHitRadius, out var hitPoint))
            {
                ApplyHit(sortKey, entity, PlayerEntity, data.AttackDamage, new float3(hitPoint, start.z), ref transform);
                return;
            }

            if (isEnemyProjectile)
            {
                transform.Position = end;
                return;
            }

            var targetCollector = CreateTargetCollector(entity);

            var overlapInput = new ColliderDistanceInput(
                collider.Value,
                OverlapTolerance,
                new RigidTransform(transform.Rotation, start),
                transform.Scale);

            if (PhysicsWorld.CalculateDistance(overlapInput, ref targetCollector) &&
                targetCollector.NumHits > 0)
            {
                ApplyHit(sortKey, entity, targetCollector.DistanceHit.Entity, data.AttackDamage, start, ref transform);
                return;
            }

            targetCollector = CreateTargetCollector(entity);
            var castInput = new ColliderCastInput(collider.Value, start, end, transform.Rotation, transform.Scale);

            if (PhysicsWorld.CastCollider(castInput, ref targetCollector) &&
                targetCollector.NumHits > 0)
            {
                var hit = targetCollector.ColliderCastHit;
                ApplyHit(sortKey, entity, hit.Entity, data.AttackDamage, hit.Position, ref transform);
                return;
            }

            transform.Position = end;
        }

        private static bool TryGetSegmentCircleHit(
            float2 start,
            float2 end,
            float2 center,
            float radius,
            out float2 hitPoint)
        {
            var segment = end - start;
            var segmentLengthSq = math.lengthsq(segment);
            if (segmentLengthSq <= float.Epsilon)
            {
                hitPoint = start;
                return math.distancesq(start, center) <= radius * radius;
            }

            var centerToStart = start - center;
            var b = 2f * math.dot(centerToStart, segment);
            var c = math.lengthsq(centerToStart) - radius * radius;
            if (c <= 0f)
            {
                hitPoint = start;
                return true;
            }

            var discriminant = b * b - 4f * segmentLengthSq * c;
            if (discriminant < 0f)
            {
                hitPoint = default;
                return false;
            }

            var t = (-b - math.sqrt(discriminant)) / (2f * segmentLengthSq);
            if (t < 0f || t > 1f)
            {
                hitPoint = default;
                return false;
            }

            hitPoint = start + segment * t;
            return true;
        }

        private TargetHitCollector CreateTargetCollector(Entity projectileEntity)
        {
            return new TargetHitCollector
            {
                MaxFraction = 1f,
                ProjectileEntity = projectileEntity,
                EnemyLookup = EnemyLookup,
                DestroyEntityLookup = DestroyEntityLookup
            };
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

    }

    public struct TargetHitCollector :
        ICollector<ColliderCastHit>,
        ICollector<DistanceHit>
    {
        [ReadOnly] public ComponentLookup<EnemyTag> EnemyLookup;
        [ReadOnly] public ComponentLookup<DestroyEntityFlag> DestroyEntityLookup;

        public Entity ProjectileEntity;
        public float MaxFraction { get; set; }
        public int NumHits { get; private set; }
        public ColliderCastHit ColliderCastHit { get; private set; }
        public DistanceHit DistanceHit { get; private set; }
        public bool EarlyOutOnFirstHit => false;

        public bool AddHit(ColliderCastHit hit)
        {
            if (!IsValidTarget(hit.Entity) || hit.Fraction > MaxFraction)
            {
                return false;
            }

            MaxFraction = hit.Fraction;
            ColliderCastHit = hit;
            NumHits = 1;
            return true;
        }

        public bool AddHit(DistanceHit hit)
        {
            if (!IsValidTarget(hit.Entity) || hit.Fraction > MaxFraction)
            {
                return false;
            }

            MaxFraction = hit.Fraction;
            DistanceHit = hit;
            NumHits = 1;
            return true;
        }

        private bool IsValidTarget(Entity hitEntity)
        {
            if (hitEntity == ProjectileEntity ||
                !DestroyEntityLookup.HasComponent(hitEntity))
            {
                return false;
            }

            return EnemyLookup.HasComponent(hitEntity);
        }
    }
}
