using Unity.Entities;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(EnemyAuthoring))]
    public class GemDropAuthoring : MonoBehaviour
    {
        public GameObject GemPrefab;

        private class Baker : Baker<GemDropAuthoring>
        {
            public override void Bake(GemDropAuthoring authoring)
            {
                if (authoring.GemPrefab == null)
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GemPrefab
                {
                    Value = GetEntity(authoring.GemPrefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
