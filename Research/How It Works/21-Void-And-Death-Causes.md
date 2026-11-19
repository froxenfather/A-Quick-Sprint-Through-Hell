# The Void and how the player died

There are two ways to die: Conseraph touching the player, and being lost to the void. Both go to the same stats screen (see [18-Death-And-Stats](18-Death-And-Stats.md)), which now opens with a header saying how the player died.

**Files:** `Assets/Scripts/Player Scripts/VoidWatch.cs`, `PlayerDeath.cs`; `Assets/Scripts/Rooms/Room.cs`, `RoomChain.cs` (the room bounds); `Assets/Scripts/Lobby/GameStartSequence.cs` (the header).

**Set up:**
1. In TheFactory, add **Void Watch** to the RoomChain object (any object works). The font fills itself.
2. On the Player's **Player Death**, drag the void death video into **Void Video** (empty = straight to the stats). The header texts and the Anton font are already filled in.
3. Rooms need a footprint. A room without one is never checked, and the console warns about it once.

## Staying inside the room (`VoidWatch`)
The player is only watched once they have gone into their first room (`RoomChain.RoomEntered`), so the fall down the chute never counts. After that, every frame it asks `RoomChain.IsInsideBounds` whether the middle of the player's capsule is inside the room they are in.
- **Inside:** the timer resets, and any "Let Go" fades away over `clearSeconds` (0.4).
- **Outside:** the timer counts up. For the first `graceSeconds` (0.5) nothing shows. Then "Let Go" fades in over `fadeInSeconds` (5), `alpha = (time - grace) / fade`, in a straight line from 0 to 1. At 1, `PlayerDeath.DieToVoid()` runs.
- **Sound:** put a sound in `consumeSound`. It plays while the words are on the screen and its volume follows their opacity: `volume = alpha * maxVolume`, so it starts silent when "Let Go" starts to appear and is at full volume when the words are full. It fades back down as the words clear, and stops when they are gone. `loopSound` (on) makes it loop, so a short drone works. Untick it only for a one-shot sound at least as long as the fade. It plays through a 2D `AudioSource`, and the death sequence's `AudioListener.pause` silences it when the player dies. Empty = no sound.
- **Jitter, like the timer's:** the words constantly jump to a random spot up to `jitterPixels` (6) from their place, `jitterChangesPerSecond` (16) times a second (`Random.insideUnitCircle * jitterPixels`), for as long as any of the words are visible. They go back to their place when gone. `jitterPixels` is read every frame, so drag it in Play mode to tune it (0 turns it off).
- Getting back inside at any time, even at 99%, clears it and starts over.
- If the player is already dying (Conseraph got them first), it stops.
- The text is purple (`textColor`), in the scary font, size 160, with a black outline, in the middle of the screen on its own overlay canvas (sorting order 70).

## The room's bounds
A footprint is a BoxCollider for each piece of a room (a wall, a floor), so a player standing in the middle of a room is inside none of them. So the bounds are the **one box around all of a room's footprint boxes**, in the room's own space. `RoomChain.SpawnPlanned` works it out just before the footprint object is deleted (`StoreBounds`) and stores it on the `Room` (`SetBounds`). `Room.Contains(point, margin)` turns the world point into the room's space with `InverseTransformPoint`, so rotation and mirroring are handled.
- A pit with a floor below it stays inside the bounds, correctly: it isn't the void. A hole with nothing below it, or a missing wall, leaves the bounds.
- **Blind spot:** the empty outer corner of an L-shaped turn room is inside its bounds, so leaving there takes longer to notice, until the player falls below the box.
- `IsInsideBounds(point, margin)` checks the **previous, current and next** room. Including the neighbours means going through a gate, or back through an open entrance, can never count as leaving. `boundsMargin` (2 m) adds space all round, and covers the 1 m gap between rooms.
- If the current room has no bounds, it says "inside" (it can't be judged).

## Two causes of death (`PlayerDeath`)
`Die(cause)` does the same for both: guard against dying twice, freeze the player, snapshot the stats, load the lobby. Only the video and the header differ.
- **Conseraph:** plays `gotYouVideo`.
- **Void:** plays `voidVideo`.

## The header
At the very top of the stats screen, before the stats (it lands first, with the same thud): a large title and a smaller tip under it.

| cause | title | tip |
|---|---|---|
| Conseraph | red **You succumbed to Conseraph** (the stats font, Midnight Spree) | normal: *Try taking your time to ensure you do not need to backtrack or slow down!* |
| Conseraph, enraged (`Conseraph.Enraged`) | same | *Tip: When enraged, you can parry him by dashing straight at him right before he kills you!* |
| Conseraph, singed (`PlayerBurn.Singe > 0`) | same | *When singed, slide bouncing and dashing is the best way to keep speed!* |
| the void | purple **The Void consumes you.** (Anton) | *Make sure to stay inside the Eternal Factory and avoid holes!* |

If the player was both enraged and singed, the enraged tip wins. All the texts, colours and the two sizes (`titleSize` 110, `tipSize` 42) are fields on `PlayerDeath`. `voidHeaderFont` is filled with Anton in the editor by its GUID if empty. `GameStartSequence.StatsPage` has the optional header fields, so the Portal's own sequence (which passes none) looks as before. With a header, the stats column sits 140 px lower so the two don't overlap.

## Testing
- Sprint through many rooms, including turns and up and down sections: "Let Go" should never show at gates.
- Fall through a floor or out of a window: after half a second it starts fading in, and at full the death sequence plays.
- Climb back in before it is full: it clears.
