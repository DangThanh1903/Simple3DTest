using Unity.Entities;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class EnemyContactAttackAuthoring : MonoBehaviour
    {
        public int AttackDamage;
        public float CooldownTime;

        private class Baker : Baker<EnemyContactAttackAuthoring>
        {
            public override void Bake(EnemyContactAttackAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EnemyAttackData
                {
                    HitPoints = authoring.AttackDamage,
                    CooldownTime = authoring.CooldownTime
                });
                AddComponent<EnemyCooldownExpirationTimestamp>(entity);
                SetComponentEnabled<EnemyCooldownExpirationTimestamp>(entity, false);
            }
        }
    }
}
