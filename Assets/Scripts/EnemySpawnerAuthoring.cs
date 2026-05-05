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
        public float RollingHazardLifetime;
        public float RollingHazardMoveSpeed;
        public float RollingHazardKnockbackSpeed;
        public float RollingHazardKnockbackDuration;
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

        [Header("Rolling Hazard")]
        public float RollingHazardLifetime = 5f;
        public float RollingHazardMoveSpeed = 12f;
        public float RollingHazardKnockbackSpeed = 14f;
        public float RollingHazardKnockbackDuration = 0.35f;
        
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
                    LightningStrikerPrefab = GetPrefabOrFallback(authoring.LightningStrikerPrefab, reaperPrefab),
                    HeavyLeaperPrefab = GetPrefabOrFallback(authoring.HeavyLeaperPrefab, defaultEnemyPrefab),
                    EnemyProjectilePrefab = GetOptionalEntity(authoring.EnemyProjectilePrefab),
                    AerialArtilleryCount = math.max(1, authoring.AerialArtilleryCount),
                    SwiftSwarmCount = math.max(1, authoring.SwiftSwarmCount),
                    VolatileVanguardCount = math.max(1, authoring.VolatileVanguardCount),
                    HeavyLeaperCount = math.max(1, authoring.HeavyLeaperCount),
                    SpawnDistance = math.max(1f, authoring.SpawnDistance),
                    RandomSeed = authoring.RandomSeed == 0 ? 1u : authoring.RandomSeed,
                    RollingHazardLifetime = math.max(0.1f, authoring.RollingHazardLifetime),
                    RollingHazardMoveSpeed = math.max(0.1f, authoring.RollingHazardMoveSpeed),
                    RollingHazardKnockbackSpeed = math.max(0.1f, authoring.RollingHazardKnockbackSpeed),
                    RollingHazardKnockbackDuration = math.max(0.05f, authoring.RollingHazardKnockbackDuration)
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
}
