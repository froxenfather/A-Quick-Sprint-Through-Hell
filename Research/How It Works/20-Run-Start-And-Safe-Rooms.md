# Run start and safe rooms

The game starts calm: the clock says UR GOOD and Conseraph is invisible and still. The player runs a hand-built tutorial to a safe room. **Leaving a safe room starts the run**: the song plays, the countdown starts, and Conseraph hunts from 1000 m below. Going into any safe room pauses all that again.

**Files:** `RoomChain.cs`, `Room.cs`, `RoomPlanner.cs` (the rooms); `CountdownTimerHUD.cs`, `Conseraph.cs`, `RunStats.cs` (what listens).

## The flow
1. Start: chute, then the **Tutorial Rooms** in order, the last one a safe room, then random rooms. The timer says UR GOOD (`started` is false), and Conseraph is invisible (he follows `timer.IsCounting`).
2. The player leaves the first safe room: `RoomChain.StartRun()`.
3. They reach a later safe room (a room with **Safe Room** ticked): `RoomChain.PauseRun()`. They leave it: `StartRun()` again.

There is no game mode switch yet. A mode chooser is planned for the lobby, and this version is meant to become the TimeTrial mode, so the run simply always starts when the first safe room is left.

## Events (all on `RoomChain`)
| event | when | who listens |
|---|---|---|
| `StarterExited` | the player leaves the chute, the first time | nobody yet |
| `RunStarted` | the player leaves a safe room (or the chute, when there is no tutorial) | `CountdownTimerHUD` (starts the countdown from the full time), `Conseraph` (placed `startDistanceBelow` = 1000 m below the player, speed reset), `RunStats` (starts counting, only the first time; its "Time survived" only runs while `RunActive`) |
| `SafeRoomEntered` | the player goes into a safe room | `CountdownTimerHUD` (stops, says UR GOOD). The RoomChain itself also fades the song out |
| `RoomCleared` | the player leaves any room except the chute | `PlayerBurn` (heals one level) |

`RoomChain.StartRun()` also restarts the song (`PlayStartMusic`: one AudioSource, `Stop()` then `Play()` with the fade-in, so it never stacks). Debug mode keeps the song quiet. `PauseRun()` fades the song out (`musicFadeOutSeconds`, 2 s) and then stops it: `StopMusic()` starts the fade from the current volume, and `UpdateMusicFade()` lowers it in a straight line each frame, then calls `Stop()`.

Conseraph follows the timer, so a safe room pauses him with no extra code: `Hunting` goes false, so he fades out, stops and silences the toll, and an enrage (and the red screen) ends because `IsCounting` goes false. Going into a safe room while enraged is the "safe room" case the Enraged section was written for.

## How the rooms are passed
Each time the player goes through a room's Exit, `RoomChain.OnRoomPassed(left, entered)` runs. `entered` is the next room, which is always built ahead already, so the player "entering" a room is the same moment as leaving the one before it (the two gates are 1 m apart).
- **Left the chute:** `StarterExited`. With no tutorial rooms it also calls `StartRun()`, which is how the game worked before the tutorial.
- **Left any other room:** `RoomsPassed++` only if the run is active and the room isn't a safe room, then `RoomCleared`.
- **Left a safe room:** `StartRun()`.
- **Entered a safe room:** `PauseRun()`.

So the Rooms counter starts at 0 for the real run, and tutorial rooms heal burns but don't count.

## Tutorial rooms
`tutorialRooms` is built one at a time by `SpawnNext` before any random room, through `RoomPlanner.PlanFixed` (never mirrored, committed as forced, so backtracking never removes them). They are streamed like every other room (3 built ahead, the one behind kept, older ones deleted). Warnings in the console: an empty slot, a tutorial room that overlaps the rooms before it, and a tutorial whose last room isn't a safe room.

## Setting it up
1. Build the tutorial rooms as normal room prefabs (one Entry gate, one Exit gate) and give each a Footprint (Room component, three dots, **Create Footprint From Renderers**), so the random rooms after them don't overlap.
2. Tick **Safe Room** on the last one's Room component.
3. Put them in order in the RoomChain's **Tutorial Rooms** list.
4. Leave the list empty to play without the tutorial: the run then starts when the player leaves the chute.
