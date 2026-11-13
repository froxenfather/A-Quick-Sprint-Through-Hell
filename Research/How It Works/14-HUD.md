# HUD (rooms, speed, dash meter)

On-screen UI built with Unity UI (Canvas) and TextMeshPro. Each piece is a small script that only reads the game, so the HUD can be deleted without breaking anything.

**Files:** `Assets/Scripts/UI/SpeedHUD.cs`, `DashMeterHUD.cs`, `RoomCounterHUD.cs`; `Assets/Editor/HUDSetup.cs` (the setup menu); `Assets/UI/rounded_rect.png`, `speed_label.png`.

**Set up:** menu **A Quick Sprint Through Hell > Set Up HUD** with the Player scene open. It replaces any object named `HUD`, so it is safe to run again. Save the scene afterwards.

**Layout:** top left, from the top: rooms, speed, dash meter. The bottom centre is kept free for the heartbeat (not built yet: waits for "disgrace"; `heart_sheet.png` is a 3-frame sheet for it).

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

- **Starts** the first time the player goes through the starter room's Exit (`RoomChain.StarterExited`, the same moment the music starts). Counts down from `durationSeconds` (310 = `05:10`), one second at a time, and holds at `00:00`.
- **Clock:** `clock0.png` to `clock7.png` (8 pictures) in `Assets/UI/clock/`. Once per `cycleSeconds` (1 s): during the first `animationSeconds` (0.5 s) it plays pictures 0 to 7 (`frame = floor(phase / animationSeconds * pictureCount)`), then rests on clock0 for the other half. It is a sprite swap, not a video, so it keeps its transparency and stays in step with the seconds.
- **Jitter:** each time the picture changes during the animation, the whole group (clock and time) jumps to a random spot up to `jitterPixels` (6) away from its place. While resting it snaps back.
- **Font:** the first TextMeshPro font asset in `Assets/UI/fonts/` (make one by right-clicking a .ttf > Create > TextMeshPro > Font Asset), else Anton.
- **Time text style:** black digits with a thick red outline (`textColor`, `outlineColor`, `outlineThickness` 0.3). The outline is drawn by the font material, so it is switched on when the game starts (`Awake` uses the text's own material copy), and only shows in Play mode. The font is `Assets/UI/fonts/midnight_spree/MidnightSpree-Regular.ttf` made into a TMP Font Asset.
- **Rooms outline:** the room counter has a white outline (`outlineColor`, `outlineThickness` 0.2), applied at game start the same way as the timer's. It uses the text's own material copy so the speed number is not affected.
- **Tick sound:** `tickSound` (`Assets/UI/tick.mp3`) plays once each time the shown second changes while the countdown shows `tickFromSeconds` (5) or fewer, so at 00:05, 00:04, ... 00:00. Played from a 2D AudioSource the timer makes for itself (`tickVolume`). The factory song no longer loops.
