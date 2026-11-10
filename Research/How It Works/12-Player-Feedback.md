# Player Feedback (animations, sounds, burn, pickup)

Everything the player sees and hears from their own movement. None of it changes how the controller moves: each piece listens to events and properties of `CustomPlayerController` (`Jumped`, `WallJumped`, `Dashed`, `HazardTouched`, `IsGrounded`, `IsSliding`, `IsCrouching`, `IsWallRunning`, `IsWallSliding`, `HorizontalSpeed`), so any of them can be turned off without breaking the rest.

**Set up with:** menu **A Quick Sprint Through Hell > Set Up Player Feedback** (adds the components to the Player and Main Camera, builds the animator controller, fills the sound slots). Run it again any time; it only adds what is missing. Then save the scene. The pickup is **Create Health Pickup Prefab** in the same menu.

## Animations (`PlayerAnimation.cs`, `Assets/Animation/Player_Animator.controller`)

The stick figure's pack came with clips but a controller with no transitions, so a new controller is built (the pack's is untouched).

| Parameter | Fed from |
|---|---|
| `Speed` (float) | `HorizontalSpeed`, smoothed |
| `Grounded`, `Sliding`, `Crouching`, `WallRunning` (bool) | the matching `Is...` property |
| `Jump` (trigger) | `Jumped`, `WallJumped` and `Dashed`, so all three play the jump animation |
| `PlaybackSpeed` (float) | 0.9 to 1.4, rising with speed, so quicker running looks quicker |

States: **Locomotion** (blend tree on `Speed`: Idle 0, Walk 6, Run 14, Run Fast 24 m/s), **Air** (Jumping Up), **Crouch** (Sitting), **Slide** (Idle: `SlideVisuals` already tilts the whole body), **WallRun** (Run Fast). Any-state transitions handle slide, wall run and the jump trigger (which may transition to itself, restarting the clip).

- The clips came with **Loop Time off**; the setup turns it on for Idle, Walk, Run, Run Fast and Sitting.
- **Root motion is turned off** at runtime, or the animations would try to move the character.

## First-person body (`FirstPersonBody.cs`, `CameraBob.cs`)

- The camera sits at the head, so the `Head` bone is scaled to zero in `LateUpdate` (after the Animator). Arms, torso and legs show when looking down. Untick **Hide Head** to see it again.
- `CameraBob` bobs the Main Camera (not `CameraPivot`, which `SlideVisuals` moves) once per step: `phase += speed / strideLength * dt * pi`. Two bobs up for each sway sideways. Amount 0 turns it off. Match its stride length to `PlayerAudio` and the bob lands on the footsteps.

## Sounds (`PlayerAudio.cs`, `Assets/Audio`)

| Sound | When |
|---|---|
| Footstep (3 variations) | every `strideLength` (3.5 m) of ground running, flat speed over 1.5 m/s, not sliding |
| Jump, Wall jump, Dash | `Jumped`, `WallJumped`, `Dashed` |
| Lava touch, Electric touch | `PlayerBurn.Burned`, picked by the surface's `HazardType` |
| Wall run loop | while `IsWallRunning`, faded in and out |
| Wall slide loop | while `IsWallSliding` (airborne, touching a wall, falling, not wall running) |
| Slide loop | while `IsSliding` and `IsGrounded` (an air slide is silent). A deliberately soft, smooth glide, quieter than the wall sounds |

- All sources are 2D and live on child objects of the Player. Each one-shot gets a small random pitch change.
- The clips are **generated placeholders** (noise and sine building blocks). Drag better clips into the slots on `PlayerAudio` any time; an empty slot is silent.
- No landing sound yet.

## Burn and the health pickup

**Hazards:** `SpeedCapSurface` now has a **Type** (Lava or Electric). See `11-Hazard-Surfaces.md` for the cap and bounce. `CustomPlayerController` fires `HazardTouched` the first time a surface is touched (even with bounce 0).

**`PlayerBurn.cs`** (on the Player):

| Variable | Default | Meaning |
|---|---|---|
| `burnPerTouch` | 0.1 | Added to the burn level (0 to 1) per touch: ten touches to full |
| `sprintSpeedLostPerTouch` | 1 | Max sprint speed lost per touch, never below the walk speed |
| `touchCooldown` | 0.4 s | Touches closer than this count once |

- `CustomPlayerController` remembers its `sprintSpeed` at Awake. `ReduceSprintSpeed(amount, floor)` lowers it, `ResetSprintSpeed()` puts it back. `GroundMove` already read the field, so nothing else changed.
- Knock-on: wall running needs 20 m/s, so with a low sprint speed you need a slide, a dash or a slope to get there.
- **`Heal()`** sets burn to 0 and resets the sprint speed. It fires `Healed`.

**`BurnOverlay.cs`** builds its own screen-space canvas at runtime and draws its textures from Perlin noise (no image files). Each touch adds a black ragged burn mark at a random spot, marks grow as the burn level rises, and the screen edges darken (`edgeDarkness`). Healing fades it out over `healFadeTime` and clears the marks. At most `maxMarks` marks; the oldest go first.

**`HealthPickup.cs`:** a trigger collider (the controller's sweeps ignore triggers, so it is walked through). `OnTriggerEnter` finds a `PlayerBurn` on the player, calls `Heal()`, plays the pickup sound at its position, then is destroyed (or hidden for `respawnTime` seconds). It spins and bobs. Make one with **Create Health Pickup Prefab** and drag it into a room.

## Reasoning

- **Events, not references.** The controller knows nothing about audio, animation or UI; adding another reaction (a landing sound, camera shake) is one more subscriber.
- **Own animator controller.** The pack's had states only; editing a third-party asset is worse than adding ours.
- **No scene editing by hand.** The overlay creates itself, and the setup menu does the rest, the same pattern as `TestRoomBuilder`.
- **Sprint floor at walk speed.** Otherwise enough touches would make sprinting slower than walking.
