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

            foreach (var (transform, direction, data, stasisState) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRW<CharacterMoveDirection>, StasisOverlordData, RefRW<StasisOverlordState>>()
                         .WithAll<StasisOverlordTag>())
            {
                if (stasisState.ValueRO.BaseScale <= 0f)
                {
                    stasisState.ValueRW.BaseScale = transform.ValueRO.Scale;
                }

                var toPlayer = playerPosition.xy - transform.ValueRO.Position.xy;
                var distanceSq = math.lengthsq(toPlayer);
                var moveDirection = distanceSq > 0.0001f ? math.normalize(toPlayer) : float2.zero;
                var distance = math.sqrt(distanceSq);

                if (distance < data.MinimumDistance)
                {
                    direction.ValueRW.Value = -moveDirection;
                }
                else if (distance > data.PreferredDistance)
                {
                    direction.ValueRW.Value = moveDirection;
                }
                else
                {
                    direction.ValueRW.Value = float2.zero;
                }

                stasisState.ValueRW.CastTimer -= deltaTime;
                transform.ValueRW.Scale = stasisState.ValueRO.BaseScale;

                if (stasisState.ValueRO.CastTimer > 0f ||
                    distanceSq > data.FreezeRange * data.FreezeRange)
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
