using Unity.Entities;
using UnityEngine;

namespace TMG.Survivors
{
    [RequireComponent(typeof(CharacterAuthoring))]
    public class EnemyAuthoring : MonoBehaviour
    {
        public GameObject GemPrefab;

        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTag>(entity);
                AddComponent(entity, new GemPrefab
                {
                    Value = GetEntity(authoring.GemPrefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
