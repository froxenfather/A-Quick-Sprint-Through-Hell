# Water Update (stretch goal, idea only)

**Status:** not started. Parked on purpose: it touches the core controller and the core game comes first. Do this as a "water update" later, once the endless run and the movement polish are solid.

Nothing here is built. This is the design as it was worked out, so it can be picked up without starting from scratch.

## The idea

Water as a solid space you can place or outline in a level. The character can swim through it. Swimming should feel like flying through the water toward where you look, and keep momentum in and out so you can dolphin jump out of it.

## Making the water

- **Volume:** a `WaterVolume` component on an object with a **trigger collider**. The controller's collision sweeps ignore triggers (`QueryTriggerInteraction.Ignore`), so the player passes straight through.
- **Shape:** a box handles a pool. A convex mesh also works. A concave outline can't be one trigger, so build it from several boxes.
- **Look:** a transparent blue water material on the same object, plus a blue screen tint when the camera is under the surface (same idea as the `BurnOverlay`: its own canvas, no scene editing).
- **Placing it:** a menu item (in the style of `PlayerFeedbackSetup`) that creates a water prefab to drag in and scale.
- **Finding the water:** a static list of active `WaterVolume`s (added in `OnEnable`, removed in `OnDisable`, cleared on `SubsystemRegistration` because Enter Play Mode can skip the domain reload). Containment is a point test with the collider's `ClosestPoint`, so no physics queries each step.

## Swim mode

Only when **fully submerged**: both ends of the capsule are inside water.

- **Forward:** moves you toward where the mouse is looking, in all three axes (the full 3D look direction, pitch included).
- **Backward:** moves you away from where the mouse is looking.
- **Left and right:** strafe across the horizontal axis as normal.
- **Sprint:** moves faster, as normal.
- **Dash:** a large, substantial underwater boost along the 3D look direction, much bigger than the ground dash. Starting values to tune: about +20 m/s with a 45 m/s minimum.
- **Disabled underwater:** wall jump, wall run, slide and crouch.
- **Gravity:** off. With no input the player drifts to a stop through water drag.
- **Jump:** does nothing underwater, since up and down is steered with the mouse.
- **Partly in the water:** feet or legs only means normal movement. Swimming starts only once fully under.

### Momentum in and out

- **Entering:** velocity is kept. It bleeds toward the swim speed gradually through water drag, so a fall into water plunges and then slows.
- **Exiting:** velocity is kept, including the vertical part, so swimming upward out of the surface launches you (the dolphin jump). On exit, raise `airSpeedCap` to the current flat speed, the same way the dash and wall jump do, so air control doesn't clamp it. The existing `maxAirSpeed` bleed (`airOverspeedDecel`) still brings very fast exits down gradually.

## How it would fit the controller (notes for later)

These are from reading `CustomPlayerController.cs`. Check them again when starting, since the code will have moved on.

- Add a `swimming` flag worked out near the top of `FixedUpdate`, after `Depenetrate`. While it is true: skip `UpdateSlideAndCrouch`, the jump, `UpdateWallRun` and the wall jump, and use a `SwimMove` in place of `Move`.
- On entering water, clear `sliding` and `crouching` so the capsule goes back to standing height.
- Keep the player not grounded while swimming (`ClearGroundState`), so touching the bottom doesn't snap to ground.
- `TryDash` gets an underwater branch (3D direction, bigger boost, its own minimum).
- `MoveAndSlide` still runs, so walls and the pool floor still collide.
- Hazard surfaces (`SpeedCapSurface`) should still work underwater, and lava next to water is a good level idea.

## Extras that would fit

- **Animation:** the stickman pack already has a `Swimming.anim`. Add a `Swimming` bool to `Player_Animator.controller` and a Swim state, fed by a new `IsSwimming` property.
- **Sounds:** a splash on entering and leaving, and a soft underwater loop (muffle the other sounds while submerged).
- **Visuals:** underwater fog or tint, surface ripples, and bubbles.
- **Model tilt:** tilt the body with the look pitch while swimming, like the slide tilt does.

## Decisions to revisit when we get here

- Starting speeds: swim speed, swim sprint speed, dash boost, minimum dash speed, water drag.
- Should jump swim up, or stay disabled?
- Should the hazards also apply while swimming?
- Does the player float up slowly at rest, or hold position?
