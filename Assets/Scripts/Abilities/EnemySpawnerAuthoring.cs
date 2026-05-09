using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
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

    public enum AbilitySpawnPattern : byte
    {
        AroundPlayer,
        LeftOfPlayerRandomY
    }

    public struct AbilitySpawnData : IComponentData
    {
        public float SpawnDistance;
    }

    public struct AbilitySpawnDefinition : IBufferElementData
    {
        public AbilitySpawnType Type;
        public Entity Prefab;
        public int Count;
        public AbilitySpawnPattern Pattern;
    }

    [System.Serializable]
    public struct AbilitySpawnDefinitionAuthoring
    {
        public AbilitySpawnType Type;
        public GameObject Prefab;
        public int Count;
        public AbilitySpawnPattern Pattern;
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
        [Header("Spawn Settings")]
        public float SpawnDistance = 25f;
        public uint RandomSeed = 1337;

        public AbilitySpawnDefinitionAuthoring[] AbilitySpawnDefinitions;
        
        private class Baker : Baker<EnemySpawnerAuthoring>
        {
            public override void Bake(EnemySpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new AbilitySpawnData
                {
                    SpawnDistance = math.max(1f, authoring.SpawnDistance)
                });

                var spawnDefinitions = AddBuffer<AbilitySpawnDefinition>(entity);
                foreach (var definition in authoring.AbilitySpawnDefinitions)
                {
                    AddDefinition(spawnDefinitions, definition);
                }

                AddComponent(entity, new AbilitySpawnState
                {
                    Random = Random.CreateFromIndex(authoring.RandomSeed == 0 ? 1u : authoring.RandomSeed)
                });
            }

            private void AddDefinition(
                DynamicBuffer<AbilitySpawnDefinition> spawnDefinitions,
                AbilitySpawnDefinitionAuthoring definition)
            {
                spawnDefinitions.Add(new AbilitySpawnDefinition
                {
                    Type = definition.Type,
                    Prefab = GetOptionalEntity(definition.Prefab),
                    Count = math.max(1, definition.Count),
                    Pattern = definition.Pattern
                });
            }

            private Entity GetOptionalEntity(GameObject prefab)
            {
                return prefab == null ? Entity.Null : GetEntity(prefab, TransformUsageFlags.Dynamic);
            }
        }
    }
}
