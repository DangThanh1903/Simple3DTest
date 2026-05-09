using Unity.Entities;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class MeleeChaserAuthoring : MonoBehaviour
    {
        private class Baker : Baker<MeleeChaserAuthoring>
        {
            public override void Bake(MeleeChaserAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<MeleeChaserTag>(entity);
                AddComponent<ChasePlayerTag>(entity);
            }
        }
    }
}
