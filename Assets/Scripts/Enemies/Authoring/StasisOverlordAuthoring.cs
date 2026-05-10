using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class StasisOverlordAuthoring : MonoBehaviour
    {
        public float MinimumDistance = 5f;
        public float PreferredDistance = 7f;
        public float FreezeRange = 8f;
        public float FreezeDuration = 2f;
        public float CastCooldown = 4f;

        private class Baker : Baker<StasisOverlordAuthoring>
        {
            public override void Bake(StasisOverlordAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<StasisOverlordTag>(entity);
                AddComponent(entity, new StasisOverlordData
                {
                    MinimumDistance = math.max(0f, authoring.MinimumDistance),
                    PreferredDistance = math.max(authoring.MinimumDistance, authoring.PreferredDistance),
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
