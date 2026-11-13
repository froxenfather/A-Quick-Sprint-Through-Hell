# Lobby, portal and the start sequence

The game opens in the **CustomPlay** scene (the lobby). The player slides into the chute entrance, falls onto the **portal**, a video plays full screen while **TheFactory** loads, then the player drops down the factory's chute.

**Files:** `Assets/Scripts/Lobby/Portal.cs`, `GameStartSequence.cs`; `Assets/Editor/BuildScenesSetup.cs`.

## Setup
1. Put the videos in the project (MP4 / H.264) and drag up to five into the Portal's **Intro Videos** list. One is picked at random each time; empty slots are ignored.
2. Add `Portal` to the flat portal object. It needs a Collider with **Is Trigger** ticked (done automatically when the script is first added).
3. Menu **A Quick Sprint Through Hell > Set Up Build Scenes**: Build Settings becomes CustomPlay (index 0) then TheFactory. Without this the portal can't load the factory (it logs an error and does nothing).
4. In TheFactory, the RoomChain's **Starter Room** is `thechute` (see 13-Room-Generation).

Pressing Play in the editor starts the scene that is open, so open CustomPlay to test the lobby. A built game starts at Build Settings index 0.

## What happens
`Portal.OnTriggerEnter` (player detected with `GetComponentInParent<CustomPlayerController>`, once only):
1. Checks the scene can be loaded, then freezes the player (disables `CustomPlayerController` and `PlayerLook`).
2. Creates a `GameStartSequence` object marked `DontDestroyOnLoad` (the portal's own scene is about to unload, so it can't finish the job).

`GameStartSequence`:
1. `AudioListener.pause = true`: every sound stops. Its own `AudioSource` has `ignoreListenerPause`, so the video's audio still plays.
2. `SceneManager.LoadSceneAsync` with `allowSceneActivation = false`: loads behind the video, held at 90 %.
3. The video: a `VideoPlayer` into a `RenderTexture`, shown in a `RawImage` inside an `AspectRatioFitter` (fits the screen, black bars if the shape differs). The overlay appears once the first frame exists, so the lobby doesn't go black early. No skip.
4. The video ends: the screen is solid black. Wait for the load to reach 90 %, allow activation, wait for it to finish, then one more frame so the factory's `Start` methods run (RoomChain builds the chute and puts the player above it).
5. `AudioListener.pause = false`, and the black image fades from 1 to 0 over `fadeInSeconds` (1 s, unscaled time). The player is already falling.
6. The object destroys itself. If it is destroyed early, `OnDestroy` unpauses audio so the game is never left muted.

If the video is empty or fails, the sequence skips straight to black, load, fade.

## Notes
- Each scene has its own player, so nothing carries over. The lobby player is discarded.
- Playing TheFactory directly works as before, without the video or fade.
- The portal must be a trigger. A solid collider would just be landed on.
