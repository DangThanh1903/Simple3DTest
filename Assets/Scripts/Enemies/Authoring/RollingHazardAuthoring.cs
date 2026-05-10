using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TMG.Survivors
{
    public class RollingHazardAuthoring : MonoBehaviour
    {
        public float Lifetime = 5f;
        public float MoveSpeed = 12f;
        public float ContactRadius = 1.25f;
        public float StunDuration = 1f;
        public float KnockbackSpeed = 7f;
        public float KnockbackDuration = 0.2f;

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
                    ContactRadius = math.max(0.1f, authoring.ContactRadius),
                    StunDuration = math.max(0f, authoring.StunDuration),
                    KnockbackSpeed = math.max(0.1f, authoring.KnockbackSpeed),
                    KnockbackDuration = math.max(0.05f, authoring.KnockbackDuration)
                });
                AddComponent(entity, new RollingHazardState
                {
                    RemainingTime = lifetime,
                    Direction = new float2(1f, 0f),
                    HasHitPlayer = false
                });
                AddComponent<DestroyEntityFlag>(entity);
                SetComponentEnabled<DestroyEntityFlag>(entity, false);
            }
        }
    }
}
