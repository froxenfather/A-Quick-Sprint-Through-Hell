# Launchpads

**Runs:** contact is recorded in steps 9-10, fired in step 13 `DispatchContacts`, and the new velocity takes effect in step 4 of the **next** tick.

| Variable (`LaunchBehavior`) | Value |
|---|---|
| `launchForward` | 5 |
| `launchUp` | 5 |

(No launchpad is placed in the CustomPlay scene; these are the component defaults.)

## Steps

1. During `MoveAndSlide` and `UpdateGrounded`, every collider we hit goes into `contactsThisStep`.
2. Step 13, `DispatchContacts`: for each collider **not** in `contactsLastStep`, if it has an `IPlayerContact` -> `OnPlayerContact(player)`.
3. `LaunchBehavior.OnPlayerContact` calls
   `player.SetVelocity(pad.up * launchUp + pad.forward * launchForward)`
4. `SetVelocity`: `velocity = worldVelocity`, `grounded = false`, `platform = null`, `lastGroundedTime = -999`.
5. Next tick, we start airborne:
   - no ground snap (`snapping` needs `wasGrounded`)
   - `UpdateGrounded`'s moving-away check (`dot(v, n) > 0.1`) blocks re-grounding on the pad
   - `AirMove`: `limit = max(speedBefore, airSpeedCap)`, so the launch speed isn't clamped by the takeoff cap; only `maxAirSpeed` bleeds it.

## Math

- World-space launch vector: `v = up * launchUp + forward * launchForward`. Rotating the pad aims the launch.
- Peak height from the vertical part: `launchUp² / 2g`.
- Defaults (5 up, 5 forward) give a gentle hop. Anything over `maxAirSpeed` flat bleeds at `airOverspeedDecel`.

## Reasoning

- **The controller reports contacts itself.** The player is kinematic, so Unity's `OnCollisionEnter` doesn't fire for it. `IPlayerContact` is how the custom controller tells objects "you were touched".
- **First contact only.** Comparing against last tick's set means standing on the pad doesn't re-fire every tick.
- **`SetVelocity` replaces velocity.** The launch is the same no matter how fast you arrive.
- **Clears coyote.** A launch is not walking off an edge.
- **Leftover:** `LaunchBehavior.OnCollisionEnter` still references the old Rigidbody `PlayerMovement`. It's from the Rigidbody controller; the custom controller only uses `OnPlayerContact`.
