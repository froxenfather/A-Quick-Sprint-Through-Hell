# Room Generation (weights, sections, and the overlap safeguard)

How `RoomChain` decides which room comes next and makes sure it never overlaps another. The stitching itself (Entry onto the previous Exit, mirroring) is unchanged. This page is about the choosing.

**Files:** `RoomChain.cs` (spawning, sealing, deleting, the lists), `RoomPlanner.cs` (the decision, pure geometry), `RoomBlueprint.cs` (what the planner knows about a room prefab), `OrientedBox.cs` (the overlap test), `RoomFootprint.cs` (marks a room's footprint boxes), `Room.cs` (the footprint tools).

## The room lists and weights

Eight lists on the RoomChain, each entry is a room plus a **weight box**:

| List | Used when |
|---|---|
| Forward Rooms, Turn Rooms, Up Transitions, Down Transitions | normal level: the generator picks from all four together |
| Up Rooms, Up Returns | inside an up section |
| Down Rooms, Down Returns | inside a down section |

- **Weight:** 0 to 10, decimals allowed. 1 is normal, 0.5 is half as likely, 4 is four times as likely, 10 is ten times as likely, 0 means never.
- All four normal lists are **one pool**, so a room's chance is its weight divided by the total of every weight in the pool. A list with many rooms in it is picked more often, so tune with weights.
- The old single room list (`roomPrefabs`) is moved into Forward Rooms automatically the first time the RoomChain loads in the editor, each with weight 1.

## Up and down sections

A small state machine in `RoomPlanner`:

1. **Level:** pick from the four normal lists.
2. If the pick was an **Up Transition** (or **Down Transition**), the state becomes Up (or Down) and it rolls a whole number from `sectionRoomsMin` to `sectionRoomsMax` (1 to 3, both included).
3. **Up/Down:** that many rooms come from **Up Rooms** (or **Down Rooms**).
4. Then one room from **Up Returns** (or **Down Returns**) brings the state back to Level.

A transition room is only a candidate if its section has at least one room with a weight above 0 **and** at least one return, so the generator never enters a section it can't leave.

**Authoring up and down rooms:** these are ordinary upright rooms whose gates are on floors and ceilings (arrow pointing up for an up room, down for a down room). The stitching math already handles any gate direction. Use one convention for gate roll on a vertical gate (the gate's up arrow points back toward where you came from) on every vertical gate, so rooms line up without twisting.

## The overlap safeguard

Every room has a **footprint**: the space it fills, as boxes. A new room is only allowed if its boxes do not overlap the boxes of any room that exists.

1. **Footprint.** A child object `Footprint` (with the `RoomFootprint` marker) holding BoxColliders, one per piece of the room, or one big box. Make it in the prefab: open the room, three dots on the **Room** component, **Create Footprint From Renderers** (exact, one box per piece) or **Create Footprint (One Box)** (quick, loose). Then move or resize the boxes with the normal Box Collider handles. They are drawn in orange. At spawn the Footprint object is removed, so these colliders never exist in the game.
2. **Candidates.** For the current state, every room in the lists with a weight above 0 is a candidate, and a room that can mirror is two candidates (its normal and its mirrored version, weighted by `mirrorChance`).
3. **Test without spawning.** For each candidate the planner works out where the room would sit (`RoomBlueprint.ComputePlacement`, the same Entry-to-Exit math the spawn uses) and moves its boxes there. It is dropped if its boxes overlap any existing room's boxes.
4. **Exit clearance.** A candidate is also dropped if the **clear space in front of its Exit** (`exitClearanceSize`, default 30 x 30 x 30 m) overlaps an existing room. That way every room leaves space for at least one more.
5. **Pick.** One weighted draw among the candidates that survive. (Same odds as "draw, reject, re-draw", with no loop that can hang, and the same seed gives the same rooms.)
6. **Back one step.** If nothing survives, the last spawned room is taken back and a different pick is tried there (that exact room and mirror are banned for that spot). Only rooms the player has not reached can be taken back, and at most `maxBacktracks` (6) in a row.
7. **Last resort.** If even that fails, the highest-weight room for the state is placed anyway, overlapping, and a warning is logged, so the run never stops.

### Boxes and tolerance

- A box is a center plus three half-extent vectors, so one matrix transform handles rotation, mirroring (a scale of -1) and vertical gates. The overlap test is the standard separating axis test (15 axes), with a sphere check first.
- Existing rooms' boxes are shrunk by `overlapTolerance` (0.25 m) before testing, so rooms that only touch don't count.
- **The join between two rooms.** The room before and the new room both have walls and a gate frame right on the join, so their boxes overlap there by design. Shrinking can't fix that (a wall centered on the join stays on the join however thin it gets). Instead, when testing against the room it connects to, the new room's boxes are pushed forward (the way the player travels) by `gateTolerance` (5 m; the room `walls` has a rotated platform that pokes about 4 m past its exit, and at 1.5 m it made several rooms refuse to follow it). The join stops counting, but a room that runs alongside or folds back over the one before it still does. Raise `gateTolerance` if footprints stick out far past the gates.
- The footprint should cover the room's walls too. A room with no footprint is never blocked (and a warning is logged) but its Exit clearance is still checked.

### Staying out of trouble

- **Forced rooms are permanent.** The generator never takes back a room it had to force in, so progress only goes forward. (Taking back and forcing the same rooms again and again is what once froze the editor.)
- **A hard limit per frame.** `RoomChain.BuildAhead` makes at most 16 attempts in one frame, then logs a warning and carries on next frame, so a layout nothing fits can never freeze the editor.
- **Reasons are counted.** The warning for a forced room, and Simulate Rooms, say how many picks were thrown away because they ran into the room before, into older rooms, or had their exit clearance blocked.

### Rooms behind the player

Rooms behind the player are deleted, so only the rooms that currently exist are checked. A later room can reuse that space.

## Checking it: Simulate Rooms

Right-click the **RoomChain** component and choose **Simulate Rooms**. It places `simulateRooms` (200) rooms with the planner alone (nothing is spawned, no Play mode needed) and keeps every one, then logs:

- **overlapping pairs:** every room against every other. Should be 0, except around a forced room.
- **rooms thrown away before one fit:** how often the check rejected a pick.
- **times a room was taken back / forced in anyway:** if forced is above 0, the lists are too narrow for the layout (add turns that go the other way, more forward rooms, or smaller footprints).
- **a count per list, and a count per room** (most used first), to check the weights behave.

The simulation keeps rooms the live game would delete, so it is a harder test than playing. Footprints of the last run can be drawn in the Scene view (green), and the last rejected room in red: tick `drawFootprints` on the RoomChain (off by default). The orange boxes on a room's own Footprint object only show while that object is selected.

## Reasoning

- **Reject, don't restrict.** The generator keeps almost complete freedom, since it only loses the picks that physically can't fit.
- **Mirrors are separate candidates.** A turn and its mirror have different footprints, so one can fit where the other can't.
- **Exit clearance as a one-step lookahead.** It stops dead ends cheaply. Backtracking handles the rest.
- **Pure geometry.** The planner needs no spawned rooms, so candidates cost almost nothing and the safeguard can be tested in the editor.
- **One source for the placement math.** The planner and the spawn use the same function, so the footprint that was checked is exactly where the room appears.

## Starter room (the chute)

The **Starter Room** slot on the RoomChain (header "Start") holds the room every run begins with, normally `thechute`.

- It is placed first, with its Entry on the RoomChain object, never mirrored. `RoomPlanner.PlanStarter` commits it like any room (so it has a footprint and every later room is checked against it) but as *forced*, so the planner never takes it back. It is not counted in the per-list totals. Simulate Rooms places it first too.
- The generator continues from its Exit as normal.
- **The player** is teleported (`CustomPlayerController.Teleport`) to `Entry position + up * spawnHeightAboveEntry (100) + Entry forward * spawnDistanceInFront (1)`, turned to face the Entry's blue arrow and tilted straight down (`PlayerLook.SetLook(forward, 89)`).
- **The Rooms counter** ignores the starter room: the first room passed is the chute and doesn't add to `RoomsPassed`.
- Leave the slot empty to start with a normal random room, as before.
- Give the chute a Footprint (Room component > Create Footprint From Renderers), or nothing is checked against it.
- If 100 m up puts the player inside or above the chute's geometry, change the two spawn fields on the RoomChain.

### Music
The **Start Music** slot (and **Music Volume**) on the RoomChain holds a song. It starts the first time the player crosses the starter room's Exit (the same moment the starter room is counted as passed), and plays **once** (it does not loop). It plays through a 2D AudioSource added to the RoomChain object. Empty slot = no music.

## Gate colors (plain look toggle)
The RoomChain's **Gate colors** header has a **Plain Gate Look** checkbox.
- **Ticked:** every gate's frame uses **Plain Gate Material** (`ProBuilderDefault_Copy`).
- **Unticked:** entrances use **Entry Gate Material** (`Blue`), exits use **Exit Gate Material** (`orange stretch`).
- The three material slots are filled in automatically in the editor when empty (by the materials' GUIDs). Clicking the checkbox changes the gates already in the scene straight away (`OnValidate` -> `ApplyGateLookToAll`, delayed one editor tick), and every room that spawns gets the current look (`SpawnPlanned` -> `ApplyGateLook`). It sets the material on the gate object's MeshRenderers (and its children's), so the prefab assets themselves are never changed.

**Fade-in:** **Music Fade In Seconds** (slider 0 to 30, default 4) sets how long the song takes to rise from silence to **Music Volume**: `volume = musicVolume * clamp01(timeSinceStart / fadeSeconds)`. 0 = full volume at once. Both sliders are read every frame, so you can drag them while the song plays (changing the fade time while it is fading changes the curve at once). Uses unscaled time.
