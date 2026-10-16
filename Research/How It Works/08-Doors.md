# Doors

**Runs:** the door animates in its own `Update()`. The player only reacts in step 3 `Depenetrate`, on the next `FixedUpdate`.

| Variable (`Door`) | Value |
|---|---|
| `openAngle` | 90° |
| `rotationSpeed` | 90°/s |

## Steps

**Powering the door**

1. `PressurePlate.OnTriggerEnter` -> `target.SwitchOn()`; `OnTriggerExit` -> `SwitchOff()` (through the `IPowered` interface).
2. The player's kinematic Rigidbody is what makes trigger volumes detect it. The controller's own sweeps use `QueryTriggerInteraction.Ignore`, so plates never block movement.

**Moving the door (`Door.Update`)**

1. `targetAngle = isOpen ? openAngle : 0`
2. `localRotation = RotateTowards(localRotation, Euler(0, targetAngle, 0), rotationSpeed * Time.deltaTime)`

**When it swings into the player (step 3, `Depenetrate`)**

Up to 3 passes:
1. `OverlapCapsule` at our position.
2. `ComputePenetration` against each overlapping collider -> shortest escape direction + distance.
3. `position += escapeDirection * (distance + 0.001)`

## Math

- Open time = `openAngle / rotationSpeed` = 90 / 90 = **1 s**.
- `RotateTowards` never overshoots, like `MoveTowards` does for speed.
- Push-out distance is the penetration depth plus 1 mm.

## Reasoning

- **Interface decoupling.** The plate only knows `IPowered`, so anything with `SwitchOn/SwitchOff` can be plugged in.
- **Target angle recomputed every frame.** Stepping off the plate mid-swing reverses from the current angle instead of snapping.
- **Doors are plain colliders, not platforms.** In CustomPlay the door is a pivot object with a child `BoxCollider` and **no Rigidbody**. Platform carry needs `attachedRigidbody`, so standing on a door never carries you; it only pushes you out of the way.
- **`Depenetrate` is the door's push, not `AcceptExternalMovement`.** The comment on `AcceptExternalMovement` mentions doors shoving you, but that function only reacts to the **Rigidbody** being moved, and doors never touch it.
- **Rotation in `Update`, push-out in `FixedUpdate`.** The door can move several frames between physics ticks, so the push-out happens on the next tick.
