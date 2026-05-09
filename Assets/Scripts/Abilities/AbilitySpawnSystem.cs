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
            state.RequireForUpdate<AbilitySpawnDefinition>();
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

            foreach (var (spawnData, spawnDefinitions, spawnState) in SystemAPI.Query<AbilitySpawnData, DynamicBuffer<AbilitySpawnDefinition>, RefRW<AbilitySpawnState>>())
            {
                foreach (var (request, requestEntity) in SystemAPI.Query<AbilitySpawnRequest>().WithEntityAccess())
                {
                    if (TryGetSpawnDefinition(spawnDefinitions, request.Type, out var definition))
                    {
                        SpawnAbility(ecb, spawnData, spawnState, definition, playerPosition);
                    }

                    ecb.DestroyEntity(requestEntity);
                }
            }
        }

        private void SpawnAbility(
            EntityCommandBuffer ecb,
            AbilitySpawnData spawnData,
            RefRW<AbilitySpawnState> spawnState,
            AbilitySpawnDefinition definition,
            float3 playerPosition)
        {
            if (definition.Prefab == Entity.Null)
            {
                return;
            }

            for (var i = 0; i < definition.Count; i++)
            {
                var entity = ecb.Instantiate(definition.Prefab);
                ecb.SetComponent(entity, LocalTransform.FromPosition(GetSpawnPosition(spawnData, spawnState, definition, playerPosition, i)));
            }
        }

        private bool TryGetSpawnDefinition(
            DynamicBuffer<AbilitySpawnDefinition> spawnDefinitions,
            AbilitySpawnType spawnType,
            out AbilitySpawnDefinition definition)
        {
            foreach (var spawnDefinition in spawnDefinitions)
            {
                if (spawnDefinition.Type != spawnType)
                {
                    continue;
                }

                definition = spawnDefinition;
                return true;
            }

            definition = default;
            return false;
        }

        private float3 GetSpawnPosition(
            AbilitySpawnData spawnData,
            RefRW<AbilitySpawnState> spawnState,
            AbilitySpawnDefinition definition,
            float3 playerPosition,
            int index)
        {
            if (definition.Pattern == AbilitySpawnPattern.LeftOfPlayerRandomY)
            {
                var yOffset = spawnState.ValueRW.Random.NextFloat(-8f, 8f);
                return playerPosition + new float3(-spawnData.SpawnDistance, yOffset, 0f);
            }

            var angle = definition.Count == 1
                ? spawnState.ValueRW.Random.NextFloat(0f, math.TAU)
                : (math.TAU * index / definition.Count) + spawnState.ValueRW.Random.NextFloat(-0.2f, 0.2f);

            var direction = new float3(math.sin(angle), math.cos(angle), 0f);
            return playerPosition + direction * spawnData.SpawnDistance;
        }
    }
}
