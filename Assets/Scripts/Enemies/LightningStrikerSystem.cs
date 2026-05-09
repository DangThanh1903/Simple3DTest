using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TMG.Survivors
{
    public partial struct LightningStrikerSystem : ISystem
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

            foreach (var (transform, data, lightningState, entity) in SystemAPI
                         .Query<RefRW<LocalTransform>, LightningStrikerData, RefRW<LightningStrikerState>>()
                         .WithAll<LightningStrikerTag>()
                         .WithPresent<DestroyEntityFlag>()
                         .WithEntityAccess())
            {
                if (lightningState.ValueRO.BaseScale <= 0f)
                {
                    lightningState.ValueRW.BaseScale = transform.ValueRO.Scale;
                }

                lightningState.ValueRW.Timer -= deltaTime;

                if (lightningState.ValueRO.Phase == LightningStrikerPhase.Flying)
                {
                    transform.ValueRW.Position += new float3(data.FlySpeed * deltaTime, 0f, 0f);
                    transform.ValueRW.Scale = lightningState.ValueRO.BaseScale;

                    if (lightningState.ValueRO.Timer > 0f)
                    {
                        continue;
                    }

                    lightningState.ValueRW.Phase = LightningStrikerPhase.Locking;
                    lightningState.ValueRW.Timer = data.LockDuration;
                    lightningState.ValueRW.LockedTargetPosition = playerPosition;
                    continue;
                }

                if (lightningState.ValueRO.Phase == LightningStrikerPhase.Locking)
                {
                    transform.ValueRW.Scale = lightningState.ValueRO.BaseScale * 1.4f;

                    if (lightningState.ValueRO.Timer > 0f)
                    {
                        continue;
                    }

                    lightningState.ValueRW.Phase = LightningStrikerPhase.Striking;
                    lightningState.ValueRW.Timer = 0.15f;

                    var distanceToLockedTargetSq = math.distancesq(playerPosition.xy, lightningState.ValueRO.LockedTargetPosition.xy);
                    if (distanceToLockedTargetSq <= data.StrikeRadius * data.StrikeRadius)
                    {
                        playerDamageBuffer.Add(new DamageThisFrame
                        {
                            Value = data.StrikeDamage
                        });
                    }

                    continue;
                }

                transform.ValueRW.Position = lightningState.ValueRO.LockedTargetPosition;
                transform.ValueRW.Scale = lightningState.ValueRO.BaseScale * 2f;
                if (lightningState.ValueRO.Timer <= 0f)
                {
                    SystemAPI.SetComponentEnabled<DestroyEntityFlag>(entity, true);
                }
            }
        }
    }
}
