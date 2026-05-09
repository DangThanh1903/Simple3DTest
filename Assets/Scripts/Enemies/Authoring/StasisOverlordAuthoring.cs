using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class StasisOverlordAuthoring : MonoBehaviour
    {
        public float FreezeRange = 8f;
        public float FreezeDuration = 2f;
        public float CastCooldown = 4f;

        private class Baker : Baker<StasisOverlordAuthoring>
        {
            public override void Bake(StasisOverlordAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<StasisOverlordTag>(entity);
                AddComponent<ChasePlayerTag>(entity);
                AddComponent(entity, new StasisOverlordData
                {
                    FreezeRange = math.max(0.1f, authoring.FreezeRange),
                    FreezeDuration = math.max(0.1f, authoring.FreezeDuration),
                    CastCooldown = math.max(0.1f, authoring.CastCooldown)
                });
                AddComponent(entity, new StasisOverlordState
                {
                    CastTimer = 1f,
                    BaseScale = math.max(0.1f, authoring.transform.localScale.x)
                });
            }
        }
    }
}
