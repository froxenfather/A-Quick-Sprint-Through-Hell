# Chisel PR: fix models disappearing after entering Play mode

Target repo: https://github.com/RadicalCSG/com.chisel (branch `main`).

## PR title

Fix Chisel models vanishing after entering Play mode (generator pools lost in ResetState)

## PR description (paste this)

### Problem

On Unity 6000.6.0f1, a Chisel model loses its render mesh and collider after pressing Play, and stays gone after pressing Stop until the scene is reopened. With the default Enter Play Mode settings (Reload Domain and Scene) the Console shows this on every Play:

```
InvalidOperationException: Collection was modified; enumeration operation may not execute.
System.Collections.Generic.HashSet`1+Enumerator[T].MoveNext ()
Chisel.Core.GeneratorJobPoolManager.ResetState () (at .../Core/1.Input/GeneratorBase/GeneratorJobPool.cs:62)

ObjectDisposedException: The UNKNOWN_OBJECT_TYPE has been deallocated, it is not allowed to access it
Unity.Collections.NativeHashMap`2[TKey,TValue].Clear ()
Chisel.Components.BrushVisibilityLookup.Clear () (at .../Components/Managers/BrushVisibilityLookup.cs)
Chisel.Components.ChiselUnityVisibilityManager.ResetState () (at .../Components/Managers/ChiselUnityVisibilityManager.cs:34)

Brush with ID (Chisel.Core.CompactNodeID) has its brushMeshID set to (0), which is invalid.
The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly.
```

### Repro

1. Unity 6000.6.0f1, URP 17.6.0, Entities 1.4.3, Collections 2.6.3, Chisel `main`.
2. Default Enter Play Mode settings (Reload Domain and Scene).
3. New scene, add a Chisel Model with a single Box brush. It renders and has a collider.
4. Press Play. The Console shows the errors above and the box is no longer rendered or solid.
5. Press Stop. The box is still gone until the scene is reopened.

### Cause

1. `GeneratorJobPoolManager.ResetState` enumerates `generatorPools` while `pool.Dispose()` calls `Unregister(this)`, which removes the pool from that same set. That throws on the next `MoveNext`. It is also wrong beyond the exception: the pool is disposed and re-allocated but never registered again, so `ScheduleJobs` never runs it. Brushes of that generator type (for example the Box) never get a mesh, which produces `brushMeshID is invalid` and the model is dropped. (`GeneratorJobPoolManager.Dispose()` already iterates over `ToArray()` for the same reason.)
2. `ChiselUnityVisibilityManager.OnBeforeAssemblyReload` replaces `s_BrushVisibilityLookup` with `new()`, whose `NativeHashMap`s are not created. `ResetState` then calls `BrushVisibilityLookup.Clear()` on them, which throws `ObjectDisposedException` with safety checks on.

### Fix

- `GeneratorJobPoolManager.ResetState`: iterate over a copy (`ToArray()`) and call `Register(pool)` again after `Dispose()`.
- `BrushVisibilityLookup.Clear`: only clear a map if `IsCreated`.

### Testing

Tested with the two changes on Unity 6000.6.0f1 (applied on top of commit `1a40428`, where these methods are identical to current `main`). The model now stays visible and solid in Play mode and after exiting it. [fill in: number of Play/Stop cycles tried]

Not addressed here: the "Leak Detected" warnings (TempJob / Persistent allocations) that Unity prints at the same time. They look like a separate native-memory disposal issue (there is a `TODO: properly dispose native memory` in `ChiselUnityVisibilityManager`).

## The exact changes

`Core/1.Input/GeneratorBase/GeneratorJobPool.cs` (applies to `main` as-is, around line 62):

```diff
-            foreach (var pool in s_Instance.generatorPools)
+            foreach (var pool in s_Instance.generatorPools.ToArray()) // Dispose() unregisters the pool, so loop over a copy
             {
                 pool.Dispose();
+                Register(pool); // Dispose() unregistered it; without this the pool never runs again and its brushes get no mesh
                 pool.AllocateOrClear();
             }
```

`Components/Managers/BrushVisibilityLookup.cs`, the `Clear()` method (around line 43 on `main`; the first two lines of its body):

```diff
         internal void Clear()
 		{
-			compactNodeIDVisibilityStateLookup.Clear();
-			entityIDVisibilityStateLookup.Clear();
+			if (compactNodeIDVisibilityStateLookup.IsCreated) compactNodeIDVisibilityStateLookup.Clear(); // may be uncreated after a domain reload
+			if (entityIDVisibilityStateLookup.IsCreated) entityIDVisibilityStateLookup.Clear();
 			notAllVisibleCount = 0;
 			Version++;
 		}
```

Note: `GeneratorJobPool.cs` already has `using System.Linq;`, so `ToArray()` needs no new import. Both files use CRLF line endings; keep them.

## Steps (run by you, on your GitHub account)

1. Fork https://github.com/RadicalCSG/com.chisel on GitHub (Fork button, top right).
2. Clone your fork into a SHORT path (no submodules needed for this change): `git clone https://github.com/<your-username>/com.chisel.git C:/dev/com.chisel`
3. `cd C:/dev/com.chisel` and `git checkout -b fix-playmode-reset-state`
4. Make the two edits above (use an editor that keeps CRLF, such as Rider or VS Code).
5. `git commit -am "Fix generator pools being lost on Play-mode reset"` then `git push -u origin fix-playmode-reset-state`
6. On GitHub, open "Compare & pull request" on your fork, use the title and description above, target `RadicalCSG/com.chisel` `main`.
