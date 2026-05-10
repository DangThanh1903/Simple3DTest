# Simple3DTest

Unity survival prototype built with a hybrid ECS/DOTS architecture. The project focuses on spawning enemy events, projectile combat, collision-based effects, pickups, and simple UI while keeping runtime gameplay data-oriented.

## Tech Stack

- Unity `6000.3.6f1`
- Unity Entities `1.4.2`
- Unity Physics `1.4.2`
- Entities Graphics `1.4.15`
- Universal Render Pipeline `17.3.0`
- Unity Input System `1.18.0`
- Hybrid presentation/UI with MonoBehaviour, TextMeshPro, and uGUI

## Optimization Highlights

This repo uses ECS/DOTS as the main gameplay layer. Authoring components are baked into ECS components, then runtime behavior is processed by systems.

Applied optimization techniques:

- **Job System + Burst Compiler**
  - Enemy contact damage uses `ICollisionEventsJob`.
  - Gem collection uses `ITriggerEventsJob`.
  - Projectile movement/hit detection uses `IJobEntity` with Burst.
  - Chase movement support jobs build and sort spatial hash data in parallel.

- **Spatial Partitioning**
  - `ChasePlayerMoveSystem` uses a spatial hash grid to calculate local separation between chasing enemies.
  - This avoids expensive all-vs-all neighbor checks and helps keep large enemy groups stable.

- **ECS Enableable Components**
  - `DestroyEntityFlag`, cooldown flags, and UI update flags avoid unnecessary structural churn where possible.

- **Projectile Pooling**
  - Enemy projectiles can be disabled and reused instead of always destroyed/recreated.

- **Deterministic Lightweight Hit Checks**
  - Rolling hazards use direct swept segment-vs-circle checks for player contact instead of relying only on physics collision events.

## Gameplay Structure

Main folders:

- `Assets/Scripts/Core`: shared character movement, health, damage, destroy flow, timed effects.
- `Assets/Scripts/Player`: player authoring, input, auto attack, camera target, gem UI, world UI.
- `Assets/Scripts/Abilities`: debug/event input and ability spawn requests.
- `Assets/Scripts/Enemies`: enemy components, authoring, systems, and spatial hash utilities.
- `Assets/Scripts/Projectiles`: plasma projectile data, movement, hit detection.
- `Assets/Scripts/Pickups`: gem pickup logic.
- `Assets/Scripts/UI`: title/game UI and entity counter.

Ability debug keys:

- `Q`: Aerial Artillery
- `W`: Swift Swarm
- `E`: Volatile Vanguard
- `Space`: Rolling Hazard
- `R`: Stasis Overlord
- `F`: Lightning Striker
- `B`: Heavy Leaper

## How To Run

1. Open the project with Unity `6000.3.6f1`.
2. Open scene:
   - `Assets/Scenes/TitleScene.unity` to start from the menu, or
   - `Assets/Scenes/MoonLevel.unity` to jump directly into gameplay.
3. Press Play.

The gameplay ECS content is stored in the entity subscene:

- `Assets/Scenes/MoonLevel/MoonEntityScene.unity`

Open `MoonLevel.unity` for normal gameplay testing; it references the entity scene setup used by the prototype.
