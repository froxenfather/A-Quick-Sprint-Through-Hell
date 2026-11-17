# HUD (rooms, speed, dash meter)

On-screen UI built with Unity UI (Canvas) and TextMeshPro. Each piece is a small script that only reads the game, so the HUD can be deleted without breaking anything.

**Files:** `Assets/Scripts/UI/SpeedHUD.cs`, `DashMeterHUD.cs`, `RoomCounterHUD.cs`; `Assets/Editor/HUDSetup.cs` (the setup menu); `Assets/UI/rounded_rect.png`, `speed_label.png`.

**Set up:** menu **A Quick Sprint Through Hell > Set Up HUD** with the Player scene open. It replaces any object named `HUD`, so it is safe to run again. Save the scene afterwards.

**Layout:** rooms at the top left, the countdown timer at the top centre, and speed (above) and the dash meter (below) at the bottom left, anchored to the bottom-left corner. The heartbeat sits at the bottom centre (see Heartbeat below).

## Heartbeat (`HeartbeatHUD`)
A beating heart at the bottom centre. It is **not built by Set Up HUD**: you make it by hand under the HUD canvas (steps below). The script goes on a `RawImage` whose texture is `Assets/UI/heart_sheet_even.png` (840 x 332: three equal cells of 280 x 332, each heart centred on its black outline at its own size). It was made from `heart_sheet.png`, whose hearts were different sizes and not on equal thirds, so cutting that sheet into thirds sliced them. Keep both files: the original is the source. It shows one third of the sheet at a time by moving the `RawImage`'s `uvRect`, so the sheet needs no slicing and no sprite import. Re-running Set Up HUD deletes the HUD object, and the heart with it, so make it again afterwards.

To make it:
1. Right-click the **HUD** object in the Hierarchy and choose **UI (Canvas) > Raw Image**. Name it `Heartbeat`.
2. Rect Transform: **Alt + Shift** click the bottom-centre anchor preset, then set **Pos Y** 40, **Width** 169, **Height** 200 (one cell is 280 x 332, so 200 tall is 169 wide).
3. Drag `heart_sheet_even.png` into **Texture**. Set **UV Rect** to X 0, Y 0, W 0.3333, H 1 so it shows one heart in the editor.
4. **Add Component > Heartbeat HUD** on the same object. Leave the other fields as they are.
- **Normal beat:** a beat is 1 s: the three hearts play in order during `animationSeconds` (0.25), then it rests on the first for `restSeconds` (0.75).
- **Hidden when he isn't hunting:** the heart (the `RawImage`) is switched off whenever `Conseraph.Hunting` is false: from the start of the game through the tutorial, and inside safe rooms. It switches on again, starting from the first heart, when he begins to hunt. With no Conseraph in the scene it always shows.
- **Speeding up:** only while Conseraph is hunting. Between his catch-up distance (`Conseraph.CatchUpDistance`) and his close distance (`Conseraph.CloseDistance`), the speed rises in a straight line from 1x to `maxSpeedMultiplier` (3.5x) as `DistanceToPlayer` falls: `speed = lerp(1, 3.5, InverseLerp(catchUp, close, distance))`. Farther than the catch-up distance, or while he is paused or invisible (safe rooms, the tutorial), it beats normally.
- **No jumps:** the beat is a position that moves forward each frame by `deltaTime * speed / beatLength`, so a change of speed never makes the heart jump.
- **Place and size:** whatever you set on the Raw Image's Rect Transform.

## Rooms (`RoomCounterHUD`)
- Number = `RoomChain.RoomsPassed`, a running total that increases each time the player passes a room's Exit. (`passedCount` can't be used, it drops when old rooms are deleted.)
- Colour: `HSV(0, t, t)` with `t = rooms / fullRedRoom` (100), so black at 0 rooms and bright red at 100 (the room where the rooms are meant to switch to the outline look).
- Font: Anton SDF (TextMesh Pro examples).

## Speed (`SpeedHUD`)
- The "SPEED:" picture plus a number: `player.HorizontalSpeed` rounded to whole m/s.
- Below `growStartSpeed` (30) the number is normal size. Above it the size grows in a straight line to `maxScale` (2x) at `maxScaleSpeed` (100 m/s): `scale = lerp(1, 2, InverseLerp(30, 100, speed))`. Smoothed by `scaleSmoothing`. The number grows from its left edge so it does not cover the picture.

## Dash meter (`DashMeterHUD`)
- One rounded rectangle per `MaxDashCharges`, rebuilt if that number changes.
- Blue = no charge. The charging slot fills left to right in green. A full slot is teal and means a dash is ready.
- Slot `i` shows `clamp01(DashCharges - i)`. See [10-Dash](10-Dash.md) for how charges work.
- The row starts at the object's left edge and grows right.

## Countdown timer (`CountdownTimerHUD`)
A big `mm:ss` countdown at the top centre with a ticking clock on its left. Only built in a scene that has a RoomChain (TheFactory), by the same **Set Up HUD** menu.

- **Starts** (and starts again) each time the run starts: `RoomChain.RunStarted`, which is when the player leaves a safe room, the same moment the music starts. Entering a safe room (`RoomChain.SafeRoomEntered`) stops it and it says UR GOOD again. See [20-Run-Start-And-Safe-Rooms](20-Run-Start-And-Safe-Rooms.md). Counts down from `durationSeconds` (310 = `05:10`), one second at a time, and holds at `00:00`.
- **Clock:** `clock0.png` to `clock7.png` (8 pictures) in `Assets/UI/clock/`. Once per `cycleSeconds` (1 s): during the first `animationSeconds` (0.5 s) it plays pictures 0 to 7 (`frame = floor(phase / animationSeconds * pictureCount)`), then rests on clock0 for the other half. It is a sprite swap, not a video, so it keeps its transparency and stays in step with the seconds.
- **Jitter:** each time the picture changes during the animation, the whole group (clock and time) jumps to a random spot up to `jitterPixels` (6) away from its place. While resting it snaps back.
- **Font:** the first TextMeshPro font asset in `Assets/UI/fonts/` (make one by right-clicking a .ttf > Create > TextMeshPro > Font Asset), else Anton.
- **Time text style:** black digits with a thick red outline (`textColor`, `outlineColor`, `outlineThickness` 0.3). The outline is drawn by the font material, so it is switched on when the game starts (`Awake` uses the text's own material copy), and only shows in Play mode. The font is `Assets/UI/fonts/midnight_spree/MidnightSpree-Regular.ttf` made into a TMP Font Asset.
- **Rooms outline:** the room counter has a white outline (`outlineColor`, `outlineThickness` 0.2), applied at game start the same way as the timer's. It uses the text's own material copy so the speed number is not affected.
- **Tick sound:** `tickSound` (`Assets/UI/tick.mp3`) plays once each time the shown second changes while the countdown shows `tickFromSeconds` (5) or fewer, so at 00:05, 00:04, ... 00:00. Played from a 2D AudioSource the timer makes for itself (`tickVolume`). The factory song no longer loops.
- **Idle message:** while the countdown is not running (from the start of the game through the tutorial, and inside a safe room) the time text shows `idleMessage` (default `UR GOOD`) instead of `05:10`, at the same text size. Safe rooms pause the clock by clearing `started` (`SafeRoomEntered`).
- **`IsCounting`:** a public flag that is true while the countdown is running (the same `started` that picks the time or the idle message). Conseraph follows it: he hunts while it is true and is invisible while it is false. See [17-Conseraph](17-Conseraph.md).
- **Enraged text:** while Conseraph is enraged (`Conseraph.Enraged`, from the moment the countdown reaches 00:00) the text is no longer the time. It blinks `runMessage` (`RUN!!!`) on and off `runFlashesPerSecond` (4) times a second for `runFlashSeconds` (2), then shows his distance from the player as `00.00 METERS` (for example `07.52 METERS`, `123.45 METERS` when far, always with a dot). The order is: enraged text, else the countdown, else the idle message. The tick sound, clock animation and jitter are unchanged. `IsFinished` is public: true once the countdown has run down to 00:00.
- **Debug mode:** the RoomChain's **Debug Mode** shortens the countdown to its **Debug Timer Seconds** (5). The timer reads it once, when the countdown starts, and uses that length for the rest of the run. See [13-Room-Generation](13-Room-Generation.md).
- **Long messages push the clock left:** the time text stays centered in its box and spills out of both sides when the words are long (`UR GOOD`). `PlaceClock` (in `LateUpdate`) moves the clock so it keeps the same gap from the left end of the text as in the scene: `textWidth = max(box width in the scene, timeText.preferredWidth)`, then `clockRight = textLeft - gap`. The time (`05:10`) fits the box, so the clock doesn't move for it.
