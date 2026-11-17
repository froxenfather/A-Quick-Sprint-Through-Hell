# Death and the stats screen

Conseraph touching the player is the only way to die. It sends the player from TheFactory back to the lobby (CustomPlay), plays the "got you" video while the lobby loads, then shows the stats of the run on a black screen.

**Files:** `Assets/Scripts/Player Scripts/PlayerDeath.cs`, `RunStats.cs`; `Assets/Scripts/Lobby/GameStartSequence.cs` (shared with the Portal, see [15-Lobby-And-Portal](15-Lobby-And-Portal.md)); `Assets/Scripts/Conseraph/Conseraph.cs` (the `TouchedPlayer` event, see [17-Conseraph](17-Conseraph.md)).

**Set up (by hand, no menu):**
1. Put the "got you" video in the project (for example `Assets/UI/videos/`). If it crashes Unity like the portal videos did, tick **Transcode** in its import settings or re-encode it.
2. Find a short, low thud sound (WAV).
3. In **TheFactory**, select the Player and **Add Component > Player Death** (it adds `RunStats` by itself). Drag in the video (**Got You Video**), the sound (**Thud Sound**) and the font `MidnightSpree-Regular SDF` (**Stats Font**).
4. Run **A Quick Sprint Through Hell > Set Up Build Scenes** if CustomPlay or TheFactory isn't in Build Settings.

## What happens
1. `Conseraph.TouchedPlayer` fires. It only fires while he is hunting (visible, the timer running, or AI off), so a resting Conseraph is harmless.
2. `PlayerDeath.Die` (once only): checks the lobby scene can be loaded (logs an error and does nothing if not), takes the stats lines from `RunStats`, freezes the player (disables `CustomPlayerController` and `PlayerLook`, like the Portal), and starts a `GameStartSequence` with the stats page.
3. `GameStartSequence`: silences everything, loads CustomPlay behind the screen (held back), and plays the video with its sound. No skip.
4. **Stats page** (only when one is given): on black, each line pops in `lineDelaySeconds` (0.8) after the last, with a thud. The first line also waits, so there is a black beat after the video.
5. After the last line, keys are ignored for 0.5 s, then **any key or mouse click** (left or right) continues. This uses the Input System (`Keyboard.current.anyKey`, `Mouse.current`) because the old `Input` class is turned off in this project.
6. The stats hide, the lobby activates once loaded, and the black fades away over `fadeInSeconds` (1 s).

The thud plays on the sequence's own `AudioSource`, which ignores the listener pause, so it is heard while everything else is silent.

## Stats (`RunStats`)
Counted from the moment the run starts (`RoomChain.RunStarted`, the first time the player leaves a safe room, the same moment as the timer), so the long drop down the chute and the tutorial don't count (the chute would inflate top speed). Later safe rooms don't reset anything, so the stats keep adding up across them. The exception is "Time survived", which only goes up while the player is actively being hunted (`RoomChain.RunActive`), so it stops while they are in a safe room. In a scene with no RoomChain it counts from the start.

| line | where it comes from |
|---|---|
| Rooms cleared | `RoomChain.RoomsPassed` |
| Top speed | highest `HorizontalSpeed` (the HUD's number), whole m/s |
| Jumps | `Jumped` + `WallJumped` events |
| Dashes | `Dashed` events |
| Time survived | seconds spent being hunted (the run active, so not the chute, the tutorial or a safe room), `mm:ss` |
| Distance run | sideways distance moved, m (falling and climbing don't add) |
| Wall jumps | `WallJumped` events |
| Time wall running | seconds with `IsWallRunning` |
| Time sliding | seconds with `IsSliding` |
| Times burned | `PlayerBurn.Burned` events (each hazard touch) |

`RunStats.BuildLines()` returns the list of (label, value) in this order. To add a stat, count it in `RunStats` and add a line there. To remove one, delete its line.

## Fields (`PlayerDeath`)
`gotYouVideo`, `lobbySceneName` ("CustomPlay"), `fadeInSeconds` (1), `statsFont`, `thudSound`, `thudVolume` (1), `lineDelaySeconds` (0.8), `labelColor` (white), `valueColor` (red). An empty video skips straight to the stats. An empty thud sound is silent.

## Testing
- **Quick:** in TheFactory untick **Ai Enabled** on Conseraph and walk into him. He is always hunting with AI off, so the sequence starts at once (the stats will be mostly zeros, since the run hasn't started).
- **Real:** leave the chute, let the timer run, and let him catch you.
