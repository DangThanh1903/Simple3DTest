using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TMG.Survivors
{
    public partial struct VolatileVanguardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            var playerDamageBuffer = SystemAPI.GetBuffer<DamageThisFrame>(playerEntity);

            foreach (var (transform, data, volatileState, entity) in SystemAPI
                         .Query<RefRW<LocalTransform>, VolatileVanguardData, RefRW<VolatileVanguardState>>()
                         .WithAll<VolatileVanguardTag>()
                         .WithPresent<DestroyEntityFlag>()
                         .WithEntityAccess())
            {
                volatileState.ValueRW.RemainingTime -= deltaTime;
                if (volatileState.ValueRO.BaseScale <= 0f)
                {
                    volatileState.ValueRW.BaseScale = transform.ValueRO.Scale;
                }

                var normalizedTime = math.saturate(volatileState.ValueRO.RemainingTime / data.CountdownTime);
                var blinkInterval = math.lerp(0.08f, 0.45f, normalizedTime);
                volatileState.ValueRW.BlinkTimer -= deltaTime;
                if (volatileState.ValueRO.BlinkTimer <= 0f)
                {
                    volatileState.ValueRW.BlinkExpanded = !volatileState.ValueRO.BlinkExpanded;
                    volatileState.ValueRW.BlinkTimer = blinkInterval;
                }

                var pulseScale = volatileState.ValueRO.BlinkExpanded ? 1.35f : 1f;
                transform.ValueRW.Scale = volatileState.ValueRO.BaseScale * pulseScale;

                if (volatileState.ValueRO.RemainingTime > 0f)
                {
                    continue;
                }

                var distanceToPlayerSq = math.distancesq(transform.ValueRO.Position.xy, playerPosition.xy);
                if (distanceToPlayerSq <= data.ExplosionRadius * data.ExplosionRadius)
                {
                    playerDamageBuffer.Add(new DamageThisFrame
                    {
                        Value = data.ExplosionDamage
                    });
                }

                SystemAPI.SetComponentEnabled<DestroyEntityFlag>(entity, true);
            }
        }
    }
}
