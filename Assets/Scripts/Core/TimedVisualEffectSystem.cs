using Unity.Entities;

namespace TMG.Survivors
{
    public partial struct TimedVisualEffectSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (effect, entity) in SystemAPI.Query<RefRW<TimedVisualEffect>>().WithEntityAccess())
            {
                effect.ValueRW.RemainingTime -= deltaTime;
                if (effect.ValueRO.RemainingTime <= 0f)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
