using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TMG.Survivors
{
    public class RollingHazardAuthoring : MonoBehaviour
    {
        public float Lifetime = 5f;
        public float MoveSpeed = 12f;
        public float KnockbackSpeed = 14f;
        public float KnockbackDuration = 0.35f;

        private class Baker : Baker<RollingHazardAuthoring>
        {
            public override void Bake(RollingHazardAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var lifetime = math.max(0.1f, authoring.Lifetime);

                AddComponent<RollingHazardTag>(entity);
                AddComponent<InitializeRollingHazardFlag>(entity);
                AddComponent(entity, new RollingHazardData
                {
                    Lifetime = lifetime,
                    MoveSpeed = math.max(0.1f, authoring.MoveSpeed),
                    KnockbackSpeed = math.max(0.1f, authoring.KnockbackSpeed),
                    KnockbackDuration = math.max(0.05f, authoring.KnockbackDuration)
                });
                AddComponent(entity, new RollingHazardState
                {
                    RemainingTime = lifetime,
                    Direction = new float2(1f, 0f)
                });
                AddComponent<DestroyEntityFlag>(entity);
                SetComponentEnabled<DestroyEntityFlag>(entity, false);
            }
        }
    }
}
