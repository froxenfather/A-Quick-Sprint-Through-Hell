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
| `RunStarted` | the player leaves a safe room (or the chute, when there is no tutorial) | `CountdownTimerHUD` (starts the countdown from the full time), `Conseraph` (placed `startDistanceBelow` = 1500 m below the player, speed reset), `RunStats` (starts counting, only the first time; its "Time survived" only runs while `RunActive`) |
| `SafeRoomEntered` | the player goes into a safe room | `CountdownTimerHUD` (stops, says UR GOOD). The RoomChain itself also fades the song out |
| `RoomCleared` | the player leaves any room except the chute | `PlayerBurn` (heals one level) |
| `RoomEntered(Room)` | the player goes into any room (the one they are now in) | `TutorialPrompt` (shows that room's message) |

`RoomChain.StartRun()` also restarts the song (`PlayStartMusic`: one AudioSource, `Stop()` then `Play()` with the fade-in, so it never stacks). Debug mode keeps the song quiet. `PauseRun()` fades the song out (`musicFadeOutSeconds`, 2 s) and then stops it: `StopMusic()` starts the fade from the current volume, and `UpdateMusicFade()` lowers it in a straight line each frame, then calls `Stop()`.

Conseraph follows the timer, so a safe room pauses him with no extra code: `Hunting` goes false, so he fades out, stops and silences the toll, and an enrage (and the red screen) ends because `IsCounting` goes false. Going into a safe room while enraged is the "safe room" case the Enraged section was written for.

## How the rooms are passed
Each time the player goes through a room's Exit, `RoomChain.OnRoomPassed(left, entered)` runs. `entered` is the next room, which is always built ahead already, so the player "entering" a room is the same moment as leaving the one before it (the two gates are 1 m apart).
- **Left the chute:** `StarterExited`. If there is nothing to wait for (no tutorial rooms, and with the tutorial off no safe room either) it also calls `StartRun()`, which is how the game worked before the tutorial. With the tutorial **off** and a Safe Rooms list, the first room after the chute is a guaranteed safe room instead (see [13-Room-Generation](13-Room-Generation.md)), and the run starts when the player leaves it.
- **Left any other room:** `RoomsPassed++` only if the run is active and the room isn't a safe room, then `RoomCleared`.
- **Left a safe room:** `StartRun()`.
- **Entered a safe room:** `PauseRun()`.

So the Rooms counter starts at 0 for the real run, and tutorial rooms heal burns but don't count.

## Tutorial rooms
The tutorial is on if **Tutorial Enabled** is ticked on the RoomChain, or if the player took the tutorial orb in the lobby just before this factory load (see [15-Lobby-And-Portal](15-Lobby-And-Portal.md)). The orb counts for one load only.

`tutorialRooms` is built one at a time by `SpawnNext` before any random room, through `RoomPlanner.PlanFixed` (never mirrored, committed as forced, so backtracking never removes them). They are streamed like every other room (3 built ahead, the one behind kept, older ones deleted). Warnings in the console: an empty slot, a tutorial room that overlaps the rooms before it, and a tutorial whose last room isn't a safe room.

## Room messages (`TutorialPrompt`)
Every `Room` has a **Tutorial Message** text box (`Room.TutorialMessage`). When the player goes into a room (`RoomChain.RoomEntered`), `TutorialPrompt` shows that room's message on the screen:
- A room with a message: the text is set and **types out letter by letter** at `charactersPerSecond` (30), using TextMesh Pro's `maxVisibleCharacters`. The whole text is laid out first, so the lines don't jump while it types. Entering another room with a message restarts the typing from the first letter. It stays for as long as the player is in that room.
- A room with an empty message: the old message fades out over `fadeOutSeconds` (0.4). Nothing shows if there was none.
- The chute never shows a message, because the player starts inside it rather than going into it. Any other room can have one, tutorial or random.
- It builds its own overlay canvas at runtime (1920 x 1080 reference, sorting order 50, below the death sequence's canvas), so **Set Up HUD** can't wipe it and nothing else needs setting up. The text is white with a black outline, `heightOnScreen` (0.25) up the screen, wrapping at `width` (1400). Put your own font in **Font**. Placement is read when the game starts.

To use it: add **Tutorial Prompt** to any object in TheFactory (for example the RoomChain object), then give each room prefab either a **Tutorial UI** prefab or a **Tutorial Message**.

### Tutorial UI prefabs (full customizing)
`Room.TutorialUI` takes a UI prefab you design yourself, with text, images, key icons, animations and any layout. When the player goes into the room, `TutorialPrompt` makes it on its overlay canvas, and it goes away when they leave: it fades out with the rest if the next room has nothing, and is replaced straight away if the next room has its own. If a room has a Tutorial UI, the Tutorial Message text is not used. The prefab fades in from nothing over `fadeInSeconds` (1) on `TutorialPrompt`; a typed message doesn't fade, it types.

To make one:
1. In the Hierarchy, right-click > **UI** > **Panel** (Unity adds a Canvas). Set the Game view to 1920 x 1080 and design it: add Text (TextMeshPro), Images, anything.
2. Drag the **Panel** (the thing inside the Canvas) from the Hierarchy into the Project window to make a prefab, then delete it from the scene. Its anchors and position decide where it sits on the screen. Dragging the whole Canvas also works: it is stretched over the screen.
3. Drag the prefab into the **Tutorial UI** slot on the room prefab's Room component.

Animations: an Animator or Animation on the prefab starts playing when it appears, because the prefab is made fresh each time the room is entered. For typed-out text, add the **Typewriter Text** component (`TypewriterText.cs`) to any TextMeshPro text in the prefab. Each text has its own **Characters Per Second** and **Start Delay Seconds**, so several texts can type one after another.

## Chase warning (`ChaseWarning`)
Every time the run starts (`RoomChain.RunStarted`: the first safe room and every later one), bright red letters type themselves out in the middle of the screen: `here he comes...`, in the Swamp Witch font.
- **Typing:** `charactersPerSecond` (5, slow on purpose), using TextMesh Pro's `maxVisibleCharacters`. The whole text is laid out first so the letters don't shift. `letterSpacing` (30) spreads the letters out, so it reads like "h e r e  h e  c o m e s".
- **Jitter, like the timer's:** while it types, the text jumps to a random spot up to `jitterPixels` (6) from its place, `jitterChangesPerSecond` (16, the same rate as the timer's clock) times a second, using `Random.insideUnitCircle * jitterPixels`. The first jump is immediate, and when the typing ends it snaps back to its place and stays still, like the timer resting. `jitterPixels` is read every frame, so you can drag it in Play mode (0 turns the jitter off).
- **After typing:** it stays for `holdSeconds` (2.5), then fades out over `fadeOutSeconds` (1.5). Another run start in the middle restarts it from the first letter.
- **Look:** red (`textColor`), a thin black outline, size 130, `message` editable. It builds its own overlay canvas (sorting order 60, above the room messages and below the death sequence).
- **Font:** `ChaseWarning.cs.meta` has the Swamp Witch SDF set as the script's default **Font**. If the Font slot is empty on your component, drag `Assets/UI/fonts/Swamp Witch SDF` into it.

To use it: add **Chase Warning** to any object in TheFactory (for example the RoomChain object).

## Setting it up
1. Build the tutorial rooms as normal room prefabs (one Entry gate, one Exit gate) and give each a Footprint (Room component, three dots, **Create Footprint From Renderers**), so the random rooms after them don't overlap.
2. Tick **Safe Room** on the last one's Room component.
3. Put them in order in the RoomChain's **Tutorial Rooms** list.
4. Leave the list empty to play without the tutorial: the run then starts when the player leaves the chute.
