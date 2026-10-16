# Moving Platform Velocity

**Runs:** step 2 `ApplyPlatformCarry` (first thing that moves us) · step 4 / 12 (relative <-> world velocity) · step 7 `LaunchOffMovingGround` · step 13 `RecordPlatformAnchor`.

## Steps

**End of tick N (step 13, `RecordPlatformAnchor`):**
If grounded and the ground collider has an `attachedRigidbody`:
- `platform = that transform`
- `platformLocalPos = platform.InverseTransformPoint(position)` (our spot in the platform's own coordinates)
- `platformLastRot = platform.rotation`

Otherwise `platform = null`. Ground with no Rigidbody never carries.

**Start of tick N+1 (step 2, `ApplyPlatformCarry`):**

1. **Rotate.** If `platform.up · up > 0.7` (mostly upright): `rotationDelta = platform.rotation * Inverse(platformLastRot)`, take its yaw, `transform.Rotate(0, yaw, 0, World)`.
2. **Carry.** `platformDelta = platform.TransformPoint(platformLocalPos) - position`
3. `MoveAndSlide(platformDelta, ..., allowStep: false)` (carry respects collision).
4. `platformVelocity = platformDelta / dt`

**Then the rest of the tick runs in the platform's frame:**

- Step 4: `rel = velocity - platformVelocity`
- Steps 5-11: all movement math uses `rel`
- Step 12: `velocity = rel + platformVelocity`

**Platform stops or slows (step 7, `LaunchOffMovingGround`):**
`speedAway = dot(previousPlatformVelocity - platformVelocity, groundNormal)` (how much speed the platform lost this tick). If `speedAway > gravity * dt` -> we left the ground: `grounded = false`, coyote reset.

It uses the platform's lost speed, **not** our own velocity. Walking off a ledge tilts `groundNormal` toward our walking direction (the capsule touches the edge corner), so our own speed used to look like "the ground launched us" and wiped the coyote window.

## Math

- Local anchor: `p_local = T⁻¹(p)`, new world spot `p' = T'(p_local)`.
- Platform velocity: `v = Δx / Δt`.
- Launch test: gravity can only pull back `g * dt` = 0.8 m/s per tick (g = 40), so moving away faster than that means the ground let go.
- `HorizontalSpeed` (used for speed readouts) = `|flat(velocity - platformVelocity)|`, so riding a fast platform doesn't count as running.

## Reasoning

- **Local-space anchor.** One formula covers translation **and** rotation: if the platform spins, the anchor orbits the pivot automatically. Yaw is applied separately so you also turn with it (tested with `SpinningPlatform`).
- **Velocity from displacement.** Works for any Rigidbody-backed mover, including kinematic platforms (`MovingPlatform` uses `MovePosition`), without reading the Rigidbody's own velocity.
- **Platform-relative movement.** Walking, sliding and air control are written once and stay correct on any platform; the platform velocity is added back at the end.
- **Momentum when a platform stops.** A platform that decelerates leaves its old speed in `rel`. Pointing up, it launches you (a platform that stops or slows faster than gravity can pull you back leaves you flying with the speed it had). Pointing sideways, ground friction takes it over.
- **Carry goes through `MoveAndSlide`.** A platform can't shove you through a wall.
- **Upright check (0.7).** Tilted platforms don't spin your view.
- **`MovingPlatform.cs`** runs 4 move phases (left, up, right, down), each followed by a pause, at constant `distance / phaseTime`. The abrupt stop at each phase end is the test for the launch rule.
