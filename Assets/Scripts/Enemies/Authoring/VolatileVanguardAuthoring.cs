using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class VolatileVanguardAuthoring : MonoBehaviour
    {
        public float CountdownTime = 5f;
        public float ExplosionRadius = 3f;
        public int ExplosionDamage = 25;

        private class Baker : Baker<VolatileVanguardAuthoring>
        {
            public override void Bake(VolatileVanguardAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var countdownTime = math.max(0.1f, authoring.CountdownTime);
                AddComponent<VolatileVanguardTag>(entity);
                AddComponent<ChasePlayerTag>(entity);
                AddComponent(entity, new VolatileVanguardData
                {
                    CountdownTime = countdownTime,
                    ExplosionRadius = math.max(0.1f, authoring.ExplosionRadius),
                    ExplosionDamage = math.max(1, authoring.ExplosionDamage)
                });
                AddComponent(entity, new VolatileVanguardState
                {
                    RemainingTime = countdownTime,
                    BlinkTimer = 0f,
                    BaseScale = math.max(0.1f, authoring.transform.localScale.x),
                    BlinkExpanded = false
                });
            }
        }
    }
}
