# Dash

An instant turn toward where you look, plus some speed. Built for quick trajectory corrections that keep your momentum instead of resetting it.

**Runs:** step 6b `TryDash` (after `UpdateSlideAndCrouch`, before `Jump`). The FOV punch is `SpeedFOV.cs`, per frame.

**Input:** `E`, or the mouse forward thumb button (button 5). Press is queued (`dashQueued`), same trick as `jumpQueued`. Slide uses the back thumb button (button 4) only.

| Variable | Default | Meaning |
|---|---|---|
| `dashDirectionInfluence` | 80 % | How much of the angle toward your look direction the dash turns you. 100 = exactly where you look, 10 = mostly keeps your direction |
| `dashSpeedBonus` | 5 | Added to your current flat speed (m/s) |
| `dashMinSpeedSprint` | 35 | Dash speed never ends up below this while Shift is held |
| `dashMinSpeedWalk` | 20 | ...and never below this otherwise |
| `maxDashCharges` | 3 | Dashes you can bank. Each dash uses one, and dashes can be done as fast as you press the button while you have charges |
| `dashRechargeTime` | 3 s | Time to earn one charge back. Charges return one at a time |
| `dashHangTime` | 0.25 s | Seconds of no gravity after a dash (level glide). 0 = off |

## Steps (`TryDash`)

1. Take the queued press (it is used up either way, so pressing with no charge does not buffer a dash). Stop if `DashCharges < 1`.

   **Charges:** one float, `dashCharges`. Every physics step it goes up by `dt / dashRechargeTime` (capped at `maxDashCharges`), and a dash takes 1 off, so progress toward the next charge is kept and charges come back one at a time. The HUD reads `DashCharges` and `MaxDashCharges`.
2. `flat` = flat velocity (relative to the platform you stand on), `look` = `Flatten(transform.forward)`, the camera's yaw. Same "look" the walk and wall jump code use.
3. **Direction:**
   - Standing still (flat speed under 0.01): go along `look`. If you are looking dead up or down there is no flat look, so the dash does nothing.
   - Otherwise: `angle = SignedAngle(flatDirection, look, up)`, then rotate `flatDirection` by `angle * influence / 100` around `up`.
4. **Speed:** `newSpeed = max(flatSpeed + dashSpeedBonus, sprinting ? dashMinSpeedSprint : dashMinSpeedWalk)`.
5. Set `x` and `z` to `direction * newSpeed` and **`y` to 0**, so a dash also cancels any rising or falling. Gravity starts pulling again on the next tick (and a jump pressed on the same frame still sets its own upward speed afterwards).
6. `airSpeedCap = max(airSpeedCap, newSpeed)`, spend one charge, `dashHangEndTime = Time.time + dashHangTime`, fire the `Dashed` event.

**Hang:** until `dashHangEndTime`, `CurrentGravity()` returns 0, so the air models add no gravity and we glide level. A ground jump or wall jump resets `dashHangEndTime`, so the jump rises and falls normally. `dashHangTime = 0` turns it off. Wall slip and the wall run need no special case.

Nothing else: no state flag. `Move()` picks its model afterwards from the usual state, so the dash works in every state.

## What it does in each state

- **Ground, air:** the new velocity is just the starting point for that tick's `GroundMove` / `AirMove`.
- **Ground slide:** the slide goes on (the dash never touches `sliding`, `slideStartTime` or the grace window, so a slide jump after a dash still gets its boost). `SlideGroundMove` projects onto the ground each tick, so on a ramp the along-ground speed is a little below the horizontal dash speed. Slide friction is very low, so a slide dash keeps its speed much longer than a walking one.
- **Air slide:** `SlideAirMove` only adds gravity, so the new direction and speed simply carry.
- **Wall run:** `WallRunMove` leaves everything but Y alone, so a dash steers the run. Dashing into the wall can slide you along it; dashing away ends the run through the "moving away" check.
- **Launch pad:** `SetVelocity` just sets the velocity, so a dash right after it redirects the launch.
- **On a moving platform:** works in platform-relative speed, like everything else.

## Air speed cap

`airSpeedCap` is the hard limit on what air control can add (it can never push you past the speed you took off with). The dash raises it with `Max()` so a dash can never lower it, and a lower cap would clamp the boost away on the next line, the same reason as the wall jump. Anything above `maxAirSpeed` (20) still bleeds back toward it at `airOverspeedDecel` (15), which is the soft limit. The cap just lets air control rebuild up to the dash speed afterwards.

On the ground there is no air cap involved: `GroundMoveTowards` pulls overspeed back to the sprint speed at `groundOverspeedDecel` (52), so a ground dash is mostly a burst. Dashing just before a jump (or in the air) keeps the speed much longer.

## FOV punch (`SpeedFOV.cs`)

- The speed-based FOV (`speedFov`) now lives in its own field instead of being read back from the camera, so the punch can sit on top without fighting the easing.
- On `Dashed`: the punch target becomes `dashFovBoost` (25 degrees). It opens at `dashFovRiseSpeed` (800 deg/s, about 0.03 s), then fades back to 0 at `dashFovFadeSpeed` (120 deg/s, about 0.2 s). The camera shows `speedFov + dashPunch`.
- With charges, dashes can be chained faster than the punch fades. A new dash just sets the target back to `dashFovBoost`, so punches restart rather than stack.

## Math

- At 80 % and a 90 degree gap between velocity and look: the velocity turns 72 degrees.
- Dashing exactly backwards: `SignedAngle` = 180, so 80 % is a 144 degree turn. There is no zero-length vector, so there is no stall.
- Sprinting at 30 m/s: new speed = max(30 + 5, 35) = 35. Walking at 12 m/s: max(17, 20) = 20. From a standstill while sprinting: 35.

## Reasoning

- **Rotate by a fraction of the angle, not a lerp of velocities.** Blending two velocity vectors shrinks the speed when they point different ways (halfway between opposite directions is zero). Rotating keeps the length, so the slider only affects direction, never speed.
- **Added to current speed.** "Reward accumulated speed rather than reset it": faster players dash faster. The minimums keep it useful from a walk.
- **Before `Jump`.** Dash + jump on the same frame gives the jump the dash's speed. A ground jump leaves the ground with `airSpeedCap` already raised.
- **Press is always consumed.** Same as jump: no secret buffer.
- **Event instead of a reference.** The controller does not know about cameras; anything that wants to react subscribes to `Dashed`.

## Future

- Optional short burst of extra air control right after a dash (not implemented).
