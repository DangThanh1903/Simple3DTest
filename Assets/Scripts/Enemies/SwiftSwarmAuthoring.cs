using Unity.Entities;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class SwiftSwarmAuthoring : MonoBehaviour
    {
        private class Baker : Baker<SwiftSwarmAuthoring>
        {
            public override void Bake(SwiftSwarmAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<SwiftSwarmTag>(entity);
                AddComponent<ChasePlayerTag>(entity);
            }
        }
    }
}
