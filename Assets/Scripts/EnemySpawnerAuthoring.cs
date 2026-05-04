using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace TMG.Survivors
{
    public enum AbilitySpawnType : byte
    {
        AerialArtillery,
        SwiftSwarm,
        VolatileVanguard,
        RollingHazard,
        StasisOverlord,
        LightningStriker,
        HeavyLeaper
    }

    public struct AbilitySpawnData : IComponentData
    {
        public Entity DefaultEnemyPrefab;
        public Entity AerialArtilleryPrefab;
        public Entity SwiftSwarmPrefab;
        public Entity VolatileVanguardPrefab;
        public Entity RollingHazardPrefab;
        public Entity StasisOverlordPrefab;
        public Entity LightningStrikerPrefab;
        public Entity HeavyLeaperPrefab;
        public Entity EnemyProjectilePrefab;

        public int AerialArtilleryCount;
        public int SwiftSwarmCount;
        public int VolatileVanguardCount;
        public int HeavyLeaperCount;

        public float SpawnDistance;
        public uint RandomSeed;
    }

    public struct AbilitySpawnState : IComponentData
    {
        public Random Random;
    }

    public struct AbilitySpawnRequest : IComponentData
    {
        public AbilitySpawnType Type;
    }
    
    public class EnemySpawnerAuthoring : MonoBehaviour
    {
        [Header("Fallbacks")]
        public GameObject EnemyPrefab;
        public GameObject ReaperPrefab;
        public float SpawnDistance = 25f;
        public uint RandomSeed = 1337;

        [Header("Ability Prefabs")]
        public GameObject AerialArtilleryPrefab;
        public GameObject SwiftSwarmPrefab;
        public GameObject VolatileVanguardPrefab;
        public GameObject RollingHazardPrefab;
        public GameObject StasisOverlordPrefab;
        public GameObject LightningStrikerPrefab;
        public GameObject HeavyLeaperPrefab;
        public GameObject EnemyProjectilePrefab;

        [Header("Ability Counts")]
        public int AerialArtilleryCount = 5;
        public int SwiftSwarmCount = 12;
        public int VolatileVanguardCount = 5;
        public int HeavyLeaperCount = 5;
        
        private class Baker : Baker<EnemySpawnerAuthoring>
        {
            public override void Bake(EnemySpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var defaultEnemyPrefab = GetOptionalEntity(authoring.EnemyPrefab);
                var reaperPrefab = GetOptionalEntity(authoring.ReaperPrefab);

                AddComponent(entity, new AbilitySpawnData
                {
                    DefaultEnemyPrefab = defaultEnemyPrefab,
                    AerialArtilleryPrefab = GetPrefabOrFallback(authoring.AerialArtilleryPrefab, defaultEnemyPrefab),
                    SwiftSwarmPrefab = GetPrefabOrFallback(authoring.SwiftSwarmPrefab, defaultEnemyPrefab),
                    VolatileVanguardPrefab = GetPrefabOrFallback(authoring.VolatileVanguardPrefab, defaultEnemyPrefab),
                    RollingHazardPrefab = GetOptionalEntity(authoring.RollingHazardPrefab),
                    StasisOverlordPrefab = GetPrefabOrFallback(authoring.StasisOverlordPrefab, reaperPrefab),
                    LightningStrikerPrefab = GetOptionalEntity(authoring.LightningStrikerPrefab),
                    HeavyLeaperPrefab = GetPrefabOrFallback(authoring.HeavyLeaperPrefab, defaultEnemyPrefab),
                    EnemyProjectilePrefab = GetOptionalEntity(authoring.EnemyProjectilePrefab),
                    AerialArtilleryCount = math.max(1, authoring.AerialArtilleryCount),
                    SwiftSwarmCount = math.max(1, authoring.SwiftSwarmCount),
                    VolatileVanguardCount = math.max(1, authoring.VolatileVanguardCount),
                    HeavyLeaperCount = math.max(1, authoring.HeavyLeaperCount),
                    SpawnDistance = math.max(1f, authoring.SpawnDistance),
                    RandomSeed = authoring.RandomSeed == 0 ? 1u : authoring.RandomSeed
                });

                AddComponent(entity, new AbilitySpawnState
                {
                    Random = Random.CreateFromIndex(authoring.RandomSeed == 0 ? 1u : authoring.RandomSeed)
                });
            }

            private Entity GetOptionalEntity(GameObject prefab)
            {
                return prefab == null ? Entity.Null : GetEntity(prefab, TransformUsageFlags.Dynamic);
            }

            private Entity GetPrefabOrFallback(GameObject prefab, Entity fallback)
            {
                return prefab == null ? fallback : GetEntity(prefab, TransformUsageFlags.Dynamic);
            }
        }
    }

    public partial class AbilityInputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<AbilitySpawnData>();
        }

        protected override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                CreateSpawnRequest(AbilitySpawnType.AerialArtillery);
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                CreateSpawnRequest(AbilitySpawnType.SwiftSwarm);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                CreateSpawnRequest(AbilitySpawnType.VolatileVanguard);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                CreateSpawnRequest(AbilitySpawnType.RollingHazard);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                CreateSpawnRequest(AbilitySpawnType.StasisOverlord);
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                CreateSpawnRequest(AbilitySpawnType.LightningStriker);
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                CreateSpawnRequest(AbilitySpawnType.HeavyLeaper);
            }
        }

        private void CreateSpawnRequest(AbilitySpawnType spawnType)
        {
            var request = EntityManager.CreateEntity();
            EntityManager.AddComponentData(request, new AbilitySpawnRequest
            {
                Type = spawnType
            });
        }
    }

    public partial struct AbilitySpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<AbilitySpawnData>();
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

            foreach (var (spawnData, spawnState) in SystemAPI.Query<AbilitySpawnData, RefRW<AbilitySpawnState>>())
            {
                foreach (var (request, requestEntity) in SystemAPI.Query<AbilitySpawnRequest>().WithEntityAccess())
                {
                    SpawnAbility(ref state, ecb, spawnData, spawnState, request.Type, playerPosition);
                    ecb.DestroyEntity(requestEntity);
                }
            }
        }

        private void SpawnAbility(
            ref SystemState state,
            EntityCommandBuffer ecb,
            AbilitySpawnData spawnData,
            RefRW<AbilitySpawnState> spawnState,
            AbilitySpawnType spawnType,
            float3 playerPosition)
        {
            var prefab = GetPrefab(spawnData, spawnType);
            if (prefab == Entity.Null)
            {
                return;
            }

            var count = GetSpawnCount(spawnData, spawnType);
            for (var i = 0; i < count; i++)
            {
                var entity = ecb.Instantiate(prefab);
                ApplyBehaviorTag(ref state, ecb, spawnData, prefab, entity, spawnType);
                ecb.SetComponent(entity, LocalTransform.FromPosition(GetSpawnPosition(spawnData, spawnState, spawnType, playerPosition, i, count)));
            }
        }

        private Entity GetPrefab(AbilitySpawnData spawnData, AbilitySpawnType spawnType)
        {
            return spawnType switch
            {
                AbilitySpawnType.AerialArtillery => spawnData.AerialArtilleryPrefab,
                AbilitySpawnType.SwiftSwarm => spawnData.SwiftSwarmPrefab,
                AbilitySpawnType.VolatileVanguard => spawnData.VolatileVanguardPrefab,
                AbilitySpawnType.RollingHazard => spawnData.RollingHazardPrefab,
                AbilitySpawnType.StasisOverlord => spawnData.StasisOverlordPrefab,
                AbilitySpawnType.LightningStriker => spawnData.LightningStrikerPrefab,
                AbilitySpawnType.HeavyLeaper => spawnData.HeavyLeaperPrefab,
                _ => Entity.Null
            };
        }

        private int GetSpawnCount(AbilitySpawnData spawnData, AbilitySpawnType spawnType)
        {
            return spawnType switch
            {
                AbilitySpawnType.AerialArtillery => spawnData.AerialArtilleryCount,
                AbilitySpawnType.SwiftSwarm => spawnData.SwiftSwarmCount,
                AbilitySpawnType.VolatileVanguard => spawnData.VolatileVanguardCount,
                AbilitySpawnType.HeavyLeaper => spawnData.HeavyLeaperCount,
                _ => 1
            };
        }

        private float3 GetSpawnPosition(
            AbilitySpawnData spawnData,
            RefRW<AbilitySpawnState> spawnState,
            AbilitySpawnType spawnType,
            float3 playerPosition,
            int index,
            int count)
        {
            if (spawnType == AbilitySpawnType.RollingHazard ||
                spawnType == AbilitySpawnType.LightningStriker)
            {
                var yOffset = spawnState.ValueRW.Random.NextFloat(-8f, 8f);
                return playerPosition + new float3(-spawnData.SpawnDistance, yOffset, 0f);
            }

            var angle = count == 1
                ? spawnState.ValueRW.Random.NextFloat(0f, math.TAU)
                : (math.TAU * index / count) + spawnState.ValueRW.Random.NextFloat(-0.2f, 0.2f);

            var direction = new float3(math.sin(angle), math.cos(angle), 0f);
            return playerPosition + direction * spawnData.SpawnDistance;
        }

        private void ApplyBehaviorTag(
            ref SystemState state,
            EntityCommandBuffer ecb,
            AbilitySpawnData spawnData,
            Entity prefab,
            Entity entity,
            AbilitySpawnType spawnType)
        {
            RemoveBehaviorTagIfPresent<ChasePlayerTag>(ref state, ecb, prefab, entity);
            RemoveBehaviorTagIfPresent<MeleeChaserTag>(ref state, ecb, prefab, entity);
            RemoveBehaviorTagIfPresent<SwiftSwarmTag>(ref state, ecb, prefab, entity);
            RemoveBehaviorTagIfPresent<AerialArtilleryTag>(ref state, ecb, prefab, entity);
            RemoveBehaviorTagIfPresent<VolatileVanguardTag>(ref state, ecb, prefab, entity);
            RemoveBehaviorTagIfPresent<HeavyLeaperTag>(ref state, ecb, prefab, entity);
            RemoveBehaviorTagIfPresent<StasisOverlordTag>(ref state, ecb, prefab, entity);
            RemoveBehaviorTagIfPresent<RollingHazardTag>(ref state, ecb, prefab, entity);
            RemoveBehaviorTagIfPresent<LightningStrikerTag>(ref state, ecb, prefab, entity);

            switch (spawnType)
            {
                case AbilitySpawnType.AerialArtillery:
                    ecb.AddComponent<AerialArtilleryTag>(entity);
                    AddOrPatchAerialData(ref state, ecb, spawnData, prefab, entity);
                    break;
                case AbilitySpawnType.SwiftSwarm:
                    ecb.AddComponent<SwiftSwarmTag>(entity);
                    ecb.AddComponent<ChasePlayerTag>(entity);
                    break;
                case AbilitySpawnType.VolatileVanguard:
                    ecb.AddComponent<VolatileVanguardTag>(entity);
                    AddVolatileDataIfMissing(ref state, ecb, prefab, entity);
                    break;
                case AbilitySpawnType.RollingHazard:
                    ecb.AddComponent<RollingHazardTag>(entity);
                    break;
                case AbilitySpawnType.StasisOverlord:
                    ecb.AddComponent<StasisOverlordTag>(entity);
                    break;
                case AbilitySpawnType.LightningStriker:
                    ecb.AddComponent<LightningStrikerTag>(entity);
                    break;
                case AbilitySpawnType.HeavyLeaper:
                    ecb.AddComponent<HeavyLeaperTag>(entity);
                    AddHeavyLeaperDataIfMissing(ref state, ecb, prefab, entity);
                    break;
            }
        }

        private void AddOrPatchAerialData(
            ref SystemState state,
            EntityCommandBuffer ecb,
            AbilitySpawnData spawnData,
            Entity prefab,
            Entity entity)
        {
            var projectilePrefab = spawnData.EnemyProjectilePrefab;
            if (state.EntityManager.HasComponent<AerialArtilleryData>(prefab))
            {
                if (projectilePrefab != Entity.Null)
                {
                    var aerialData = state.EntityManager.GetComponentData<AerialArtilleryData>(prefab);
                    aerialData.ProjectilePrefab = projectilePrefab;
                    ecb.SetComponent(entity, aerialData);
                }
            }
            else
            {
                ecb.AddComponent(entity, new AerialArtilleryData
                {
                    ProjectilePrefab = projectilePrefab,
                    MinimumDistance = 7f,
                    PreferredDistance = 10f,
                    ShootCooldown = 1.25f
                });
            }

            if (!state.EntityManager.HasComponent<AerialArtilleryState>(prefab))
            {
                ecb.AddComponent(entity, new AerialArtilleryState
                {
                    ShootTimer = 0f
                });
            }
        }

        private void AddVolatileDataIfMissing(
            ref SystemState state,
            EntityCommandBuffer ecb,
            Entity prefab,
            Entity entity)
        {
            if (!state.EntityManager.HasComponent<VolatileVanguardData>(prefab))
            {
                ecb.AddComponent(entity, new VolatileVanguardData
                {
                    CountdownTime = 5f,
                    ExplosionRadius = 3f,
                    ExplosionDamage = 25
                });
            }

            if (!state.EntityManager.HasComponent<VolatileVanguardState>(prefab))
            {
                ecb.AddComponent(entity, new VolatileVanguardState
                {
                    RemainingTime = 5f,
                    BlinkTimer = 0f,
                    BaseScale = 1f,
                    BlinkExpanded = false
                });
            }
        }

        private void AddHeavyLeaperDataIfMissing(
            ref SystemState state,
            EntityCommandBuffer ecb,
            Entity prefab,
            Entity entity)
        {
            if (!state.EntityManager.HasComponent<HeavyLeaperData>(prefab))
            {
                ecb.AddComponent(entity, new HeavyLeaperData
                {
                    LeapInterval = 1.5f,
                    LeapDuration = 0.65f,
                    SlamRadius = 3.5f,
                    SlamDamage = 20
                });
            }

            if (!state.EntityManager.HasComponent<HeavyLeaperState>(prefab))
            {
                ecb.AddComponent(entity, new HeavyLeaperState
                {
                    Phase = HeavyLeaperPhase.Waiting,
                    Timer = 1.5f,
                    StartPosition = float3.zero,
                    TargetPosition = float3.zero,
                    BaseScale = 1f
                });
            }
        }

        private void RemoveBehaviorTagIfPresent<T>(
            ref SystemState state,
            EntityCommandBuffer ecb,
            Entity prefab,
            Entity entity)
            where T : unmanaged, IComponentData
        {
            if (state.EntityManager.HasComponent<T>(prefab))
            {
                ecb.RemoveComponent<T>(entity);
            }
        }
    }
}
