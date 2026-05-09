using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class HeavyLeaperAuthoring : MonoBehaviour
    {
        public float LeapInterval = 1.5f;
        public float LeapDuration = 0.65f;
        public float SlamRadius = 3.5f;
        public int SlamDamage = 20;

        private class Baker : Baker<HeavyLeaperAuthoring>
        {
            public override void Bake(HeavyLeaperAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var leapInterval = math.max(0.1f, authoring.LeapInterval);
                AddComponent<HeavyLeaperTag>(entity);
                AddComponent(entity, new HeavyLeaperData
                {
                    LeapInterval = leapInterval,
                    LeapDuration = math.max(0.1f, authoring.LeapDuration),
                    SlamRadius = math.max(0.1f, authoring.SlamRadius),
                    SlamDamage = math.max(1, authoring.SlamDamage)
                });
                AddComponent(entity, new HeavyLeaperState
                {
                    Phase = HeavyLeaperPhase.Waiting,
                    Timer = leapInterval,
                    StartPosition = float3.zero,
                    TargetPosition = float3.zero,
                    BaseScale = math.max(0.1f, authoring.transform.localScale.x)
                });
            }
        }
    }
}
