# Conseraph (picture, chase, toll)

Conseraph is four 2D sprites (halo, ring, face, mouth) in the 3D world. They always look at the player, and they are drawn over every wall. The ring, face and mouth pop between poses, and the halo stays still. He chases the player while the countdown timer runs, tolls a bell that gets louder as the player gets closer, and has a trigger that detects touching the player. The kill itself is not built yet.

**Files:**
- `Assets/Scripts/Conseraph/ConseraphVisuals.cs`: facing and the pose animation.
- `Assets/Scripts/Conseraph/Conseraph.cs`: the chase AI, the fade, and touching the player.
- `Assets/Scripts/Conseraph/ConseraphAudio.cs`: the toll sound.
- `Assets/Shaders/ConseraphSprite.shader`: draws him over everything.
- `Assets/Editor/ConseraphSetup.cs`: the setup menu.
- The art and sound go in `Assets/Conseraph/`.

**Set up:**
1. Save four transparent PNGs as `Assets/Conseraph/halo.png`, `ring.png`, `face.png` and `mouth.png`, and the sound as `toll` (`toll.wav`, `toll.ogg` or `toll.mp3`).
2. Run **A Quick Sprint Through Hell > Set Up Conseraph** with the scene open, then save the scene (Ctrl+S).
3. Move the `Conseraph` object where you want him. The first time he is put 8 m in front of the player.

The menu imports the PNGs as sprites and builds a `Conseraph` root with `Halo`, `Ring`, `Face` and `Mouth` children (sorting order -1, 0, 1, 2, so the halo is at the very back and the mouth is on top). It gives all four the `conseraph_sprite` material, adds a trigger sphere and the `Conseraph`, `ConseraphAudio` and `ConseraphVisuals` scripts, and fills in the toll sound. All four pictures get the same pixels per unit, set so the face is 2 m tall, so they keep the proportions of the art. Scale the root to change his size.

**Running the menu again updates him in place.** It keeps his position, size, hand-placed parts and every number you tuned. It sets the material, and only adds the scripts, parts, pictures or sound that are missing. To start again from nothing, delete the `Conseraph` object first.

## Drawn over every wall
`ConseraphSprite.shader` (`Custom/Conseraph Sprite`) turns the depth test off (`ZTest Always`), so the depth buffer never hides him behind a wall, floor or object. It also uses the highest render queue (Overlay, 4000), so he is drawn after every other see-through object too. All four parts share the one material, and each sprite renderer gives it its own picture, so the order between the parts still comes from their sorting orders.

What can still draw over him: the HUD and the Burn Overlay (both Screen Space - Overlay canvases). What can still hide him: nothing in the 3D world, except the camera's far plane (1000 m).

## AI (`Conseraph.cs`)
**Ai Enabled** is the switch.
- **Off:** he stays where he is, keeps facing the player, and is always visible. The toll still plays.
- **On:** he is bound to the countdown timer (`CountdownTimerHUD.IsCounting`). While the timer counts down he is visible and hunting. While it says "UR GOOD" (before the player leaves the chute, and later in safe rooms) he fades out to invisible and stops. He is still there, just invisible. The fade takes `fadeSeconds` (1). If a scene has no timer, he hunts from the start (with a warning).

He flies in a straight line at the middle of the player's capsule, through walls. The speed depends on how far away the player is:

| distance to the player | speed |
|---|---|
| more than `catchUpDistance` (30 m) | grows exponentially: `speed = min(maxCatchUpSpeed, max(speed, normalSpeed) * exp(catchUpGrowth * dt))` |
| `closeDistance` (10 m) to 30 m | `normalSpeed` (10 m/s) |
| less than `closeDistance` (10 m) | `closeSpeed` (5 m/s) |

- Beyond 30 m the speed is acceleration based, not instant. If the player gains a burst of speed they leave him behind, and he visibly speeds back up until he is inside 30 m again. `catchUpGrowth` (1 per second) is how fast it grows, and `maxCatchUpSpeed` (150) is the cap.
- Inside 30 m the speed moves toward its target: up at `acceleration` (20 m/s per second), down at `slowdownRate` (1000 m/s per second), which is nearly instant. Raise it to make it instant.
- Everything uses `deltaTime`, so the framerate doesn't matter.
- While he is resting the speed is 0, and when he hunts again he picks up from there.
- **Current Speed** and **Distance To Player** in the Inspector are read-only readouts, to watch while tuning in Play mode.

## Touching the player
The root has a trigger sphere (radius 1) and **no Rigidbody on purpose**. A trigger only produces events against a Rigidbody, so he reacts to the player (who has a kinematic Rigidbody) and cannot press pressure plates, which react to any collider. All the player controller's physics queries ignore triggers, so he never blocks or shoves the player.

`OnTriggerEnter` raises `Conseraph.TouchedPlayer` when the player enters it, but only while he is hunting (visible), so a resting Conseraph is safe. It also logs "Conseraph touched the player." as a placeholder, to be replaced by the kill. `Conseraph` also exposes `Hunting`, `Visibility`, `DistanceToPlayer` and `Speed` for that code.

## Toll (`ConseraphAudio.cs`)
- A looping 3D `AudioSource` on the root, so the sound comes from him and pans toward him. Unity's own distance fade is switched off with a flat rolloff curve, and the doppler effect is off, so the volume is entirely ours and his speed doesn't bend the pitch.
- `volume = rangeVolume * Visibility`. `rangeVolume` is 0 at or beyond `hearingRange` (80 m). Inside it, it rises in a straight line from `minVolume` (0.05, where the player first begins to hear him) to `maxVolume` (1, right on top of the player).
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
