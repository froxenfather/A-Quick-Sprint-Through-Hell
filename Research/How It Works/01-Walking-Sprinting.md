# Walking / Sprinting

**Runs:** step 8, `Move()` -> `GroundMove`. After the jump/slide decisions, before collision.

| Variable | Old System | Custom System |
|---|------------|---------------|
| `walkSpeed` | 12         | 12            |
| `sprintSpeed` | 20         | 25            |
| `groundAccel` | 90.9       | 100           |
| `groundOverspeedDecel` | 52.1       | 30            |
| `groundFriction` | 68.6       | 200           |
| `landingGrace` | 0.04       | 0.04          |

## Steps

**Input (`Update`)**: WASD -> `Vector2`, then `.normalized` (without it diagonals are 1.414× faster). Hold Shift = sprint.

**Each physics tick (`GroundMoveTowards`)**:

1. `wish = flatForward * input.y + flatRight * input.x`
2. `v = ProjectOnPlane(rel, groundNormal)` -> removes any velocity into or out of the ground.
3. **Input held:**
   - `dir = ProjectOnPlane(wish, groundNormal).normalized`
   - `target = dir * (sprint ? sprintSpeed : walkSpeed)`
   - `rate = |v| > targetSpeed ? groundOverspeedDecel : groundAccel`
   - `v = MoveTowards(v, target, rate * dt)`
4. **No input**, and not within `landingGrace` of landing: `v = MoveTowards(v, 0, groundFriction * dt)`

`MoveAndSlide` then moves the player by `v * dt`.

Crouch-walking is the same function with `targetSpeed = crouchSpeed` (5).

## Math

- Plane projection: `v_plane = v - n * dot(v, n)`
- `MoveTowards` changes velocity by at most `rate * dt` per tick (0.02 × 100 = 2 m/s per tick).
- Time to reach speed = `target / accel`: walk 12/100 = **0.12 s**, sprint 25/100 = **0.25 s**.
- Stop from sprint = 25/200 = **0.125 s**.
- Landing at 40 m/s while sprinting bleeds to 25 at 30 m/s² -> **0.5 s**.

## Reasoning

- **Replaced instant `velocity = dir * speed`** (devlog 2026-09-10). Air already felt good (accelerate toward a direction); the ground now eases the same way.
- **`MoveTowards` is self-limiting.** It never overshoots, so holding W settles at exactly the target. No separate speed-cap check. Turning the camera rotates the target vector and velocity follows.
- **Two rates.** With one rate, landing hot (launch pad, air) while holding forward would brake like a wall. Over-speed bleed is its own, softer rate.
- **Friction is constant deceleration, not `= 0`.** Near-instant stop, but still scaled by `dt`.
- **Landing grace.** Friction used to zero momentum within two ticks of landing, so jumps couldn't chain. For 0.04 s after touchdown the no-input friction branch is skipped (bunny-hop window). Only the no-input branch is skipped.
- **Slope projection.** "Forward" becomes "forward along the ramp", so you keep full target speed on inclines instead of fighting the normal.
- **Platform-relative.** `v` comes from `rel`, so walking on a moving platform needs no special case.
