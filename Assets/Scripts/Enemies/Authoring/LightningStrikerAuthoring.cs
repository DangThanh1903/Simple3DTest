using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class LightningStrikerAuthoring : MonoBehaviour
    {
        public GameObject WarningPrefab;
        public GameObject StrikeFlashPrefab;
        public float FlyDuration = 1.25f;
        public float FlySpeed = 18f;
        public float LockDuration = 0.75f;
        public float StrikeFlashDuration = 0.2f;
        public float StrikeRadius = 2.75f;
        public int StrikeDamage = 35;

        private class Baker : Baker<LightningStrikerAuthoring>
        {
            public override void Bake(LightningStrikerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var flyDuration = math.max(0.1f, authoring.FlyDuration);
                AddComponent<LightningStrikerTag>(entity);
                AddComponent(entity, new LightningStrikerData
                {
                    WarningPrefab = GetOptionalEntity(authoring.WarningPrefab),
                    StrikeFlashPrefab = GetOptionalEntity(authoring.StrikeFlashPrefab),
                    FlyDuration = flyDuration,
                    FlySpeed = math.max(0.1f, authoring.FlySpeed),
                    LockDuration = math.max(0.1f, authoring.LockDuration),
                    StrikeFlashDuration = math.max(0.05f, authoring.StrikeFlashDuration),
                    StrikeRadius = math.max(0.1f, authoring.StrikeRadius),
                    StrikeDamage = math.max(1, authoring.StrikeDamage)
                });
                AddComponent(entity, new LightningStrikerState
                {
                    Phase = LightningStrikerPhase.Flying,
                    Timer = flyDuration,
                    LockedTargetPosition = float3.zero,
                    BaseScale = math.max(0.1f, authoring.transform.localScale.x)
                });
            }

            private Entity GetOptionalEntity(GameObject prefab)
            {
                return prefab == null ? Entity.Null : GetEntity(prefab, TransformUsageFlags.Dynamic);
            }
        }
    }
}
