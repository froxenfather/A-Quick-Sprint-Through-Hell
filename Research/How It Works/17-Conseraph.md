# Conseraph (picture, chase, toll)

Conseraph is four 2D sprites (halo, ring, face, mouth) in the 3D world. They always look at the player, and they are drawn over every wall. The ring, face and mouth pop between poses, and the halo stays still. He chases the player while the countdown timer runs, tolls a bell that gets louder as the player gets closer, and has a trigger that detects touching the player. Touching kills the player, unless he is enraged and the player parried him (see Parry below).

**Files:**
- `Assets/Scripts/Conseraph/ConseraphVisuals.cs`: facing and the pose animation.
- `Assets/Scripts/Conseraph/Conseraph.cs`: the chase AI, the fade, and touching the player.
- `Assets/Scripts/Conseraph/ConseraphAudio.cs`: the toll sound.
- `Assets/Scripts/Conseraph/ParryImpact.cs`: the freeze frame, flash and hit sound when a parry lands.
- `Assets/Scripts/Conseraph/EnrageMusic.cs`: the song that plays while he is enraged.
- `Assets/Shaders/ConseraphSprite.shader`: draws him over everything.
- The art and sound go in `Assets/Conseraph/`.

**Set up (by hand, there is no menu any more):** he is already in TheFactory and CustomPlay, so this is only for making another one.
1. Make an empty `Conseraph` object. Add the `Conseraph` component (it adds a SphereCollider by itself and makes it a trigger when the game starts, so set the radius you want, see Touching the player), then `ConseraphAudio` (drag in the toll sound) and `ConseraphVisuals`.
2. Give him four children, `Halo`, `Ring`, `Face` and `Mouth`, each a Sprite Renderer with its transparent PNG imported as a sprite. Sorting orders -1, 0, 1 and 2, so the halo is at the very back and the mouth is on top. Use the same pixels per unit for all four so they keep the proportions of the art.
3. Make a material from the `Custom/Conseraph Sprite` shader and put it on all four renderers, so he draws over every wall.
4. Drag the ring, face and mouth into the slots on `ConseraphVisuals` (the halo stays still, so it has none).
5. Scale the root to change his size. Move him where you want. At the start of a run he is put below the player anyway.

## Drawn over every wall
`ConseraphSprite.shader` (`Custom/Conseraph Sprite`) turns the depth test off (`ZTest Always`), so the depth buffer never hides him behind a wall, floor or object. It also uses the highest render queue (Overlay, 4000), so he is drawn after every other see-through object too. All four parts share the one material, and each sprite renderer gives it its own picture, so the order between the parts still comes from their sorting orders.

What can still draw over him: the HUD and the Burn Overlay (both Screen Space - Overlay canvases). What can still hide him: nothing in the 3D world, except the camera's far plane (1000 m).

## AI (`Conseraph.cs`)
**Ai Enabled** is the switch.
- **Off:** he stays where he is, keeps facing the player, and is always visible. The toll still plays.
- **On:** he is bound to the countdown timer (`CountdownTimerHUD.IsCounting`). While the timer counts down he is visible and hunting. While it says "UR GOOD" (from the start of the game through the tutorial, and inside safe rooms) he fades out to invisible and stops. He is still there, just invisible. The fade takes `fadeSeconds` (1). If a scene has no timer, he hunts from the start (with a warning).
- **When the run starts** (`RoomChain.RunStarted`: the player leaves a safe room) he is put `startDistanceBelow` (1500 m) straight below the player's capsule center, with his speed reset to 0. He fades in and rushes up through the catch-up zone. A scene with no RoomChain is unaffected. See [20-Run-Start-And-Safe-Rooms](20-Run-Start-And-Safe-Rooms.md).

He flies in a straight line at the middle of the player's capsule, through walls. The speed depends on how far away the player is:

| distance to the player | speed |
|---|---|
| more than `catchUpDistance` (150 m) | grows exponentially: `speed = min(maxCatchUpSpeed, max(speed, normalSpeed) * exp(catchUpGrowth * dt))` |
| `closeDistance` (60 m) to 150 m | `normalSpeed` (25 m/s) |
| less than `closeDistance` (60 m) | `closeSpeed` (20 m/s) |

- Beyond 150 m the speed is acceleration based, not instant. If the player gains a burst of speed they leave him behind, and he visibly speeds back up until he is inside 150 m again. `catchUpGrowth` (1 per second) is how fast it grows, and `maxCatchUpSpeed` (80) is the cap.
- Inside 150 m the speed moves toward its target: up at `acceleration` (20 m/s per second), down at `slowdownRate` (100 m/s per second), a quick drop. Raise it to make it instant.
- Everything uses `deltaTime`, so the framerate doesn't matter.
- While he is resting the speed is 0, and when he hunts again he picks up from there.
- **Current Speed** and **Distance To Player** in the Inspector are read-only readouts, to watch while tuning in Play mode.

## Enraged (`Conseraph.cs`, Enraged section)
When the countdown reaches 00:00 he becomes enraged: `enraged = enragedEnabled && aiEnabled && hasTimer && timer.IsFinished && timer.IsCounting`. It is worked out every frame.
- His speed drops to `enragedStartSpeed` (1), then `speed = enragedStartSpeed + floor(elapsed / enragedStepSeconds) * enragedSpeedStep`: +1 every 0.2 s, with **no cap**. The distance zones (catch-up, normal speed, close speed) are ignored while he is enraged, since they would fight the climb.
- He still moves with `Vector3.MoveTowards`, which stops exactly on the player, so he can't skip past them however fast he gets, and the touch trigger always overlaps.
- The screen goes red (see below), and the timer's text stops showing the time: it flashes `RUN!!!` for 2 s, then shows his distance from the player like `07.52 METERS` (see [14-HUD](14-HUD.md)).
- It ends when the timer says "UR GOOD" (`IsCounting` goes false, which is what a safe room will do later): his speed goes back to `normalSpeed` and the usual distance speeds take over wherever he is. While the timer isn't counting he is also invisible and still, as before.
- **Enraged Enabled** off means 00:00 does nothing at all: no speed change, no red, no timer text.
- `Conseraph.Enraged` is public for other scripts to read.
- **Music:** `EnrageMusic` (add it to the Conseraph object, drag the song into **Enrage Music**) reads `Enraged` every frame. The song starts from its very beginning the moment he becomes enraged (the timer hits 00:00) and fades in over `fadeInSeconds` (1, 0 starts it at full volume). When the enrage ends it fades out over `fadeOutSeconds` (1) and stops, so the next enrage starts the song from the beginning again. It loops like the wind (`WindAudio.cs`): two copies of the clip, and in the last `loopCrossfadeSeconds` (2, cut to half the clip's length) of one the other starts at its beginning while the first fades out (equal power). That crossfade only happens at the loop, the start of the song has none. It is 2D (not placed in the world), and the fades use real time, so the freeze frame of a parry doesn't stall them. Leave **Enrage Music** empty for no music. The clip needs to be longer than a few seconds.

## Red screen
While he is enraged, `Conseraph` fades a global shader number `_RageAmount` from 0 to 1 over `redFadeSeconds` (1), and back when the enrage ends. The number sent is `rage * rageIntensity`, where **Rage Intensity** (0 to 1, default 1) is a slider that sets how red the screen gets at the most: lower keeps part of the real picture showing through the red. A full-screen shader reads it and paints the whole view bright red. See [19-Rage-Screen](19-Rage-Screen.md) for the shader and the two things to set up by hand. The number is reset to 0 when Conseraph is disabled or destroyed, so the lobby is never red after a death.

## Touching the player
The root has a trigger sphere (radius 10, which is about 7.9 m in the world because the root is scaled 0.79) and **no Rigidbody on purpose**. So he counts as touching the player when they are within about 8 m of him, not only on contact. A trigger only produces events against a Rigidbody, so he reacts to the player (who has a kinematic Rigidbody) and cannot press pressure plates, which react to any collider. All the player controller's physics queries ignore triggers, so he never blocks or shoves the player.

`OnTriggerEnter` and `OnTriggerStay` both go to one `HandleTouch`, which raises `Conseraph.TouchedPlayer` when the player is inside the sphere, but only while he is hunting (visible), so a resting Conseraph is safe. `Stay` is there because a parry (and the flight after it) can make him harmless while the player is still inside the sphere, and no new `Enter` comes when that ends. `PlayerDeath` listens to the event and sends the player back to the lobby (see [18-Death-And-Stats](18-Death-And-Stats.md)). `Conseraph` also exposes `Hunting`, `Visibility`, `DistanceToPlayer` and `Speed` for other code to read.

## Parry (`Conseraph.cs`, Parry section)
While he is **enraged, and only then**, a dash aimed at him makes the player immune to him for a moment, and if he touches the player in that moment he is parried instead of killing them. This is what the death screen's enraged tip describes.

**Arming.** When the player dashes (`CustomPlayerController.Dashed`) while he is enraged, three checks are made, once, at that moment:
1. **Going fast enough:** the player's flat speed just before the dash is at least `parryMinSpeed` (26 m/s). It is `CustomPlayerController.DashStartSpeed`, the flat speed the dash started from (the same kind of number as the speedometer: flat, relative to the platform), read before the dash adds its own boost. Reading it after would be no test, because a sprint dash always ends at 35 m/s or more. Standing still is 0, so it fails. 26 passes, below 26 fails.
2. **Looking at him:** the angle between the camera's forward and the direction from the camera to him is at most `lookToleranceDegrees` (20), so he is near the middle of the screen.
3. **Dashing at him:** the sideways angle between the dash (`CustomPlayerController.DashDirection`, the flat direction the dash used) and the flat direction from the player to him is at most `dashToleranceDegrees` (25). If he is less than 0.5 m away sideways (straight above or below), there is no sideways direction, so this passes. With **Dash Direction Influence** at 100 the dash goes exactly where the player looks, so this mostly catches him being steeply above or below.

All three true: the player is immune for `DashHangTime + parryGraceSeconds`, which is the dash's no-gravity glide (0.25 s) plus 0.5 s, so 0.75 s. A dash that fails any check gives nothing. The time counts down in `Update` (the event fires in `FixedUpdate`, so the two clocks aren't mixed).

**The parry.** If he touches the player (`HandleTouch`) while that time is left and he is enraged, nothing kills the player. Instead:
- **Throw:** he is thrown the opposite way to his current velocity. He has no velocity vector, so `MoveToward` remembers the direction he last moved (toward the player), and the throw is that reversed. His speed at that moment is the throw speed.
- **Flight:** for `knockbackSeconds` (1) his speed falls in a straight line from the throw speed to 0 (`speed = throwSpeed * (1 - t)`), so he always stops exactly at the end, however fast he was. He travels `throwSpeed * knockbackSeconds / 2`, so the faster he was, the further he goes. `throwSpeed` is his speed times `knockbackMultiplier` (1), so 5 throws him five times as fast and five times as far, in the same 1 s. The multiplier only changes him: the player's throw below still uses his real speed. He doesn't chase while he flies.
- **The player is thrown too:** the same moment, the player is thrown exactly the way he was moving (`lastMoveDirection`, in full 3D, so up or down as well as sideways), at the **same speed he had**: so the faster he was, the harder both of them go. It is one call, `player.SetVelocity(lastMoveDirection * currentSpeed)` (the call launch pads use), and it replaces the player's whole velocity, including any rising or falling and their dash. Coming up at the player from below, that throws them upward. Air control can't take the speed away (it only limits adding speed), but sideways speed above `maxAirSpeed` bleeds off at `airOverspeedDecel`, and gravity pulls them down as normal, so a very hard throw fades over a few seconds.
- **Harmless while flying:** he can't kill during the flight, so a player can't die to a Conseraph they have just parried.
- **After:** his speed is 0 and the enraged climb starts over (`enragedElapsed` is reset), so he climbs again from `enragedStartSpeed` (1), +1 every 0.2 s.
- **Sound:** `parrySound` plays once, flat from the speakers (not from him), at `parryVolume`, when he starts to fly back. That is the moment the freeze frame (below) ends, or the moment the parry works if there is no `ParryImpact`. Empty = no sound. Drag the clip into **Parry Sound** on the Conseraph component. (The sound at the instant of the parry is a different one, on `ParryImpact`.)

**Cancelled when** the enrage ends (the timer says UR GOOD), when he stops hunting, or when the run starts again. A parry doesn't carry into a safe room.

**Fields:** `parryEnabled` (true), `parryMinSpeed` (26, 0 lets any dash parry), `lookToleranceDegrees` (20), `dashToleranceDegrees` (25), `parryGraceSeconds` (0.5), `knockbackSeconds` (1), `knockbackMultiplier` (1), `parrySound`, `parryVolume` (1). The camera is found at the start (a Camera under the player, else `Camera.main`); with none, the parry switches itself off with a warning.

**Testing:** turn on the RoomChain's **Debug Mode** (a 5 s timer), let it reach 00:00 so he is enraged (the screen is red), get above 26 m/s (watch the speedometer), look at him and dash straight at him as he closes in. Watch **Current Speed** and **Distance To Player** on his component: the speed jumps to what it was, falls to 0 over 1 s, and the distance climbs. A dash that isn't at him, one made below 26 m/s, or one made more than 0.75 s before he arrives, still kills. Before 00:00 a dash at him does nothing.

## Freeze frame (`ParryImpact.cs`)
The hit-stop when a parry lands. **Add Component > Parry Impact** on the Conseraph object (it needs the `Conseraph` component, and listens to its `Parried` event, raised by `Parry()`). Without it the parry still works, just with no freeze, flash or hit sound.

The order of a parry:
1. **The parry lands:** the hit sound (`hitSound`) plays, the screen flashes, and time stops. `Conseraph.Parry()` has already set everything going (his flight, and the player's throw with `SetVelocity`), they simply wait.
2. **Freeze:** `Time.timeScale` is 0 for `freezeSeconds` (0.15), counted in real time (`Time.unscaledDeltaTime`), so it is the same length at any frame rate. The flash holds while time is stopped.
3. **Time runs again:** `timeScale` goes back to what it was. The player's throw and his flight begin, his own `parrySound` plays, and the flash fades out over `flashFadeSeconds` (0.25).

What stops and what doesn't while `timeScale` is 0:
- **Stops:** the player controller (it moves in `FixedUpdate`, which doesn't run), Conseraph, the countdown timer, the dash glide and every grace time (all use `Time.time` or `deltaTime`), and animations.
- **Keeps going:** mouse look (it reads the raw mouse movement, so the player can still turn their head), all audio (the toll, the music), and the scripts that already use real time (the chase warning, the tutorial prompt, the speed number's smoothing).
- **Button presses** during the freeze are queued by the controller (`jumpQueued`, `dashQueued`), so a dash or jump pressed then happens as soon as time runs again.

How it is kept safe:
- **`timeScale` is always put back:** when the freeze ends, when the component is disabled, and when the object is destroyed (a scene change, a death), through `OnDisable`. It restores the speed it had before, never a blind 1.
- **Conseraph waits for real:** the frame the freeze starts in still has its old, nonzero `deltaTime`, so `Knockback` checks `Time.timeScale` itself and does nothing while it is 0. His `parrySound` waits for the first step that really moves him (`parrySoundPending`).
- **The freeze countdown skips the frame the parry landed in**, since that frame had already run, so the freeze is the full length.
- **Nothing divides by `deltaTime`** in a script that runs while frozen (the only division is the platform carry in the controller, which runs in `FixedUpdate`).

The flash is a full-screen `Image` on its own Screen Space - Overlay canvas, sorting order 110 (above the HUD, the burn overlay at 100 and the messages, below only the death sequence), so it draws over the red rage screen. It is its own object (not a child of Conseraph) and is destroyed with him.

**Fields:** `freezeSeconds` (0.15, 0 turns the freeze off), `hitSound`, `hitVolume` (1), `flashColor` (white), `flashStrength` (0.85, 1 is a solid screen), `flashFadeSeconds` (0.25).

**Testing:** parry him as above. You should hear the hit sound, see the flash, and feel time stop for a moment, then he flies away with his sound and you are thrown. Check afterwards that the game runs at normal speed, and that the timer keeps counting.

## Toll (`ConseraphAudio.cs`)
- A looping 3D `AudioSource` on the root, so the sound comes from him and pans toward him. Unity's own distance fade is switched off with a flat rolloff curve, and the doppler effect is off, so the volume is entirely ours and his speed doesn't bend the pitch.
- `volume = rangeVolume * Visibility`. `rangeVolume` is 0 at or beyond `hearingRange` (120 m). Inside it, it rises in a straight line from `minVolume` (0.05, where the player first begins to hear him) to `maxVolume` (1, right on top of the player).
- Multiplying by `Visibility` makes the toll fade out and in with him.
- A WAV or OGG loops cleanly. An MP3 usually has a tiny gap at the loop point.

## Facing
- Every `LateUpdate` the root turns to look at the middle of the player's capsule: `player.transform.TransformPoint(capsule.center)`. That follows the player smoothly and still tracks the shorter capsule while sliding.
- `Quaternion.LookRotation(root.position - target, Vector3.up)`. A sprite is seen from its -Z side, so forward points away from the player. World up stays up, so he never rolls and a tilt is always measured from true vertical.
- It is the same at any distance. If the player can't be found, a warning is logged and he doesn't turn.

## Halo
A plain sprite child of the root, behind everything else, with no script and no animation. It turns and moves with the rest of Conseraph because it is a child of the root, and that is all it does. `ConseraphVisuals` doesn't know about it.

## Mirror and tilt
Each part has a `flip` of 1 or -1 and a tilt in degrees:
- `localScale = (baseScale.x * flip * size, baseScale.y * size, baseScale.z)`
- `localRotation = Euler(0, 0, flip * tilt)`

A flip mirrors the part over its own vertical axis (left to right). The tilt is negated with the mirror, so a mirrored part leans the opposite way, like a real reflection. A plain negative X scale would not do that. Positive tilt leans counter-clockwise as the player sees it. Each part keeps the scale it has in the scene, and the pose is applied on top.

Every pose is a snap. Nothing turns continuously.

## Ring
`ringFlipsPerSecond` (3) times a second it flips and picks a new random tilt between 0 and `ringMaxTilt` (10) degrees.

## Mouth
`mouthChangesPerSecond` (2) times a second, all at once: it flips, picks a new random tilt between 0 and `mouthMaxTilt` (10), and picks a new random size between `mouthMinSize` (0.9) and `mouthMaxSize` (1.1).

## Face
One loop every `faceLoopSeconds` (1), in four equal poses of a quarter second each:

| time | pose |
|---|---|
| 0.00 - 0.25 | tilted `faceTilt` (10°), size 100% (the start) |
| 0.25 - 0.50 | 10% bigger (`faceSizeGrowth`) |
| 0.50 - 0.75 | flipped (tilt -10°), still 110% |
| 0.75 - 1.00 | back to 100%, still flipped |
| 1.00 | flips back to the start |

The pose number is `floor(time / faceLoopSeconds * 4) % 4`.

## Timing
Ring and mouth work out `step = floor(time * rate)` and act whenever the step number changes, so there is no drift and the framerate doesn't matter. Time counts from the moment the component is enabled, and the three parts each keep their own step. All the numbers are read every frame, so you can drag them in the Inspector during Play mode and see the change straight away.
