# Script refactor: setting everything back up in Unity

This guide covers **Phase 3 and the music player**, the parts that need work in the Inspector.
Phases 1, 2, 4 and 5 changed code only, so there is nothing to set up for them (section 11 lists what they did).

Nothing here touches `CustomPlayerController.cs`. The merged scripts only read what it already exposes.

---

## 1. What happened, in plain words

Groups of small scripts that sat on the same object were merged, and all the music moved into one place:

| New script | Replaces | Sits on | Scene objects |
|---|---|---|---|
| `ConseraphWarnings` | `ManicWarning` + `ChaseWarning` | **Conseraph** | TheFactory |
| `ConseraphSound` | `ConseraphAudio` (the toll only) | **Conseraph** | TheFactory, CustomPlay |
| `MusicPlayer` (**new**) | the run music that was inside `RoomChain` + `EnrageMusic` | **RoomChainMAINGAME** | TheFactory |
| `CameraFeel` | `SpeedFOV` + `CameraBob` | **Main Camera** | TheFactory, CustomPlay |
| `PlayerVisuals` | `PlayerAnimation` + `SlideVisuals` + `FirstPersonBody` | **Player** | TheFactory, CustomPlay |
| `PlayerAudio` (same name) | + `WindAudio` | **Player** | TheFactory, CustomPlay |

Unity attaches a script to an object by the script's **GUID** (the long ID in its `.meta` file), not by its name.
For each merge I kept the GUID of one of the old scripts and gave it the new name. So:

- **The component that kept its GUID stays attached** and keeps its saved values. You do nothing, except where this guide says so.
  I renamed some fields so they read clearly in the merged script (for example `Max Volume` became `Toll Max Volume`).
  Each renamed field remembers its old name (`FormerlySerializedAs`), so your saved value moves across by itself. I checked this by script against both scenes: every saved field found a home, and no type changed.
- **The components of the absorbed scripts show as "Missing Script"** in the Inspector, because their script file is gone.
  They do nothing now. You remove them, and set the few things listed below on the surviving component.

**I did not edit your scene files.** Everything below is done by you in the Unity editor.

Settings of the absorbed scripts (numbers, colours, text) were copied into the merged script as its **default values**, matching what your scene had.
That means you don't retype numbers. What a default **cannot** hold is a file (a font, a sound), so those are the things to drag in.
The one exception is the new `MusicPlayer`: its two songs are filled in for you when you add the component.

### Who tells whom (the music and Conseraph)

```
RoomChain ──"start / stop the run music"──────────────▶ MusicPlayer
Countdown timer (started by RoomChain) ──"time's up"──▶ Conseraph goes ENRAGED
Conseraph ──his own clock──────────────────────────────▶ Conseraph goes MANIC (Mania Start Seconds after he enraged)
MusicPlayer ──watches Conseraph.Enraged──▶ plays the enrage song (and loops its end when Mania Mode is on)
```

- **RoomChain** starts the run music when the run starts and stops it in a safe room. It does not know about songs.
- **Conseraph** never looks at the music. He is enraged when the countdown timer says so, and goes manic on his own clock, which starts when he enrages.
- **MusicPlayer** only follows. All the songs and their settings live in it, so a future song goes there too.

---

## 2. Before you start

1. In Unity, click into the window and let it finish compiling (spinner in the bottom right).
   - You should see **no red errors** in the Console. Yellow "missing script" warnings are expected.
   - If there is a red error, stop and send it to me. Don't carry on.
2. Work on **one scene at a time**, and **save with Ctrl+S** after each one.
3. How to remove a "Missing Script" component: select the object, find the component named **"Missing (Mono Script)"** in the Inspector, click its three-dot menu (top right of that component), choose **Remove Component**.
4. How to add a component: select the object, click **Add Component** at the bottom of the Inspector, type the name, press Enter.

Your safety net: `Desktop\QuickSprint_Backup_2026-11-21\` has a full copy of the project from before the refactor, plus `phase_snapshots\` with the scripts after each phase.

---

## 3. TheFactory

Open `Assets/Scenes/TheFactory.unity`.

### Object: `RoomChainMAINGAME`

| Component | What to do |
|---|---|
| **Music Player** (new) | **Add Component > Music Player.** Both songs and all the numbers fill in by themselves (see the table in section 6). Check that **Run Music** says `Orchestralcore (320kbps)` and **Enrage Song** says `V1SC3RA _ wonderland.jar OST _ ENTRAILS THEME`. |
| **Room Chain** | Its old music slots (Start Music, Music Volume, Music Fade In/Out Seconds) are gone: they are in Music Player now. There is one new slot, **Music Player** under **Music**. Leave it empty: it finds the Music Player on this object by itself. |
| **Chase Warning** | Shows *Missing Script*. **Remove Component.** (It now lives on Conseraph.) |
| Tutorial Prompt, Void Watch | Nothing. |

### Object: `Conseraph`

| Component | What to do |
|---|---|
| **Conseraph** | Stays. The red screen is now built up by **parrying** (see section 6): **Red Follows Song / Red Grows Over Time / Red Full Seconds are gone**, replaced by **Red Start Percent** (10) and **Red Per Parry Percent** (10), which are already set. Mania Mode and Mania Start Seconds (yours: 158) are unchanged: they count from the moment he becomes enraged, not from the song. |
| **Conseraph Warnings** (was *Manic Warning*) | Stays. Its old values moved over: everything now reads **Manic ...** (Manic Message, Manic Font, Manic Font Size, and so on).<br>**Do:** under **Chase: text**, drag **`Assets/UI/fonts/DarkMidnightRegularDemo-rvMpK SDF.asset`** into **Chase Font**.<br>The rest of the chase settings are already filled in to match your old Chase Warning (see section 6). |
| **Conseraph Sound** (was *Conseraph Audio*) | Stays, toll values kept (**Toll ...**). It is only the toll now. **There is no song slot any more: nothing to drag.** |
| **Enrage Music** | Shows *Missing Script* (or is already gone). **Remove Component.** |

### Object: `Main Camera`

| Component | What to do |
|---|---|
| **Camera Feel** (was *Speed FOV*) | Stays. Its Player slot and FOV values moved over. The two speed fields are now **Fov Start Speed** and **Fov Full Speed**.<br>The **Bob** section already holds your old Camera Bob values, so nothing to type. |
| **Camera Bob** | Shows *Missing Script*. **Remove Component.** |

### Object: `Player`

| Component | What to do |
|---|---|
| **Player Visuals** (was *Player Animation*) | Stays. Animator, Controller and the running-cycle values moved over.<br>**Do:** under **Head (first person)**, **untick Hide Head**. TheFactory had it off, and the new default is on.<br>Leave **Camera Pivot** and **Model** empty: it finds `CameraPivot` and the stick figure by itself when the game starts. **Head Bone Name** is already `Neck`. |
| **Player Audio** | Stays, all values kept.<br>**Do:** under **Wind**, drag **`Assets/UI/music/wind-sound-fx.mp3`** into **Wind Clip**. The other wind values are already filled in to match your old Wind Audio. |
| **Slide Visuals** | *Missing Script*. **Remove Component.** |
| **First Person Body** | *Missing Script*. **Remove Component.** |
| **Wind Audio** | *Missing Script*. **Remove Component.** |
| Custom Player Controller, Player Look, Burn Overlay, Player Burn, Player Death, Run Stats | Nothing. |

Save the scene (Ctrl+S).

---

## 4. CustomPlay (the lobby)

Your saved `CustomPlay.unity` currently has no Player, Main Camera or Conseraph: you removed them on purpose, to copy them over from another scene.
So there is nothing to do here until they are back. When they are, section 10 says what to expect and clean up.

If you add the three objects back (by hand or by copying), these are the final settings in the lobby:

| Object | Component | Setting |
|---|---|---|
| `Conseraph` | **Conseraph Sound** | the toll only. Toll Hearing Range was 150 here. There is no song and no music player in the lobby. |
| `Main Camera` | **Camera Feel** | defaults are right |
| `Player` | **Player Visuals** | **leave Hide Head ticked** (the lobby had it on) |
| `Player` | **Player Audio** | leave **Wind Clip** empty: the lobby has no wind |

---

## 5. Checklist: did it work?

Press Play in **TheFactory** (and in **CustomPlay** once its player is back), and check:

- **Console:** no red errors, and no yellow warnings that mention `ConseraphWarnings`, `ConseraphSound`, `MusicPlayer`, `CameraFeel`, `PlayerVisuals` or `PlayerAudio`. (A warning from `RoomChain` saying it can't find a MusicPlayer means the Music Player component is missing. Warnings from Unity about *Missing Script* mean a component was missed.)
- **Music:** the run music fades in when you leave the first safe room, and fades out when you enter the next one. The enrage song starts when the timer runs out and Conseraph enrages. (Debug mode on RoomChain keeps the run music quiet, as before.)
- **Conseraph:** the toll gets louder as he gets close; "here it comes..." types out when the run starts; "DIE!!!!!" and the manic speed-up arrive 158 seconds after he enrages, whatever the music is doing.
- **Camera:** the view widens as you speed up and punches on a dash; the camera bobs a little while running; it drops when you slide or crouch.
- **Body:** the stick figure animates, tilts back when you slide, and in TheFactory your head shows the way you had it before (Hide Head off).
- **Sound:** footsteps, jump/dash sounds, the wall-run/slide loops, and the wind when you go fast.

If a value looks off, compare with section 6.

---

## 6. Reference: where every old setting went

### Music Player (new: on RoomChainMAINGAME)

The run music came out of RoomChain and the enrage song out of the old Enrage Music. All fields below are pre-filled to match what you had:

| Old place | Old name | New field in Music Player | Value |
|---|---|---|---|
| RoomChain | Start Music | **Run Music** | `Orchestralcore (320kbps).mp3` |
| RoomChain | Music Volume | Run Volume | 0.578 |
| RoomChain | Music Fade In Seconds | Run Fade In Seconds | 10 |
| RoomChain | Music Fade Out Seconds | Run Fade Out Seconds | 2 |
| Enrage Music | Enrage Music (the clip) | **Enrage Song** | `V1SC3RA _ wonderland.jar OST _ ENTRAILS THEME.mp3` |
| Enrage Music | Volume | Enrage Volume | 0.7 |
| Enrage Music | Loop Crossfade Seconds | Enrage Loop Crossfade Seconds | 0.1 |
| Enrage Music | Final Loop Seconds | Enrage Final Loop Seconds | 10 |
| Enrage Music | Fade In Seconds | Enrage Fade In Seconds | 1 |
| Enrage Music | Fade Out Seconds | Enrage Fade Out Seconds | 0 |

The two clips are filled in by the script's `.meta` file (the same trick that fills in the fonts), but only when you **add** the component. If a slot is empty, drag the file in yourself.

### Conseraph

The red screen no longer depends on time or on the song. It depends on parries:

| Setting | Value | What it does |
|---|---|---|
| **Red Start Percent** | 10 | how red the screen is the moment he becomes enraged, in percent of full red |
| **Red Per Parry Percent** | 10 | how much redder it gets every time he is parried |
| Red Fade Seconds | (yours) | how long the screen takes to turn red or clear: each parry's step climbs over this time |
| Rage Intensity | (yours: 1) | the most red it can ever reach (1 = full) |

So he enrages at 10%, the first parry makes it 20%, the second 30%, and so on, up to 100% after 9 parries. The parries add up for the whole run (like his speed step does), so if he enrages again after a safe room the red starts higher.
The old **Red Follows Song** tick, **Red Grows Over Time** and **Red Full Seconds** are gone. Their saved values are simply ignored.

Mania counts real seconds since he became enraged (his own clock, which parries don't reset), not the length of time the song has played. Mania no longer needs a song to exist.

The song's loop (the last **Enrage Final Loop Seconds** of the clip) and **Mania Start Seconds** are now two separate settings. Keep them close if you want the loop to start when he goes manic: the loop starts at *song length minus Final Loop Seconds*, and he goes manic at *Mania Start Seconds* after he enrages.

### Conseraph Warnings (surviving component: was Manic Warning)

| Old name | New name |
|---|---|
| Message | Manic Message |
| Font | Manic Font |
| Font Size | Manic Font Size |
| Text Color / Outline Color / Outline Thickness | Manic Text Color / Manic Outline Color / Manic Outline Thickness |
| Fade In Seconds / Clear Seconds | Manic Fade In Seconds / Manic Clear Seconds |
| Warning Sound / Max Volume / Loop Sound | Manic Sound / Manic Max Volume / Manic Loop Sound |
| Jitter Pixels / Jitter Changes Per Second | Manic Jitter Pixels / Manic Jitter Changes Per Second |

The old **Chase Warning** values, now the defaults of the **Chase ...** fields (nothing to type; only the font needs dragging):

| Old Chase Warning | New field | Value |
|---|---|---|
| Room Chain | Room Chain | found automatically |
| Message | Chase Message | `here it comes...` |
| Font | Chase Font | **drag in** `DarkMidnightRegularDemo-rvMpK SDF.asset` |
| Font Size | Chase Font Size | 70 |
| Text Color | Chase Text Color | red (1, 0.04, 0.04) |
| Outline Color / Thickness | Chase Outline Color / Thickness | black / 0.2 |
| Letter Spacing | Chase Letter Spacing | 40 |
| Characters Per Second | Chase Characters Per Second | 3 |
| Hold Seconds | Chase Hold Seconds | 2.5 |
| Fade Out Seconds | Chase Fade Out Seconds | 1.5 |
| Jitter Pixels / Changes Per Second | Chase Jitter Pixels / Changes Per Second | 6 / 16 |

### Conseraph Sound (surviving component: was Conseraph Audio)

| Old name | New name |
|---|---|
| Toll Clip | Toll Clip (same) |
| Max Volume / Min Volume | Toll Max Volume / Toll Min Volume |
| Hearing Range | Toll Hearing Range |
| Enraged Hearing Range / Manic Hearing Range | Toll Enraged Hearing Range / Toll Manic Hearing Range |

New sliders (defaults match how loud he already was): **Toll Enraged Max Volume** and **Toll Manic Max Volume** (his volume on top of you in each mode, instead of Toll Max Volume). **Toll Enraged Hearing Range** (0.1 to 5000 m) and **Toll Manic Hearing Range** (0.1 to 20000 m) are sliders now. Normal Toll Max Volume / Toll Hearing Range still apply when he is neither enraged nor manic.

### Camera Feel (surviving component: was Speed FOV)

| Old name | New name |
|---|---|
| Start Speed / Full Speed | Fov Start Speed / Fov Full Speed |
| Max Fov Boost, Fov Change Speed, Dash Fov Boost, Dash Fov Rise Speed, Dash Fov Fade Speed | same |

Old **Camera Bob** values, now defaults: Amount 0.04 -> **Bob Amount**, Stride Length 3.5 -> **Bob Stride Length**, Full Bob Speed 20 -> **Bob Full Speed**, Blend Speed 8 -> **Bob Blend Speed**.
(Keep **Bob Stride Length** equal to **Stride Length** on Player Audio, so the bob lands on the footsteps.)

### Player Visuals (surviving component: was Player Animation)

All of Player Animation's fields keep their names. The absorbed scripts' values are now defaults:

| Old | New field | Value |
|---|---|---|
| Slide Visuals: Slide Camera Height / Crouch Camera Height / Slide Camera Speed | same names | 0.7 / 1 / 8 |
| Slide Visuals: Slide Tilt Angle / Slide Model Pivot Height / Tilt Speed | same names | 75 / 1.1 / 540 |
| Slide Visuals: Camera Pivot, Model | Camera Pivot, Model | found automatically |
| First Person Body: Head Bone Name | Head Bone Name | `Neck` |
| First Person Body: Hide Head | Hide Head | **TheFactory: untick. CustomPlay: leave ticked** |

### Player Audio

Old **Wind Audio** values, now defaults of the **Wind ...** fields: Wind Clip (**drag in** `wind-sound-fx.mp3`), Wind Start Speed 30, Wind Full Volume Speed 70 (was Full Volume Speed), Wind Max Volume 0.8 (was Max Volume), Wind Crossfade Seconds 8.5 (was Crossfade Seconds), Wind Volume Smoothing 6 (was Volume Smoothing).

---

## 7. Adding music later

Everything about music is in `Assets/Scripts/Music/MusicPlayer.cs`. To add a song:
1. Give it a clip and its settings in a new section at the top of the file (so they show up in the Inspector).
2. If something in the game starts and stops it (like RoomChain does for the run music), add a `Play...` / `Stop...` method and call it from there. For a song that just starts and stops, use `AudioKit.FadedSong`, like the run music.
3. If it follows a game state (like the enrage song follows Conseraph), read that state in `Update`.

---

## 8. Things worth knowing

- **Unity's crash-recovery scenes** in `Assets/_Recovery/` (`0 (1).unity` ... `0 (7).unity`) are old automatic backups, not part of the build. They still use the old scripts, so they will show Missing Scripts if you ever open one. I left them alone. They are safe to delete if you don't need them.
- **Conseraph no longer adds `ManicWarning` to itself at startup.** `ConseraphWarnings` has to be on the Conseraph object in the Inspector, as it already is in TheFactory. The lobby doesn't need it.
- **Missing Script warnings are harmless until you remove them**, but remove them so the Console stays clean.
- **Both songs can play at once.** The run music is not stopped when Conseraph enrages (that is how it already worked). If you want it to fade out when the enrage song starts, that is now a small change in `MusicPlayer`.

## 10. Copying objects from an older scene (for example "Factory Relax")

I can't see that scene (it isn't in this project folder), so this is what to expect if it was made **before** the refactor. In Unity, pasting objects between scenes keeps their components, but:

| Old component on the copied object | What you will see | What to do |
|---|---|---|
| Camera Bob, Slide Visuals, First Person Body, Wind Audio, Enrage Music, Chase Warning | **Missing Script** (their scripts were merged away) | **Remove Component** |
| Speed FOV | now **Camera Feel**, values carried over | nothing |
| Player Animation | now **Player Visuals**, values carried over | check **Hide Head** (see section 3 / 4) |
| Conseraph Audio | now **Conseraph Sound**, toll values carried over | nothing |
| Manic Warning | now **Conseraph Warnings**, values carried over | drag the **Chase Font** (section 3) |
| a RoomChain with the old music slots | the old slots are ignored | add **Music Player** (section 3) |
| Conseraph's old red-screen settings (Red Follows Song / Red Grows Over Time / Red Full Seconds) | ignored: the red now follows parries | nothing |

Things that were merged into another script get that script's **defaults** for the new fields: a copied Player will not carry the old Slide Visuals or First Person Body numbers, so check Player Visuals against section 6.
After pasting, look at the Console: a **Missing Script** warning names the object that still has one.

---

## 11. Where things live now (what the other phases did)

Nothing to set up for these, they are here so you know where to look:

| You want to change... | Look in |
|---|---|
| any song, or add a new one | `Scripts/Music/MusicPlayer.cs` |
| the text on a death screen, or add a death | `Scripts/Player Scripts/DeathMessages.cs` (the words) and `PlayerDeath.cs` (`PickConseraphDeath`: which death it is) |
| killing the player from anything new (a hazard) | `playerDeath.Die(DeathMessages.SomeName)`: `Die` is public now, and `DieToVoid` is gone |
| a full-screen message, its shake, or a sound that follows it | `Scripts/Shared/UiKit.cs` and `AudioKit.cs` (shared by all the overlays and loops) |
| Conseraph's own sounds (the toll) | `ConseraphSound.cs` |
| Conseraph's "here it comes..." and "DIE!!!!!" | `ConseraphWarnings.cs` |
| camera FOV and bob | `CameraFeel.cs` |
| body animation, slide tilt, hidden head | `PlayerVisuals.cs` |
| footsteps, jump sounds, wind | `PlayerAudio.cs` |
| the room generator's boxes and overlap test | `Rooms/RoomGeometry.cs` (was `OrientedBox` + `RoomBlueprint`) |
| the two small object interfaces | `Objects/ObjectInterfaces.cs` (namespace is still `IObjects`) |
| the "took the tutorial orb" flag | bottom of `Lobby/TutorialPickup.cs` (class `TutorialChoice`, unchanged) |

Deleted for good: `TestScript`, `SpinningPlatform`, the template Readme/TutorialInfo, and the editor setup scripts (`TestRoomBuilder`, `GrayboxBuilder`, `BuildScenesSetup`, `PlayerFeedbackSetup`). Runtime scripts went from 53 to 45.

---

## 9. If something goes wrong

- A red error in the Console: send it to me.
- A value looks wrong: use the tables in section 6.
- To go back to before this phase, with Unity **closed**: delete the whole `Assets\Scripts` folder, then copy `Desktop\QuickSprint_Backup_2026-11-21\phase_snapshots\after_phase2\Scripts` in its place.
  Replace the folder, don't copy over it: the merged scripts and the old ones share the same GUIDs, and Unity breaks if both are present.
  Don't save the scenes you changed. If you already did, copy `Assets\Scenes` back from `Desktop\QuickSprint_Backup_2026-11-21\Assets\Scenes` as well.
  The backup folder also has a full pre-refactor copy of `Assets`.
