# Coyote Time

**Runs:** timestamp in step 10 `UpdateGrounded`; check in step 7 `Jump` -> `InCoyoteWindow`.

| Variable | Old System | Custom System |
|---|---|---|
| `coyoteTime` | 0.1 | **1.4** |

## Steps

1. **Stamp (step 10).** Every tick that ends on walkable ground: `lastGroundedTime = Time.time`.
2. **Walk off an edge.** `grounded` becomes false and the stamp stops updating, so `Time.time - lastGroundedTime` starts counting up.
3. **Jump (step 7).** If `grounded || Time.time - lastGroundedTime <= coyoteTime` -> `PerformGroundJump`. Same function as a normal jump; `v.y = jumpVelocity` overwrites any fall speed.
4. **Reset.** `lastGroundedTime = -999` (`NeverGrounded`) whenever we leave the ground **on purpose**:
   - a ground jump
   - `LaunchOffMovingGround` (a platform stopping or slowing under you; walking off a ledge does **not** count, see `06-Moving-Platform-Velocity.md`)
   - `SetVelocity` (launch pad)
   - `Teleport`

## Math

- Window in ticks = `coyoteTime / dt`: 0.1 s = **5 ticks**, 1.4 s = **70 ticks**.
- `-999` makes `Time.time - lastGroundedTime` huge, so the check can never pass.

## Reasoning

- **Reset on purpose-leaving.** Otherwise a jump leaves the window open and you could jump a second time mid-air.
- **Same function as a normal jump.** A coyote jump automatically gets the slide-jump boost and the same height. No duplicated jump code.
- **No input buffering.** `jumpQueued` is consumed every tick whether or not the jump worked, so pressing Space mid-air doesn't secretly queue a jump for later.
- **Priority.** Ground/coyote is checked before the wall jump.

> Heads-up: the Custom System has `coyoteTime = 1.4`, which looks like a test value (Old System is 0.1). With it, walking off a ledge gives a ground jump for 1.4 s, and because the ground jump has priority, a wall jump can't fire during that time.
