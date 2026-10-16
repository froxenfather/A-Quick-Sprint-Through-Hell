# Endless Run: Design Notes

Status: **brainstorm only, nothing is coded.** First saved 2026-10-07, updated the same day with top-out/bottom-out rooms and the chaser. Sections 1-6 are the design as I have it. Section 7 is open questions and section 8 is other ideas; neither is decided.

## 1. Concept

An endless first-person platforming run. I go as far and as fast as I can through chained, hand-made rooms while a floating skull chases me. The movement controller (momentum, slide, wall jump, launchpads, moving platforms, plates and doors) is the toolbox I build the rooms from.

## 2. Rooms

- **Size:** one room takes about 5-10 s to run from start to end. That is roughly 100-250 m at sprint speed.
- **Shape:** much longer than wide. Very tall ceilings, to fit wall jumps, launchpads and ramps.
- **Gates:** every room has a large entrance and a large exit. They are always the same size, so any room connects to any other, and each is marked by a very bright light.
- **Contents:** wall jumps, launchpads, ramps, moving platforms, plates and doors, plus the two new surfaces below.

### Lava (floor hazard)
- Touching it bounces the player high into the air.
- It sets the player's max air speed to **8 m/s**.
- The limit is cleared as soon as the player touches ground again, so the next jump is uncapped.
- **Lava can never end the run.** It only costs momentum (and so time against the chaser).
- (Dictated as "basically kill, set max airspeed to eight". "Kill" means kill their momentum.)

### Electric walls (the arena edges)
- Same effect as lava (high bounce, air speed capped at 8 until grounded), but triggered when the player touches the edges of the arena.
- Purpose: no running along or wall jumping off the arena edges. The player gets violently bounced away from the wall.
- Consequence: wall jumps must happen on **interior** structures (pillars, inner walls), not the perimeter.

## 3. Level flow

### Forward rooms
- Picked at random from a **forward** folder holding a large number of rooms with entries and exits that connect.
- The entrance and exit can be at **different heights**, but the room itself has a flat (level) orientation.

### Turn points
- A turn point spawns every **3-5 rooms**, with an escalating chance as rooms pass.
- Six turn types: 90 left, 90 right, 45 left, 45 right, straight up, straight down.
- Left and right turns are more likely than up and down.

| Rooms since last turn | Chance of a turn point |
|---|---|
| 1-2 | 0% |
| 3 | 33% |
| 4 | 67% |
| 5 | 100% (forced) |

(`chance = (rooms - 2) / 3`, clamped to 0-1. A starting formula, not final.)

### Vertical sections
- **Straight up:** 1-2 rooms going straight up (a wall climb). Rooms come from an **upward** folder.
- **Straight down:** a drop of 5-10 floors (a pure drop section). Rooms come from a **downward** folder.

### Ending a vertical section (top-out and bottom-out rooms)
- A climb ends in a **top-out** room and a drop ends in a **bottom-out** room.
- Each one is randomly rotated **0, 90, 180 or 270 degrees**, then continues redirecting the player forward.
- They can also be **slanted 45 degrees**, which gives different pay-offs:
  - After a fall: the player immediately gets to slide.
  - After a climb: the wall-jump reward is an immediate slide.
  - Or the player has to climb a ramp, or "go the extra mile" up a hill, to continue.

### Rules
- Turn point rooms (left, right, slightly left, slightly right, up, down) are never angled up or down. This keeps generation reliable.
- The exception is top-out and bottom-out rooms, which may be slanted 45 degrees.
- Rooms must be oriented correctly so turn points never break the next room's placement.
- Generate several rooms ahead of the player so they never see rooms appear.

### Stretch goal
- Between turn points, forward rooms may occasionally go 45 degrees up (an upward traversal) or 45 degrees down (a fun slide section).
- Angled rooms have an angled floor plan, even if part of it is lava or similar.

## 4. The chaser

A **floating skull** that I design myself.

- Chases the player **directly** (straight line), **through walls**.
- Always visible on the player's screen, through walls, no matter what.
- Tries to keep the player within a set distance and speeds up to hold it.
- When closer than 20 m it slows down to a flat speed.
- **Vertical limit:** it can only move up or down at **3 m/s** at most.
- Catching the player ends the run.

Two versions of the numbers were dictated and they don't agree:

| | Version 1 | Version 2 |
|---|---|---|
| Speeds up to keep within | 30 m | 20 m |
| Slows when closer than | 20 m | 20 m |
| Flat speed | 10 m/s | 8 m/s |
| Standing still, caught after | 30 / 10 = **3.0 s** | 20 / 8 = **2.5 s** |

The note says a stationary player is caught after **exactly 3 seconds**. Only Version 1 gives that. See open questions.

## 5. Proposed folder layout

```
Rooms/
  Forward/        level rooms, gates may differ in height, floor is level
  Up/             vertical shafts, entry at the bottom, exit at the top
  Down/           vertical shafts, entry at the top, exit at the bottom
  Turns/          90 left, 90 right, 45 left, 45 right, to-up, to-down
  TopOut/         end of a climb, rotated 0/90/180/270, flat or 45 degree slant
  BottomOut/      end of a drop, rotated 0/90/180/270, flat or 45 degree slant
```

## 6. What the controller and chaser will need

- A hazard hit call: sets a bounce velocity and a "hazard air speed limit" that is cleared in `UpdateGrounded` when the player touches ground. Same pattern as `SetVelocity` and `lastGroundedTime = NeverGrounded`.
- Apply the limit in `AirMove` **and** `SlideAirMove` (air slides currently have no caps) and after wall jump, slide jump and launchpad boosts. Hitting a hazard should also end a slide or crouch.
- Hazards as trigger volumes work with the existing `IPlayerContact` and the kinematic Rigidbody (same way `PressurePlate` already works).
- Electric walls need to be ignored by `TryFindWall` (a tag or layer check) so they can't be wall jumped.
- The skull needs a shader or render pass that draws it on top of walls (no depth test), plus a trigger for the catch. It does not need NavMesh since it ignores walls.

## 7. Open questions (answer before coding)

1. **Which chaser numbers:** Version 1 (30 m / 10 m/s) or Version 2 (20 m / 8 m/s)? Only Version 1 matches the 3-second statement.
2. **Max catch-up speed:** how fast can the skull go when it speeds up to keep its distance? With no cap, a fast player can never get away from it.
3. **What "distance" means:** straight-line 3D distance, or horizontal distance only? With a 3 m/s vertical limit, a long drop or climb opens a vertical gap bigger than 30 m, so a 3D rule can never be satisfied there.
4. **Lava vs. the chaser:** the lava limit is 8 m/s. At 10 m/s the skull gains 2 m/s on a limited player. At 8 m/s it gains nothing. That is probably a tuning decision, not a bug.
5. **Turn weights:** how much more likely are left/right than up/down? (Starting values in section 8.)
6. **Limit after a ledge:** if the player lands (limit cleared) and later walks off a ledge without jumping, that fall should also be uncapped. Assumed yes.

## 8. Other ideas (not decided)

- **Heading frame, not compass.** The generator keeps one position + rotation. Each room's `Exit` becomes the next `Entry`. Turn rooms rotate it (yaw +/-90, +/-45; pitch +/-90). Vertical rooms are authored as world-vertical shafts, not rotated copies, because gravity stays world down. Top-out and bottom-out rooms just pick a yaw of 0, 90, 180 or 270. A 180 sends the path back the way it came, but the height change from the climb or drop keeps it clear.
- **Overlap check.** Store each room's bounds. Before placing a room, test it against recently placed rooms and re-roll on overlap. Rooms far behind the player are deleted, so only a loop of about 12+ rooms could ever collide. Forward rooms with different gate heights need this too.
- **Turn weights (starting values):** 90 left 25%, 90 right 25%, 45 left 17%, 45 right 17%, up 8%, down 8%. No up right after a down, and bias turns back toward the original heading if the total yaw drifts past about 135 degrees.
- **Beats.** 100-250 m rooms are a lot of hand-building. Optional: build each room from 3-4 shorter "beat" segments (runway + obstacles + exit) so a small pool of beats makes many rooms.
- **Spawn by distance, not room count.** Keep 400-600 m of path generated. Use `Object.InstantiateAsync` (Unity 6) to avoid hitches. The bright gate glow plus fog also hides the next room's pop-in.
- **Safe islands.** Lava never covers 100% of a floor, so a bounce can't chain forever.
- **Drops and the skull.** 5-10 floors is a long fall (v = sqrt(2gh)). Consider a terminal fall speed. Since the skull moves vertically at only 3 m/s, a long drop buys a big lead, so the drop sections work as a breather. Slide-landing at the bottom already gives a capped landing boost (`slideLandingBoostMax`), which makes a nice reward.
- **Climbs.** Wall-jump zigzags in a shaft depend on shaft width (wall jump push x airtime). Wall riding (syllabus Weeks 10-11) will make vertical rooms much better, so do it first. Upward launchpads help too.
- **Telegraphing.** HUD "AIR LIMIT 8" while hazard-limited, orange emissive lava, blue arcing walls, and a distinct sound for each. A heartbeat or growl that scales with the skull's distance.
- **Chaser as a time lead.** Define the skull's start gap in seconds (about 6 s), not meters, so the lead means the same thing at any player speed.
- **Seeds.** Seeded `System.Random` for reproducible bugs, daily seeds and shared runs.
- **Editor tools.** A validator that checks every room has a correct `Entry`, `Exit` and gate size, and a preview window that chains 20 random rooms in the editor.
- **Lighting.** Realtime lighting only (baked lighting doesn't work on runtime-spawned rooms).
- **Seams.** Overlap room connectors slightly. Box colliders meeting edge to edge give tilted edge normals, the same problem that broke coyote time.
