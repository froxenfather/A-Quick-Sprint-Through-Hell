# Sliding

**Runs:** step 6 `UpdateSlideAndCrouch` (decide) **before** step 7 `Jump` · step 8 `SlideGroundMove` / `SlideAirMove` · step 9 special case in `SlideAlongSurface`.

| Variable | Old System | Custom System |
|---|---|---|
| `slidingGroundFriction` | 4 | 4 |
| `slideBoost` | 5 | 3 |
| `slideBoostMaxSpeed` | 25 | 26 |
| `slideMinSpeed` | 6.7 | 15 |
| `slideSteerAccel` | 10 | 15 |
| `slideSlopeGravityScale` | 1 | 1 |
| `slideUphillGravityScale` | 0.35 | 0.8 (*) |
| `slideGrace` | 0.5 | 0.5 |
| `slideJumpBoost` | 8 | 5 |
| `slideLandingBoostScale` | 1 (full conversion) | 0.35 |
| `slideLandingBoostMax` | none | 12 |

(*) The code default is now 0.8, but `CustomPlay.unity` serializes its own value, so set it to 0.8 on the player in the Inspector.

## Steps

**Input:** mouse back/forward thumb button. Press is queued, hold is `slideHeld`.

**Start (step 6)**, on a press while not already sliding or crouching:

- `speed >= slideMinSpeed` **and** `now - slideStartTime >= slideGrace`, **or** airborne -> **slide**.
  - Set `slideStartTime = now`.
  - If `speed < slideBoostMaxSpeed`: `v.xz += flatDir * slideBoost`.
- Otherwise, if grounded -> **crouch** (move at `crouchSpeed`, capsule shrinks to `crouchHeight`, feet stay put).
- Releasing the button is the **only** way a slide ends.

`speed` here is `calculatedMovement.magnitude`: the full 3D magnitude, so vertical speed counts too.

**Ground slide tick (`SlideGroundMove`):**

1. `v = ProjectOnPlane(rel, groundNormal)`
2. **Steer:** `v += right * input.x * slideSteerAccel * dt`. If `|v|` grew, rescale back to the pre-steer speed.
3. **Slope:** `downhill = ProjectOnPlane(down, n).normalized`
   `v += downhill * gravity * sin(slopeAngle) * scale * dt`
   `scale` = `slideSlopeGravityScale` going downhill, `slideUphillGravityScale` going uphill.
4. **Friction:** `v = MoveTowards(v, 0, slidingGroundFriction * dt)`

**Air slide tick (`SlideAirMove`):** gravity only. No steering, no speed caps.

**Slide jump (step 7):** a ground jump while `sliding` and `now - slideStartTime < slideGrace`:
`v.xz += flatDir * slideJumpBoost`, then `airSpeedCap = flat speed`.
Holding the button through the jump keeps you in the air slide (momentum locked).

**Step 9 special case (`SlideAlongSurface`):** sliding onto a walkable surface redirects velocity with `ProjectOnPlane`, then rescales it:

1. `tangentSpeed = |v|` after the projection.
2. `extraSpeed = |v_before| - tangentSpeed` (the part that pointed into the surface).
3. **Grounded last step** (ramp-to-ramp): keep all of `extraSpeed`, so the speed is unchanged.
4. **Airborne last step** (landing from the air): `extraSpeed = min(extraSpeed * slideLandingBoostScale, slideLandingBoostMax)`.
5. `v = v.normalized * (tangentSpeed + extraSpeed)`

Walkable-to-walkable double hits aren't treated as a wedge.

## Math

- `g * sin(θ)` is the part of gravity pointing along a slope. At 30°, g = 40: **20 m/s²** downhill, **16 m/s²** uphill (× 0.8; it was 7 at × 0.35).
- Valley (two facing ramps), with `a = g * sin(θ)`, `f = slidingGroundFriction`, `s = slideUphillGravityScale`: speed at the bottom changes by `v_next² = v² * (a - f) / (s * a + f)` per half cycle. At 30°: s = 0.35 gives ×1.45 (grows), s = 0.5 gives ×1.14 (still grows), s = 0.8 gives ×0.80 (decays). It can't grow on any slope up to 60° once `s > 1 - 2f / (g * sin 60°)` ≈ 0.77.
- Landing boost, 20 m/s flat, falling 40 m/s: old = 44.7 m/s; now = 20 + 24.7 × 0.35 ≈ **28.6 m/s**. A very tall drop is capped at tangent speed + 12.
- Friction: 4 × 0.02 = 0.08 m/s per tick -> ~6 s to stop from 25 m/s on flat ground.
- Entry boost only applies under `slideBoostMaxSpeed`, so the most it gives is roughly 26 + 3 = **29 m/s**.
- Plain projection on a ramp keeps `cos(angle)` of speed (~77% at 40°); the redirect avoids that loss.

## Reasoning

- **Low friction (4 vs 200).** Momentum carries. W/S do nothing, A/D only drift: a slide is a commitment, not steering.
- **Steer can't add speed.** Capping against the pre-steer speed means it can turn you but not accelerate you, and can't climb from a dead stop (0 clamps to 0).
- **Uphill scale 0.8.** Full gravity uphill (1.0) kills speed as hard as it builds it downhill, which eats the momentum you need to carry over a lip and launch. Feel over realism. It used to be 0.35, but anything much below 0.77 lets two facing ramps feed speed into each other forever (see the valley math), so 0.8 is the lowest round value that is safe.
- **Speed-preserving redirect.** Ground accel hides the loss when running, but a slide has no re-acceleration, so any loss is permanent and reads as a wall hit. Ramp-to-ramp keeps the full speed.
- **Landing boost kept, but scaled and capped.** Landing from the air used to convert **all** the fall speed into horizontal speed (the redirect used the full 3D magnitude). It felt good, so it stays, but only `slideLandingBoostScale` of it counts and the bonus can't pass `slideLandingBoostMax`. Set the scale to 1 and the max high to get the old behavior back.
- **`grounded` as the "was airborne" test.** Inside step 9 it still holds last step's value (jumps and launches clear it, `UpdateGrounded` runs after), so `!grounded` means we are landing.
- **Decide before jumping.** Pressing slide and jump on the same frame still counts as a slide jump.
- **One shared `slideGrace` timer.** It blocks starting a new slide and defines the slide-jump window.
- **Air slides skip min speed and grace.** You can start one mid-air.
- **Slide-jump sets `airSpeedCap`.** Otherwise `AirMove` would clamp the boost on the next line.
