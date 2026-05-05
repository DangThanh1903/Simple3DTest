using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace TMG.Survivors
{
    public struct EnemyTag : IComponentData {}
    public struct ChasePlayerTag : IComponentData {}

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
    public struct InitializeRollingHazardFlag : IComponentData {}
    public struct LightningStrikerTag : IComponentData {}
    public struct EnemyProjectileTag : IComponentData {}
    public struct InitializeEnemyProjectileFlag : IComponentData {}

    public struct RollingHazardData : IComponentData
    {
        public float Lifetime;
        public float MoveSpeed;
        public float KnockbackSpeed;
        public float KnockbackDuration;
    }

    public struct RollingHazardState : IComponentData
    {
        public float RemainingTime;
        public float2 Direction;
    }

    public struct PlayerKnockback : IComponentData
    {
        public float RemainingTime;
        public float2 Velocity;
    }

    public struct AerialArtilleryData : IComponentData
    {
        public Entity ProjectilePrefab;
        public float MinimumDistance;
        public float PreferredDistance;
        public float ShootCooldown;
    }

    public struct AerialArtilleryState : IComponentData
    {
        public float ShootTimer;
    }

    public struct VolatileVanguardData : IComponentData
    {
        public float CountdownTime;
        public float ExplosionRadius;
        public int ExplosionDamage;
    }

    public struct VolatileVanguardState : IComponentData
    {
        public float RemainingTime;
        public float BlinkTimer;
        public float BaseScale;
        public bool BlinkExpanded;
    }

    public enum HeavyLeaperPhase : byte
    {
        Waiting,
        Leaping
    }

    public struct HeavyLeaperData : IComponentData
    {
        public float LeapInterval;
        public float LeapDuration;
        public float SlamRadius;
        public int SlamDamage;
    }

    public struct HeavyLeaperState : IComponentData
    {
        public HeavyLeaperPhase Phase;
        public float Timer;
        public float3 StartPosition;
        public float3 TargetPosition;
        public float BaseScale;
    }

    public struct StasisOverlordData : IComponentData
    {
        public float FreezeRange;
        public float FreezeDuration;
        public float CastCooldown;
    }

    public struct StasisOverlordState : IComponentData
    {
        public float CastTimer;
        public float BaseScale;
    }

    public enum LightningStrikerPhase : byte
    {
        Flying,
        Locking,
        Striking
    }

    public struct LightningStrikerData : IComponentData
    {
        public float FlyDuration;
        public float FlySpeed;
        public float LockDuration;
        public float StrikeRadius;
        public int StrikeDamage;
    }

    public struct LightningStrikerState : IComponentData
    {
        public LightningStrikerPhase Phase;
        public float Timer;
        public float3 LockedTargetPosition;
        public float BaseScale;
    }

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

        [Header("Aerial Artillery")]
        public GameObject AerialProjectilePrefab;
        public float AerialMinimumDistance = 7f;
        public float AerialPreferredDistance = 10f;
        public float AerialShootCooldown = 1.25f;

        [Header("Volatile Vanguard")]
        public float VolatileCountdownTime = 5f;
        public float VolatileExplosionRadius = 3f;
        public int VolatileExplosionDamage = 25;

        [Header("Heavy Leaper")]
        public float HeavyLeapInterval = 1.5f;
        public float HeavyLeapDuration = 0.65f;
        public float HeavySlamRadius = 3.5f;
        public int HeavySlamDamage = 20;

        [Header("Stasis Overlord")]
        public float StasisFreezeRange = 8f;
        public float StasisFreezeDuration = 2f;
        public float StasisCastCooldown = 4f;
        
        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTag>(entity);
                AddBehaviorTag(entity, authoring);
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

            private void AddBehaviorTag(Entity entity, EnemyAuthoring authoring)
            {
                switch (authoring.BehaviorType)
                {
                    case EnemyBehaviorType.MeleeChaser:
                        AddComponent<MeleeChaserTag>(entity);
                        AddComponent<ChasePlayerTag>(entity);
                        break;
                    case EnemyBehaviorType.SwiftSwarm:
                        AddComponent<SwiftSwarmTag>(entity);
                        AddComponent<ChasePlayerTag>(entity);
                        break;
                    case EnemyBehaviorType.AerialArtillery:
                        AddComponent<AerialArtilleryTag>(entity);
                        AddComponent(entity, new AerialArtilleryData
                        {
                            ProjectilePrefab = GetOptionalEntity(authoring.AerialProjectilePrefab),
                            MinimumDistance = math.max(0f, authoring.AerialMinimumDistance),
                            PreferredDistance = math.max(authoring.AerialMinimumDistance, authoring.AerialPreferredDistance),
                            ShootCooldown = math.max(0.1f, authoring.AerialShootCooldown)
                        });
                        AddComponent(entity, new AerialArtilleryState
                        {
                            ShootTimer = 0f
                        });
                        break;
                    case EnemyBehaviorType.VolatileVanguard:
                        AddComponent<VolatileVanguardTag>(entity);
                        AddComponent<ChasePlayerTag>(entity);
                        AddComponent(entity, new VolatileVanguardData
                        {
                            CountdownTime = math.max(0.1f, authoring.VolatileCountdownTime),
                            ExplosionRadius = math.max(0.1f, authoring.VolatileExplosionRadius),
                            ExplosionDamage = math.max(1, authoring.VolatileExplosionDamage)
                        });
                        AddComponent(entity, new VolatileVanguardState
                        {
                            RemainingTime = math.max(0.1f, authoring.VolatileCountdownTime),
                            BlinkTimer = 0f,
                            BaseScale = math.max(0.1f, authoring.transform.localScale.x),
                            BlinkExpanded = false
                        });
                        break;
                    case EnemyBehaviorType.HeavyLeaper:
                        AddComponent<HeavyLeaperTag>(entity);
                        AddComponent(entity, new HeavyLeaperData
                        {
                            LeapInterval = math.max(0.1f, authoring.HeavyLeapInterval),
                            LeapDuration = math.max(0.1f, authoring.HeavyLeapDuration),
                            SlamRadius = math.max(0.1f, authoring.HeavySlamRadius),
                            SlamDamage = math.max(1, authoring.HeavySlamDamage)
                        });
                        AddComponent(entity, new HeavyLeaperState
                        {
                            Phase = HeavyLeaperPhase.Waiting,
                            Timer = math.max(0.1f, authoring.HeavyLeapInterval),
                            StartPosition = float3.zero,
                            TargetPosition = float3.zero,
                            BaseScale = math.max(0.1f, authoring.transform.localScale.x)
                        });
                        break;
                    case EnemyBehaviorType.StasisOverlord:
                        AddComponent<StasisOverlordTag>(entity);
                        AddComponent<ChasePlayerTag>(entity);
                        AddComponent(entity, new StasisOverlordData
                        {
                            FreezeRange = math.max(0.1f, authoring.StasisFreezeRange),
                            FreezeDuration = math.max(0.1f, authoring.StasisFreezeDuration),
                            CastCooldown = math.max(0.1f, authoring.StasisCastCooldown)
                        });
                        AddComponent(entity, new StasisOverlordState
                        {
                            CastTimer = 1f,
                            BaseScale = math.max(0.1f, authoring.transform.localScale.x)
                        });
                        break;
                }
            }

            private Entity GetOptionalEntity(GameObject prefab)
            {
                return prefab == null ? Entity.Null : GetEntity(prefab, TransformUsageFlags.Dynamic);
            }
        }
    }

    public partial struct ChasePlayerMoveSystem : ISystem
    {
        private EntityQuery _chaseQuery;
        private NativeList<Entity> _entities;
        private NativeList<LocalTransform> _transforms;
        private NativeList<float2> _positions;
        private NativeList<SpatialHashAndIndex> _hashAndIndices;
        private const float SpatialHashCellSize = 1.75f;
        private const float SeparationRadius = 1.25f;
        private const float SeparationWeight = 1.35f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            _chaseQuery = SystemAPI.QueryBuilder()
                .WithAll<ChasePlayerTag, CharacterMoveDirection, LocalTransform>()
                .Build();
            _entities = new NativeList<Entity>(128, Allocator.Persistent);
            _transforms = new NativeList<LocalTransform>(128, Allocator.Persistent);
            _positions = new NativeList<float2>(128, Allocator.Persistent);
            _hashAndIndices = new NativeList<SpatialHashAndIndex>(128, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_entities.IsCreated)
            {
                _entities.Dispose();
            }

            if (_transforms.IsCreated)
            {
                _transforms.Dispose();
            }

            if (_positions.IsCreated)
            {
                _positions.Dispose();
            }

            if (_hashAndIndices.IsCreated)
            {
                _hashAndIndices.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityCount = _chaseQuery.CalculateEntityCount();
            if (entityCount == 0)
            {
                return;
            }

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position.xy;
            EnsureCapacity(entityCount);
            state.Dependency.Complete();
            _entities.Clear();
            _transforms.Clear();

            foreach (var (transform, entity) in SystemAPI
                         .Query<LocalTransform>()
                         .WithAll<ChasePlayerTag, CharacterMoveDirection>()
                         .WithEntityAccess())
            {
                _entities.Add(entity);
                _transforms.Add(transform);
            }

            var chaseCount = _entities.Length;
            if (chaseCount == 0)
            {
                return;
            }

            _positions.ResizeUninitialized(chaseCount);
            _hashAndIndices.ResizeUninitialized(chaseCount);

            var buildHashJob = new BuildChaseSpatialHashJob
            {
                Transforms = _transforms.AsArray(),
                Positions = _positions.AsArray(),
                HashAndIndices = _hashAndIndices.AsArray(),
                CellSize = SpatialHashCellSize
            };

            var buildHandle = buildHashJob.Schedule(chaseCount, 64, state.Dependency);
            var sortHandle = new SortChaseSpatialHashJob
            {
                HashAndIndices = _hashAndIndices.AsArray()
            }.Schedule(buildHandle);

            var moveJob = new ChasePlayerSpatialMoveJob
            {
                Entities = _entities.AsArray(),
                Positions = _positions.AsArray(),
                HashAndIndices = _hashAndIndices.AsArray(),
                DirectionLookup = SystemAPI.GetComponentLookup<CharacterMoveDirection>(),
                PlayerPosition = playerPosition,
                CellSize = SpatialHashCellSize,
                SeparationRadius = SeparationRadius,
                SeparationWeight = SeparationWeight
            };

            var moveHandle = moveJob.Schedule(chaseCount, 64, sortHandle);
            state.Dependency = moveHandle;
        }

        private void EnsureCapacity(int entityCount)
        {
            if (_entities.Capacity < entityCount)
            {
                _entities.Capacity = entityCount;
            }

            if (_transforms.Capacity < entityCount)
            {
                _transforms.Capacity = entityCount;
            }

            if (_positions.Capacity < entityCount)
            {
                _positions.Capacity = entityCount;
            }

            if (_hashAndIndices.Capacity < entityCount)
            {
                _hashAndIndices.Capacity = entityCount;
            }
        }
    }

    [BurstCompile]
    public struct BuildChaseSpatialHashJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<LocalTransform> Transforms;
        public NativeArray<float2> Positions;
        public NativeArray<SpatialHashAndIndex> HashAndIndices;
        public float CellSize;

        public void Execute(int index)
        {
            var position = Transforms[index].Position.xy;
            Positions[index] = position;
            HashAndIndices[index] = new SpatialHashAndIndex
            {
                Hash = SpatialHashUtility.Hash(SpatialHashUtility.GridPosition(position, CellSize)),
                Index = index
            };
        }
    }

    [BurstCompile]
    public struct SortChaseSpatialHashJob : IJob
    {
        public NativeArray<SpatialHashAndIndex> HashAndIndices;

        public void Execute()
        {
            HashAndIndices.Sort();
        }
    }

    [BurstCompile]
    public struct ChasePlayerSpatialMoveJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<SpatialHashAndIndex> HashAndIndices;
        [NativeDisableParallelForRestriction] public ComponentLookup<CharacterMoveDirection> DirectionLookup;
        public float2 PlayerPosition;
        public float CellSize;
        public float SeparationRadius;
        public float SeparationWeight;

        public void Execute(int index)
        {
            var position = Positions[index];
            var toPlayer = PlayerPosition - position;
            var chaseDirection = math.lengthsq(toPlayer) > 0.0001f ? math.normalize(toPlayer) : float2.zero;
            var separation = GetSeparation(index, position);
            var desiredDirection = chaseDirection + separation * SeparationWeight;

            DirectionLookup[Entities[index]] = new CharacterMoveDirection
            {
                Value = math.lengthsq(desiredDirection) > 0.0001f ? math.normalize(desiredDirection) : float2.zero
            };
        }

        private float2 GetSeparation(int selfIndex, float2 position)
        {
            var separation = float2.zero;
            var radiusSq = SeparationRadius * SeparationRadius;
            var minGridPos = SpatialHashUtility.GridPosition(position - SeparationRadius, CellSize);
            var maxGridPos = SpatialHashUtility.GridPosition(position + SeparationRadius, CellSize);

            for (var x = minGridPos.x; x <= maxGridPos.x; x++)
            {
                for (var y = minGridPos.y; y <= maxGridPos.y; y++)
                {
                    var hash = SpatialHashUtility.Hash(new int2(x, y));
                    var startIndex = SpatialHashUtility.BinarySearchFirst(HashAndIndices, hash);
                    if (startIndex < 0)
                    {
                        continue;
                    }

                    for (var sortedIndex = startIndex;
                         sortedIndex < HashAndIndices.Length && HashAndIndices[sortedIndex].Hash == hash;
                         sortedIndex++)
                    {
                        var neighborIndex = HashAndIndices[sortedIndex].Index;
                        if (neighborIndex == selfIndex)
                        {
                            continue;
                        }

                        var awayFromNeighbor = position - Positions[neighborIndex];
                        var distanceSq = math.lengthsq(awayFromNeighbor);
                        if (distanceSq > 0.0001f && distanceSq < radiusSq)
                        {
                            separation += awayFromNeighbor / distanceSq;
                        }
                    }
                }
            }

            return separation;
        }
    }

    public struct SpatialHashAndIndex : System.IComparable<SpatialHashAndIndex>
    {
        public int Hash;
        public int Index;

        public int CompareTo(SpatialHashAndIndex other)
        {
            return Hash.CompareTo(other.Hash);
        }
    }

    public static class SpatialHashUtility
    {
        public static int2 GridPosition(float2 position, float cellSize)
        {
            return new int2(math.floor(position / cellSize));
        }

        public static int Hash(int2 gridPosition)
        {
            unchecked
            {
                return gridPosition.x * 73856093 ^ gridPosition.y * 19349663;
            }
        }

        public static int BinarySearchFirst(NativeArray<SpatialHashAndIndex> sortedHashAndIndices, int hash)
        {
            var left = 0;
            var right = sortedHashAndIndices.Length - 1;
            var result = -1;

            while (left <= right)
            {
                var mid = (left + right) / 2;
                var midHash = sortedHashAndIndices[mid].Hash;
                if (midHash == hash)
                {
                    result = mid;
                    right = mid - 1;
                }
                else if (midHash < hash)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return result;
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
