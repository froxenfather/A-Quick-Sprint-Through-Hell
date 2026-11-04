# Hazard Surfaces (lava, electric walls)

Surfaces that cap the player's speed and throw them back. A material can't hold a script, so the surface is any object with a collider plus a `SpeedCapSurface` component (`Scripts/Objects/SpeedCapSurface.cs`). The material is only the look.

**Runs:** step 8b `ApplySurfaceHazards`, right after `Move()` and before `MoveAndSlide`. Touches are collected at the end of the previous step by `CollectSurfaceTouches` and by `MoveAndSlide`.

| Variable (on the surface) | Default | Meaning |
|---|---|---|
| `maxSpeed` | 5 | Flat speed (m/s) allowed while touching |
| `bounceSpeed` | 10 | Speed along the surface normal on first touch. 0 = no bounce |

The `SpeedCapSurface` has to be on the same object as the `Collider` (the controller looks on the collider's object).

## What counts as touching

Any of these, measured by the controller (so one step of delay, like wall jumping):

- A collide-and-slide hit this step (`MoveAndSlide`), with that hit's normal.
- The ground we stand on (`groundCollider`, `groundNormal`).
- The wall in range (`currentWall`, within `wallDetectDistance`), so running along a wall counts even without bumping it.

## Steps

1. **Cap.** `flat` is limited to `maxSpeed`. For a wall (normal not walkable) the part of the speed pointing away from it is exempt, so the bounce is not squashed but running along it is.
2. **Air cap.** `airSpeedCap = min(airSpeedCap, maxSpeed)`. It only refreshes while grounded, so after leaving the hazard in the air we cannot gain speed until we land.
3. **Bounce.** On the first touch of that surface (it was not in last step's touches), the speed along the normal is raised to at least `bounceSpeed`. Floors: grounded is cleared, no coyote jump, counts as leaving the ground. Walls: `currentWall` is cleared so slip gravity does not apply.

## Interactions

- **Dash:** capped too. The cap is applied after `Move()`, in the same step as the dash.
- **Wall run:** needs 20 m/s, so touching an electric wall drops it (one step later).
- **Jumps:** a jump off lava gets its normal upward speed; the flat part is capped while still touching.
- **Standing on lava:** each landing bounces again, which makes a hop of `bounceSpeed^2 / (2 * gravity)` m (10 m/s at gravity 30 = 1.7 m).

## Reasoning

- **Component, not material.** Material names are fragile (instances, ProBuilder meshes with several materials). A component works on any collider and holds the numbers.
- **Bounce is assigned, not added.** Landing at -15 m/s then adding +10 would still point down. Raising the normal speed to `bounceSpeed` always throws us off.
- **After `Move()`.** Ground and air movement accelerate toward their targets each step, so capping before them would let ~2 m/s per step creep back.
