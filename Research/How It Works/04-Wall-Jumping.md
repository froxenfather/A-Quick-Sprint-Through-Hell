# Wall Jumping

**Runs:** detection in step 11 `UpdateWallContact` (end of tick N) -> used by step 7 `Jump` -> `TryWallJump` (tick N+1).

| Variable | Old System | Custom System |
|---|---|---|
| `wallJumpHeightMultiplier` | 0.66 | 1 |
| `wallJumpPushAway` | 10 | 10 |
| `wallJumpForwardBoost` | 8 | 3 |
| `wallJumpLookTolerance` | 30° | 30° |
| `wallJumpAnyDirection` | off | new, off until ticked in the scene |
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

**3. Jump (step 7).** Ground/coyote jump is checked first; the wall jump only runs if that fails. Needs: airborne, `currentWall.valid`, and `lookOk` **unless `wallJumpAnyDirection` is on**, in which case any wall touch is enough. (`TryGetWallContact` does the airborne + wall + live look part; wall running reuses it and always demands `lookOk`.)

- `v.y = sqrt(2 * gravity * wallJumpHeight)` (assigned)
- `v.xz += pushNormal * wallJumpPushAway`, where `pushNormal` comes from step 4
- `v.xz += forwardTangent * wallJumpForwardBoost` **only if `lookOk`**. A kick from outside the look range (only possible with `wallJumpAnyDirection`) has no along-the-wall direction, so it goes straight away from the wall.
- `airSpeedCap = max(airSpeedCap, new flat speed)`
- `sliding = false`
- `wallRunTimeUsed = 0` and `currentWall` cleared (refills the wall run grace period; see `09-Wall-Running.md`)

**4. Push direction (`ResolveWallJumpNormal`, only runs on a jump).**

- Re-run the probe ring and collect every wall-shaped surface in range, using the same corrected normals as detection. Normals within ~15° of each other count as one wall.
- One wall: the push is exactly `currentWall.normal`.
- Several walls (a corner): the push is the normalized **sum** of their normals, which points out of the corner, away from all of them. A 90° corner kicks along the diagonal.
- If the sum is shorter than 0.5 (walls facing nearly opposite, like a narrow shaft), fall back to the closest wall's normal. Otherwise the sum would normalize into a direction along the walls.

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
- **`wallJumpAnyDirection` is a toggle, not a bigger cone.** It lets you run face first into a wall, jump up it, and kick off up and backwards. The push always stays along the wall normal; only the look gate and the sideways boost change.
- **Corner normal is computed at jump time, not every tick.** Detection still keeps one closest wall (slip, wall run and the look check use it), so a corner costs nothing per tick. Pushing along one face of a V-shaped corner would drive into the other face and `SlideAlongSurface` would eat the kick, leaving only a straight-up jump.

## Issues

- don't speak to me about curved walls yet...
- Curved walls keep the normal of the first surface you determined, there needs to be polgonal structure like a dodecahedron to really feel the wall out.
- Working on this in the future but wasy fix is just to avoid curved walls for now
- 
