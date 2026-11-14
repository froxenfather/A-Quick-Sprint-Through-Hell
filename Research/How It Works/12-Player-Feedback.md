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

**`PlayerBurn.cs`** (on the Player). The player has **singe levels**, from 0 up to a maximum:

| Variable | Default | Meaning |
|---|---|---|
| `sprintSpeedLostPerTouch` | 1 | Max sprint speed lost per singe level. The number of levels is (the Sprint Speed set in the Inspector - walk speed) / this, so 25 -> 12 m/s is 13 levels |
| `touchCooldown` | 0.4 s | Touches closer than this count once |

- **Each hazard touch** adds one singe level and lowers the max sprint speed by `sprintSpeedLostPerTouch`. `Burn` (0 to 1) is `singe / MaxSinge`, so the screen darkens a little more with every level (before, `burnPerTouch` could jump it straight to full).
- **The maximum is the walk speed.** Once the sprint speed is down to 12 m/s that is the most damage there is: touching lava or an electric wall still hurts (speed cap, bounce, sound: `Burned` still fires) but adds no more singe (no `Singed`, no extra mark).
- **Clearing a room** takes one singe level away: `RoomChain.RoomCleared` (fired when the player goes through a room's Exit, not counting the starter room) -> `PlayerBurn.OnRoomCleared`. At 0 singe it does nothing. The last level cleared fires `Healed`, otherwise `Soothed`.
- **`Heal()`** (the green orb) sets singe to 0 and fires `Healed`.
- The sprint speed is always worked out from the singe level (`SetSprintSpeed(start - singe * lost)`, clamped between the walk speed and the starting sprint speed), so healing a level gives back exactly what that level took. `GroundMove` already read the field, so nothing else changed.
- Knock-on: wall running needs 20 m/s, so with a low sprint speed you need a slide, a dash or a slope to get there.
- Events: `Burned` (every counted touch, used for the sounds), `Singed` (a level was added), `Soothed` (a level was removed, some left), `Healed` (all gone).

**`BurnOverlay.cs`** builds its own screen-space canvas at runtime and draws its textures from Perlin noise (no image files). It darkens with the burn level in three ways: a black layer over the whole screen (`screenDarkness`, 0.5 at full burn), darker edges (`edgeDarkness`), and burn marks. Each new singe level (`Singed`) adds a black ragged mark at a random spot, and the marks grow as the burn level rises. A level healed by a room (`Soothed`) removes the oldest mark. When all singe is gone (`Healed`) it fades out over `healFadeTime` and clears the marks. At most `maxMarks` marks; the oldest go first.

**`HealthPickup.cs`:** a trigger collider (the controller's sweeps ignore triggers, so it is walked through). `OnTriggerEnter` finds a `PlayerBurn` on the player, calls `Heal()`, plays the pickup sound at its position, then is destroyed (or hidden for `respawnTime` seconds). It spins and bobs. Make one with **Create Health Pickup Prefab** and drag it into a room.

## Reasoning

- **Events, not references.** The controller knows nothing about audio, animation or UI; adding another reaction (a landing sound, camera shake) is one more subscriber.
- **Own animator controller.** The pack's had states only; editing a third-party asset is worse than adding ours.
- **No scene editing by hand.** The overlay creates itself, and the setup menu does the rest, the same pattern as `TestRoomBuilder`.
- **Sprint floor at walk speed.** Otherwise enough touches would make sprinting slower than walking.

## Wind (`WindAudio.cs`)
Wind that gets louder with speed. Silent below `windStartSpeed` (30 m/s), full volume (`maxVolume`) at `fullVolumeSpeed` (70 m/s), in a straight line between: `volume = maxVolume * InverseLerp(30, 70, speed)`, smoothed by `volumeSmoothing`. Uses the player's whole speed (`Velocity.magnitude`), so falling down the chute is loud.

**Seamless loop for any clip:** two AudioSources play the same clip. In the last `crossfadeSeconds` (2 s, at most half the clip) of the playing one, the other starts at the clip's beginning and they cross-fade with an equal-power curve (`cos`/`sin`, so the loudness doesn't dip). When the first finishes the roles swap. Clips that don't loop cleanly on their own still sound continuous, as long as the clip is longer than a few seconds. Start position is random.

**Setup:** add `WindAudio` to the Player (or re-run **Set Up Player Feedback**, which adds it) and drag a wind clip into **Wind Clip**. On the clip's import settings tick **Preload Audio Data** and leave **Load Type** on Decompress On Load, so it doesn't hitch the first time it plays.
