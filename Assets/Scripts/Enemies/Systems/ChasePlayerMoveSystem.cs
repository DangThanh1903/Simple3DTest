using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace TMG.Survivors
{
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

            state.Dependency = moveJob.Schedule(chaseCount, 64, sortHandle);
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
}
