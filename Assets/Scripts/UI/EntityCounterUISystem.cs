using Unity.Entities;

namespace TMG.Survivors
{
    public partial struct EntityCounterUISystem : ISystem
    {
        private EntityQuery _enemyQuery;
        private EntityQuery _projectileQuery;
        private EntityQuery _hazardQuery;
        private EntityQuery _pooledProjectileQuery;
        private float _updateTimer;

        public void OnCreate(ref SystemState state)
        {
            _enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag>()
                .WithNone<Disabled>()
                .Build();
            _projectileQuery = SystemAPI.QueryBuilder()
                .WithAll<PlasmaBlastData>()
                .WithNone<Disabled>()
                .Build();
            _hazardQuery = SystemAPI.QueryBuilder()
                .WithAll<RollingHazardTag>()
                .WithNone<Disabled>()
                .Build();
            _pooledProjectileQuery = SystemAPI.QueryBuilder()
                .WithAll<PooledEnemyProjectileTag, Disabled>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            _updateTimer -= SystemAPI.Time.DeltaTime;
            if (_updateTimer > 0f)
            {
                return;
            }

            _updateTimer = 0.2f;
            if (GameUIController.Instance == null)
            {
                return;
            }

            GameUIController.Instance.UpdateEntityCounterText(
                _enemyQuery.CalculateEntityCount(),
                _projectileQuery.CalculateEntityCount(),
                _hazardQuery.CalculateEntityCount(),
                _pooledProjectileQuery.CalculateEntityCount());
        }
    }
}
