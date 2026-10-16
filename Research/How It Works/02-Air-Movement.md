# Air Movement / Max Air Speed / Air Control

All three are one function, `AirMove`, with three layers: **air control** (accelerate), **air speed cap** (takeoff speed), **max air speed** (soft ceiling).

**Runs:** step 5 captures the cap, step 7 sets jump height, step 8 `Move()` -> `AirMove`.

| Variable | Old System | Custom System |
|---|---|---|
| `jumpVelocity` | 14.68 | 20 |
| `gravity` | 30.01 | 40 |
| `airControl` | 15.22 | 50 |
| `maxAirSpeed` | 20 | 20 |
| `airOverspeedDecel` | 52.1 | 15 |

## Steps

**Step 5: capture the cap.** Every tick that starts grounded:
`airSpeedCap = max(walkSpeed, flat |rel|)`.
When we leave the ground it stops updating, so it holds our **takeoff speed**. (`walkSpeed` is the floor so a standing jump can still reach walk speed.)

**Step 7: jump.** `v.y = jumpVelocity` (assigned, not added).

**Step 8: `AirMove` each tick:**

1. `before = |flat v|`
2. **Air control:** `v += wish * airControl * dt`
3. **Cap:** `limit = max(before, airSpeedCap)`. If `|flat v| > limit`, rescale to `limit` (direction kept).
4. **Max air speed:** if `|flat v| > maxAirSpeed`, `speed = MoveTowards(speed, maxAirSpeed, airOverspeedDecel * dt)` (direction kept).
5. **Gravity:** `v.y -= gravity * dt`. Applied last, never clamped.

Then `MoveAndSlide` moves by `v * dt` (velocity first, then position: semi-implicit Euler).

## Math

- Gravity: `v = v0 - g * dt` -> 0.8 m/s per tick at g = 40.
- Jump apex = `v² / 2g` = 400 / 80 = **5 m**. Rise time = `v / g` = **0.5 s**. Airtime on flat ground ≈ **1.0 s**.
- Air control: 50 × 0.02 = **1 m/s of steering per tick**.
- Sprint takeoff at 25 m/s with `maxAirSpeed` 20: bleeds at 15 m/s² -> down to 20 in **0.33 s**.

## Reasoning

- **Air can turn and brake you, never speed you up.** The cap equals the speed you left the ground with. Rescaling keeps direction, so holding strafe and swinging the mouse can't pump speed (the Quake-style tiny wish-speed was rejected in the devlog for exactly that exploit).
- **`max(before, cap)`.** If you're already faster than the cap (launch pad, slide jump), the limit rises to your current speed, so the cap never clamps boosts. Boost sources also raise `airSpeedCap` themselves.
- **Max air speed bleeds instead of clamping.** A hard clamp "feels like glass" (devlog). `airOverspeedDecel` is the knob: high behaves like a clamp, low lets momentum carry. At 15 it is a fast-but-visible slowdown.
- **Gravity last.** Clamping before gravity means gravity can never be capped by accident.
- **Gravity is `CurrentGravity()`.** While touching a wall it is `gravity * wallSlipGravityScale` (half by default), otherwise plain `gravity` (see Wall Running).
- **Sliding in the air skips all of this.** `SlideAirMove` is gravity only (see Sliding).

> Outdated note: the devlog roadmap (Pass 2) planned a *directional soft cap* (project velocity onto the wish direction). What shipped is the takeoff-speed cap above.
