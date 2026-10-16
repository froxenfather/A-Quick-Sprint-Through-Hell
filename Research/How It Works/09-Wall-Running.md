# Wall Slip / Wall Running

Two small mechanics layered on top of Wall Jumping. Both read the same `currentWall` that wall jumping uses (see `04-Wall-Jumping.md`).

**Runs:** step 7b `UpdateWallRun` (after `Jump`, before `Move`) -> step 8 `Move()` -> `WallRunMove` or the air models' `CurrentGravity()`.

| Variable | Default | Meaning |
|---|---|---|
| `wallSlipGravityScale` | 0.5 | Gravity multiplier while touching a wall (the "slipping speed"). 0.5 = half gravity |
| `wallRunMinSpeed` | 20 | Flat speed needed to start AND keep a wall run |
| `wallRunGracePeriod` | 1.5 s | Wall run time allowed before Y unpauses |

New code defaults only. Check `CustomPlay.unity` for overrides.

## The three things that can happen at a wall

1. **Jump at once:** a wall jump (unchanged).
2. **Stick to it:** wall slip. Gravity is `gravity * wallSlipGravityScale` while touching.
3. **Run into it at speed with sprint held:** wall run. Y is paused until the grace period ends, then you slip as in 2.

## Wall slip

- `CurrentGravity() = currentWall.valid ? gravity * wallSlipGravityScale : gravity`.
- Used by `AirMove` and `SlideAirMove`. Ground movement never calls it.
- "Touching" = `currentWall.valid`: airborne and a wall within `wallDetectDistance` (0.15 m, plus `skinWidth`). **No look check**, so any wall contact slips you.
- `currentWall` is measured at the end of the last tick, so the slip starts one tick after contact and ends one tick after you leave the wall.
- It is an **acceleration**, not a top speed. At `gravity` 40 the slip is 20 m/s^2, so you reach -10 m/s after 0.5 s (instead of -20).

## Wall run (`UpdateWallRun`)

All of these must hold, or `wallRunning` is false this tick:

1. **Every wall jump requirement.** Calls `TryGetWallJumpWall`, the same helper `TryWallJump` uses: airborne, wall in range, flat look within `wallJumpLookTolerance` of a direction along the wall.
2. **Sprint held.**
3. **Flat speed >= `wallRunMinSpeed`** (minus a 0.05 m/s float tolerance).
4. **Not moving away from the wall** (`dot(v, wall.normal) <= 0.1`).
5. **Grace left:** `wallRunTimeUsed < wallRunGracePeriod`.

While true: `wallRunTimeUsed += dt`, and `Move()` calls `WallRunMove`, which sets `v.y = 0` and changes nothing else.

When the grace period runs out, requirement 5 fails, `AirMove` runs with slip gravity from `y = 0`, and you slip down the wall.

### Grace period refill

- **Wall jump** (`ApplyWallJump`): `wallRunTimeUsed = 0`.
- **Touching the ground** (`UpdateWallRun`, grounded branch): `wallRunTimeUsed = 0`.
- Merely leaving a wall does **not** refill it. Only time actually spent wall running is counted.

## Math

- Wall run distance at 20 m/s and 1.5 s = **30 m** of wall.
- Slip speed after t seconds from rest = `g * scale * t` (40 * 0.5 * 0.5 = 10 m/s).
- Wall run -> slip: `v.y` goes 0 -> -0.4 m/s per tick (20 * 0.02).

## Reasoning

- **Multiplier, not m/s^2.** Half of gravity keeps following `gravity` if it is retuned, same pattern as `slideSlopeGravityScale`.
- **Slip needs no look check.** The spec says any wall touch slips you; only the jump and the run need the look angle.
- **Shared requirements helper.** A wall run needs exactly what a wall jump needs, so both call `TryGetWallJumpWall` and can't drift apart.
- **Momentum locked, Y paused.** "Wall runs do not hinder forward momentum whatsoever." Skipping `AirMove` also skips its air control, takeoff-speed cap and overspeed bleed. Pausing Y (`y = 0`) covers rising and falling in one line.
- **Overrides an air slide.** `Move()` checks `wallRunning` first. Slide jump -> wall run -> wall jump is the intended chain (original syllabus Week 10, now consolidated under Week 8 as complete).
- **Grace is a budget.** It refills on a wall jump or landing, so chaining wall jumps between walls is unlimited but you can't hop on and off one wall for free time.
- **"Moving away" guard.** After a wall jump the push-away is 10 m/s, but if it is ever tuned small the wall can still be in range next tick; without this a wall run would zero the jump's upward speed.
- **`ApplyWallJump` clears `currentWall`.** The wall we just pushed off must not give slip gravity or a wall run in the same step.
- **Float tolerance.** `MoveTowards` on a vector can leave the speed at 19.99999.

## Issues

- **No steering during a wall run** (momentum is locked). You must approach roughly parallel to the wall, and there is no stick-to-wall force: drift away and you leave `wallDetectDistance` and fall.
- **Angled contact bleeds speed.** Hitting the wall at an angle goes through `SlideAlongSurface`, which removes the into-wall component. At 30 degrees that is about 13% of your speed, which can drop you under 20 and block the run.
- Curved walls: same issue as in `04-Wall-Jumping.md`.
