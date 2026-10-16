# Wall Jumping

**Runs:** detection in step 11 `UpdateWallContact` (end of tick N) -> used by step 7 `Jump` -> `TryWallJump` (tick N+1).

| Variable | Old System | Custom System |
|---|---|---|
| `wallJumpHeightMultiplier` | 0.66 | 1 |
| `wallJumpPushAway` | 10 | 10 |
| `wallJumpForwardBoost` | 8 | 3 |
| `wallJumpLookTolerance` | 30° | 30° |
| `wallDetectDistance` | 0.15 | 0.15 |
| `wallProbeDirections` | 8 | 8 |
| `wallMaxNormalY` | 0.3 | 0.3 |

## Steps

**1. Detect (step 11, airborne only; grounded players skip it).**

- Sweep the capsule in `wallProbeDirections` horizontal directions: `Euler(0, 360 * i / 8, 0) * forward`, each `wallDetectDistance + skinWidth` long.
- Keep the **closest** hit that is wall-shaped: `|normal.y| <= wallMaxNormalY` and not walkable.
- Re-read the real surface normal with a ray from the closest point on the capsule's centre line to the contact (capsule hit normals lie near edges).
- Flatten the normal. `tangent = cross(up, normal).normalized`.
- Result stored in `currentWall`. It is cleared at the start of every tick, so an old wall can't linger.

**2. Look check (`EvaluateWallLook`).** Re-run at jump time against where you face **now**:

- `look` = flat forward.
- `a+ = Angle(look, tangent)`, `a- = Angle(look, -tangent)`. Take the smaller; that direction becomes `forwardTangent`.
- `lookOk = smaller angle <= wallJumpLookTolerance`.

**3. Jump (step 7).** Ground/coyote jump is checked first; the wall jump only runs if that fails. Needs: airborne, `currentWall.valid`, `lookOk`.

- `v.y = jumpVelocity * wallJumpHeightMultiplier` (assigned)
- `v.xz += normal * wallJumpPushAway + forwardTangent * wallJumpForwardBoost`
- `airSpeedCap = max(airSpeedCap, new flat speed)`
- `sliding = false`
- `wallRunTimeUsed = 0` and `currentWall` cleared (refills the wall run grace period; see `09-Wall-Running.md`)

No cooldown or counter exists.

## Math

- Tangent: wall normal (0,0,1) -> `cross(up, n)` = (1,0,0). Works for a wall at any angle. Takes angle of where YOU are
- Look cone: ±30° around **each** direction along the wall = two 60° cones.
- Custom System numbers: `y = 20 * 1 = 20`; flat boost = 10 away from the wall + 3 along it.

## Reasoning

- **Separate wall scan.** `MoveAndSlide` only sweeps in the direction you're moving. Wall jumps happen while moving *along* the wall, so you'd almost never "hit" it. A ring of sideways sweeps catches it.
- **Cross product, not hardcoded left/right.** Walls can face any direction.
- **Look along the wall, not into it.** Never compares against the normal. It also gives a `forwardTangent` that a future wall run can reuse.
- **Cached wall, live look.** Detection is from the end of the last tick, but the mouse can turn you between ticks, so the look check is redone at jump time.
- **Additive horizontal boost, assigned vertical.** Height is the same no matter how fast you were falling; the flat part adds to the speed you already have (same as the slide jump).
- **Raises `airSpeedCap`.** Otherwise `AirMove` clamps the boost away on the very next line. `Max()` so it can never lower the cap.
- **Ends an air slide.** `SlideAirMove` would lock momentum and ignore air control.
- **Ground jump wins.** Near a ledge and a wall you get the stronger ground jump.

## Issues

- don't speak to me about curved walls yet...
- Curved walls keep the normal of the first surface you determined, there needs to be polgonal structure like a dodecahedron to really feel the wall out.
- Working on this in the future but wasy fix is just to avoid curved walls for now
- 
