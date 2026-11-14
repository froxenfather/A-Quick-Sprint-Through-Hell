# Replace Materials window

An editor tool that swaps one material for another across the project, so you don't have to open every prefab.

**File:** `Assets/Editor/ReplaceMaterialsWindow.cs`. **Menu:** A Quick Sprint Through Hell > Replace Materials.

## Using it
1. Drag the material to get rid of into **Replace this material**. The button under it fills that box with ProBuilder's own default material, which lives inside a package and is hard to find to drag in.
2. Drag the material you want into **With this material**.
3. Tick **Prefabs**, **Scenes** or both.
4. Press **Replace** and confirm. A progress bar shows while it works, and the Console says how many prefabs and scenes changed.

## What it does
- **Prefabs:** every prefab under `Assets/`. For each one that uses the material, it opens the prefab, swaps the material on every renderer (inactive children too), saves it and closes it.
- **Scenes:** every scene under `Assets/`. It saves the open scene first (it asks), opens each scene that uses the material, swaps it on every renderer, saves, and finally opens the scene that was open before.
- Files that can't use the material are skipped without being opened (`AssetDatabase.GetDependencies`). The built-in materials have no file, so for those every file is opened and it is slower.
- Only the material slots that held the old material change. A renderer with several materials keeps the others.
- When both are ticked, prefabs go first, so scene copies of those prefabs follow automatically, and the scene pass only fixes the leftovers (overrides done in a scene).

## Careful
- It edits and saves your files and **Ctrl+Z can't undo it.** Back up the project (or use version control) first.
- It only looks in `Assets/`, never in packages.
- Rooms spawned by the RoomChain are copies of prefabs, so swapping on the prefabs is what changes them in the game.
