using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TMG.Survivors
{
    public partial struct StasisOverlordSystem : ISystem
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
            var playerFreeze = SystemAPI.GetComponentRW<PlayerFreeze>(playerEntity);

            foreach (var (transform, data, stasisState) in SystemAPI
                         .Query<RefRW<LocalTransform>, StasisOverlordData, RefRW<StasisOverlordState>>()
                         .WithAll<StasisOverlordTag>())
            {
                if (stasisState.ValueRO.BaseScale <= 0f)
                {
                    stasisState.ValueRW.BaseScale = transform.ValueRO.Scale;
                }

                stasisState.ValueRW.CastTimer -= deltaTime;
                transform.ValueRW.Scale = stasisState.ValueRO.BaseScale;

                var distanceToPlayerSq = math.distancesq(transform.ValueRO.Position.xy, playerPosition.xy);
                if (stasisState.ValueRO.CastTimer > 0f ||
                    distanceToPlayerSq > data.FreezeRange * data.FreezeRange)
                {
                    continue;
                }

                playerFreeze.ValueRW.RemainingTime = math.max(playerFreeze.ValueRO.RemainingTime, data.FreezeDuration);
                stasisState.ValueRW.CastTimer = data.CastCooldown;
                transform.ValueRW.Scale = stasisState.ValueRO.BaseScale * 1.35f;
            }
        }
    }
}
