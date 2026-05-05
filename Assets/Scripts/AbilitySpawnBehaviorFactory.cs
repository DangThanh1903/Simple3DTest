using Unity.Entities;
using Unity.Mathematics;

namespace TMG.Survivors
{
    public static class AbilitySpawnBehaviorFactory
    {
        public static void Apply(
            ref SystemState state,
            EntityCommandBuffer ecb,
            AbilitySpawnData spawnData,
            Entity prefab,
            Entity entity,
            AbilitySpawnType spawnType)
        {
            RemoveBehaviorTags(ref state, ecb, prefab, entity);

            switch (spawnType)
            {
                case AbilitySpawnType.AerialArtillery:
                    ecb.AddComponent<AerialArtilleryTag>(entity);
                    RemoveComponentIfPresent<EnemyAttackData>(ref state, ecb, prefab, entity);
                    AddOrPatchAerialData(ref state, ecb, spawnData, prefab, entity);
                    break;
                case AbilitySpawnType.SwiftSwarm:
                    ecb.AddComponent<SwiftSwarmTag>(entity);
                    ecb.AddComponent<ChasePlayerTag>(entity);
                    break;
                case AbilitySpawnType.VolatileVanguard:
                    ecb.AddComponent<VolatileVanguardTag>(entity);
                    ecb.AddComponent<ChasePlayerTag>(entity);
                    RemoveComponentIfPresent<EnemyAttackData>(ref state, ecb, prefab, entity);
                    AddVolatileDataIfMissing(ref state, ecb, prefab, entity);
                    break;
                case AbilitySpawnType.RollingHazard:
                    ecb.AddComponent<RollingHazardTag>(entity);
                    ecb.AddComponent<InitializeRollingHazardFlag>(entity);
                    ecb.AddComponent(entity, new RollingHazardData
                    {
                        Lifetime = spawnData.RollingHazardLifetime,
                        MoveSpeed = spawnData.RollingHazardMoveSpeed,
                        KnockbackSpeed = spawnData.RollingHazardKnockbackSpeed,
                        KnockbackDuration = spawnData.RollingHazardKnockbackDuration
                    });
                    ecb.AddComponent(entity, new RollingHazardState
                    {
                        RemainingTime = spawnData.RollingHazardLifetime,
                        Direction = new float2(1f, 0f)
                    });
                    ecb.AddComponent<DestroyEntityFlag>(entity);
                    ecb.SetComponentEnabled<DestroyEntityFlag>(entity, false);
                    break;
                case AbilitySpawnType.StasisOverlord:
                    ecb.AddComponent<StasisOverlordTag>(entity);
                    ecb.AddComponent<ChasePlayerTag>(entity);
                    RemoveComponentIfPresent<EnemyAttackData>(ref state, ecb, prefab, entity);
                    AddStasisOverlordDataIfMissing(ref state, ecb, prefab, entity);
                    break;
                case AbilitySpawnType.LightningStriker:
                    ecb.AddComponent<LightningStrikerTag>(entity);
                    RemoveComponentIfPresent<EnemyAttackData>(ref state, ecb, prefab, entity);
                    AddLightningStrikerDataIfMissing(ref state, ecb, prefab, entity);
                    break;
                case AbilitySpawnType.HeavyLeaper:
                    ecb.AddComponent<HeavyLeaperTag>(entity);
                    RemoveComponentIfPresent<EnemyAttackData>(ref state, ecb, prefab, entity);
                    AddHeavyLeaperDataIfMissing(ref state, ecb, prefab, entity);
                    break;
            }
        }

        private static void RemoveBehaviorTags(
            ref SystemState state,
            EntityCommandBuffer ecb,
            Entity prefab,
            Entity entity)
        {
            RemoveComponentIfPresent<ChasePlayerTag>(ref state, ecb, prefab, entity);
            RemoveComponentIfPresent<MeleeChaserTag>(ref state, ecb, prefab, entity);
            RemoveComponentIfPresent<SwiftSwarmTag>(ref state, ecb, prefab, entity);
            RemoveComponentIfPresent<AerialArtilleryTag>(ref state, ecb, prefab, entity);
            RemoveComponentIfPresent<VolatileVanguardTag>(ref state, ecb, prefab, entity);
            RemoveComponentIfPresent<HeavyLeaperTag>(ref state, ecb, prefab, entity);
            RemoveComponentIfPresent<StasisOverlordTag>(ref state, ecb, prefab, entity);
            RemoveComponentIfPresent<RollingHazardTag>(ref state, ecb, prefab, entity);
            RemoveComponentIfPresent<LightningStrikerTag>(ref state, ecb, prefab, entity);
        }

        private static void AddOrPatchAerialData(
            ref SystemState state,
            EntityCommandBuffer ecb,
            AbilitySpawnData spawnData,
            Entity prefab,
            Entity entity)
        {
            var projectilePrefab = spawnData.EnemyProjectilePrefab;
            if (state.EntityManager.HasComponent<AerialArtilleryData>(prefab))
            {
                var aerialData = state.EntityManager.GetComponentData<AerialArtilleryData>(prefab);
                if (projectilePrefab != Entity.Null)
                {
                    aerialData.ProjectilePrefab = projectilePrefab;
                }

                ecb.SetComponent(entity, aerialData);
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

        private static void AddVolatileDataIfMissing(
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

        private static void AddHeavyLeaperDataIfMissing(
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

        private static void AddStasisOverlordDataIfMissing(
            ref SystemState state,
            EntityCommandBuffer ecb,
            Entity prefab,
            Entity entity)
        {
            if (!state.EntityManager.HasComponent<StasisOverlordData>(prefab))
            {
                ecb.AddComponent(entity, new StasisOverlordData
                {
                    FreezeRange = 8f,
                    FreezeDuration = 2f,
                    CastCooldown = 4f
                });
            }

            if (!state.EntityManager.HasComponent<StasisOverlordState>(prefab))
            {
                ecb.AddComponent(entity, new StasisOverlordState
                {
                    CastTimer = 1f,
                    BaseScale = 1f
                });
            }
        }

        private static void AddLightningStrikerDataIfMissing(
            ref SystemState state,
            EntityCommandBuffer ecb,
            Entity prefab,
            Entity entity)
        {
            if (!state.EntityManager.HasComponent<LightningStrikerData>(prefab))
            {
                ecb.AddComponent(entity, new LightningStrikerData
                {
                    FlyDuration = 1.25f,
                    FlySpeed = 18f,
                    LockDuration = 0.75f,
                    StrikeRadius = 2.75f,
                    StrikeDamage = 35
                });
            }

            if (!state.EntityManager.HasComponent<LightningStrikerState>(prefab))
            {
                ecb.AddComponent(entity, new LightningStrikerState
                {
                    Phase = LightningStrikerPhase.Flying,
                    Timer = 1.25f,
                    LockedTargetPosition = float3.zero,
                    BaseScale = 1f
                });
            }
        }

        private static void RemoveComponentIfPresent<T>(
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
