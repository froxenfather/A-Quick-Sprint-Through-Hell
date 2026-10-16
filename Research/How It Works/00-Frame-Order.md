# Frame Order (what runs when)

Source: `CustomPlayerController.cs` -> `FixedUpdate()`. Fixed timestep = 0.02 s (50 Hz), so `dt = 0.02`.

`Update()` only reads input. Jump and slide presses are stored as flags (`jumpQueued`, `slideQueued`) so a one-frame press can't fall between two physics steps.

All movement math works in **platform-relative velocity**: `rel = velocity - platformVelocity`.

| # | Step | Module it belongs to |
|---|------|----------------------|
| 1 | `AcceptExternalMovement` | adopt the Rigidbody's position if something else moved it |
| 2 | `ApplyPlatformCarry` | **Moving Platform Velocity** |
| 3 | `Depenetrate` | **Doors** (push-out) |
| 4 | `rel = velocity - platformVelocity` | |
| 5 | capture `airSpeedCap` (only if grounded) | **Air Movement** |
| 6 | `UpdateSlideAndCrouch` | **Sliding** (start / stop) |
| 7 | `Jump()` then `LaunchOffMovingGround` | **Coyote Time**, **Wall Jumping**, slide jump |
| 7b | `UpdateWallRun` | **Wall Running** (decides `wallRunning` for this tick) |
| 8 | `Move()` picks ONE model | **Walking/Sprinting**, **Air Movement**, **Sliding**, **Wall Running** |
| 9 | `MoveAndSlide(rel * dt)` | collision, step-up, slope redirect |
| 10 | `UpdateGrounded` | ground snap, coyote timestamp, landing time |
| 11 | `UpdateWallContact` | **Wall Jumping** (detection, used next step) |
| 12 | `velocity = rel + platformVelocity` | |
| 13 | `DispatchContacts`, `RecordPlatformAnchor`, `CommitPosition` | **Launchpads**, **Moving Platform Velocity** |

## The pattern

1. Edit velocity (steps 5-8).
2. Move position once (step 9).
3. Re-measure the world: grounded, slope, wall, contacts (steps 10-13).

Anything measured in steps 10-11 is read by steps 5-8 on the **next** tick.

## `Move()` state picker (step 8)

```
wallRunning -> WallRunMove (y = 0, nothing else)
sliding  -> grounded ? SlideGroundMove : SlideAirMove
grounded -> crouching ? CrouchGroundMove : GroundMove
else     -> AirMove
```

## Tuning values

Each doc lists **Old System / Custom System** values.

- **Old System** = the values in the code and in the older scenes (`GamePlay.unity`, `SampleScene.unity`).
- **Custom System** = `CustomPlay.unity` (last saved 2026-10-05), the newest scene. It overrides most of the Old System values, so the worked examples use Custom System numbers.
