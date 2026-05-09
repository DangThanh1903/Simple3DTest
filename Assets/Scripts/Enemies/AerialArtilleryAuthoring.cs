using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class AerialArtilleryAuthoring : MonoBehaviour
    {
        public GameObject ProjectilePrefab;
        public float MinimumDistance = 7f;
        public float PreferredDistance = 10f;
        public float ShootCooldown = 1.25f;

        private class Baker : Baker<AerialArtilleryAuthoring>
        {
            public override void Bake(AerialArtilleryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<AerialArtilleryTag>(entity);
                AddComponent(entity, new AerialArtilleryData
                {
                    ProjectilePrefab = authoring.ProjectilePrefab == null
                        ? Entity.Null
                        : GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                    MinimumDistance = math.max(0f, authoring.MinimumDistance),
                    PreferredDistance = math.max(authoring.MinimumDistance, authoring.PreferredDistance),
                    ShootCooldown = math.max(0.1f, authoring.ShootCooldown)
                });
                AddComponent(entity, new AerialArtilleryState
                {
                    ShootTimer = 0f
                });
            }
        }
    }
}
