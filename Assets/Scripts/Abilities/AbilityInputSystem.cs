using Unity.Entities;
using UnityEngine;

namespace TMG.Survivors
{
    public partial class AbilityInputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<AbilitySpawnData>();
        }

        protected override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                CreateSpawnRequest(AbilitySpawnType.AerialArtillery);
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                CreateSpawnRequest(AbilitySpawnType.SwiftSwarm);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                CreateSpawnRequest(AbilitySpawnType.VolatileVanguard);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                CreateSpawnRequest(AbilitySpawnType.RollingHazard);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                CreateSpawnRequest(AbilitySpawnType.StasisOverlord);
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                CreateSpawnRequest(AbilitySpawnType.LightningStriker);
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                CreateSpawnRequest(AbilitySpawnType.HeavyLeaper);
            }
        }

        private void CreateSpawnRequest(AbilitySpawnType spawnType)
        {
            var request = EntityManager.CreateEntity();
            EntityManager.AddComponentData(request, new AbilitySpawnRequest
            {
                Type = spawnType
            });
        }
    }
}
