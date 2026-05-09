using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TMG.Survivors
{
    public partial struct HeavyLeaperSystem : ISystem
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

            foreach (var (transform, direction, data, leaperState) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRW<CharacterMoveDirection>, HeavyLeaperData, RefRW<HeavyLeaperState>>()
                         .WithAll<HeavyLeaperTag>())
            {
                direction.ValueRW.Value = float2.zero;

                if (leaperState.ValueRO.BaseScale <= 0f)
                {
                    leaperState.ValueRW.BaseScale = transform.ValueRO.Scale;
                }

                leaperState.ValueRW.Timer -= deltaTime;
                if (leaperState.ValueRO.Phase == HeavyLeaperPhase.Waiting)
                {
                    transform.ValueRW.Scale = leaperState.ValueRO.BaseScale;
                    if (leaperState.ValueRO.Timer > 0f)
                    {
                        continue;
                    }

                    leaperState.ValueRW.Phase = HeavyLeaperPhase.Leaping;
                    leaperState.ValueRW.Timer = data.LeapDuration;
                    leaperState.ValueRW.StartPosition = transform.ValueRO.Position;
                    leaperState.ValueRW.TargetPosition = playerPosition;
                    continue;
                }

                var progress = 1f - math.saturate(leaperState.ValueRO.Timer / data.LeapDuration);
                var nextPosition = math.lerp(leaperState.ValueRO.StartPosition, leaperState.ValueRO.TargetPosition, progress);
                var arcScale = 1f + math.sin(progress * math.PI) * 0.75f;
                transform.ValueRW.Position = nextPosition;
                transform.ValueRW.Scale = leaperState.ValueRO.BaseScale * arcScale;

                if (leaperState.ValueRO.Timer > 0f)
                {
                    continue;
                }

                transform.ValueRW.Position = leaperState.ValueRO.TargetPosition;
                transform.ValueRW.Scale = leaperState.ValueRO.BaseScale;

                var distanceToPlayerSq = math.distancesq(transform.ValueRO.Position.xy, playerPosition.xy);
                if (distanceToPlayerSq <= data.SlamRadius * data.SlamRadius)
                {
                    playerDamageBuffer.Add(new DamageThisFrame
                    {
                        Value = data.SlamDamage
                    });
                }

                leaperState.ValueRW.Phase = HeavyLeaperPhase.Waiting;
                leaperState.ValueRW.Timer = data.LeapInterval;
            }
        }
    }
}
