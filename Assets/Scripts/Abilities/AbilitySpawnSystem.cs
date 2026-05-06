using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TMG.Survivors
{
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
                AbilitySpawnBehaviorFactory.Apply(ref state, ecb, spawnData, prefab, entity, spawnType);
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
    }
}
