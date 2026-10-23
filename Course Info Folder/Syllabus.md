# Independent Study Syllabus

## Physics-Based First-Person Movement and Parkour in Unity

## Course Description

This independent study focuses on learning the Unity game engine and applying software engineering, 3D mathematics, and physics concepts to the development of a first-person parkour movement system, and then building a procedurally generated endless run on top of it.

The first portion of the course focused on learning Unity through smaller programming, physics, and movement projects. Development then transitioned into an iterative first-person movement system, beginning with basic movement and progressively introducing acceleration, momentum, air control, slopes, sliding, slide jumping, wall jumping, and wall riding.

The course was intentionally front-loaded, with the primary development of the movement controller planned for the first eight weeks. The required movement system, including all wall mechanics, was in fact completed ahead of schedule, by Week 8 (see the note under Week 8). The scope of the remaining weeks has therefore been changed: the semester now moves past the original wall mechanics, integration, and environment-design weeks and toward a finalized product.

The remaining weeks have two parts. First, the movement engine is polished, documented, and tested (Week 9). Second, a procedural room generator is built that chains hand-authored rooms into an endless run, turns the player left, right, up, and down, and stitches every exit to the next entrance (Weeks 10-15). The core gameplay and the code behind it are the priority. Visual presentation is a stretch goal.

The central focus of the project remains the preservation and redirection of player momentum across different movement states and environmental interactions, and now across a level that is generated at runtime.

If the required scope is completed ahead of schedule, automatic bar traversal (bar swings) and visual presentation will be explored as stretch goals.

## Learning Objectives

By the end of this independent study, I will be able to:

- Navigate Unity and understand its scene, object, component, prefab, physics, and scripting systems.
- Develop gameplay systems using C# and Unity's scripting API.
- Apply vectors, acceleration, gravity, collision detection, raycasts, and surface normals to 3D movement.
- Design and implement a modular first-person character controller.
- Implement acceleration, deceleration, friction, gravity, air control, and slope-based movement.
- Preserve and redirect player momentum between different movement states.
- Implement momentum-based sliding and slide jumping.
- Implement wall jumping and sustained wall riding.
- Design a 3D environment around the capabilities and limitations of a movement system.
- Analyze and tune movement mechanics based on responsiveness, consistency, and player feedback.
- Document the architecture and mathematical behavior of a complete movement system.
- Design a procedural level generator that chains hand-authored rooms through a shared entry and exit contract.
- Apply rotations and transform math to align rooms and redirect the player's heading left, right, up, and down.
- Detect and prevent overlapping geometry in generated levels.
- Stream generated content at runtime without hitches or unbounded memory growth.
- Build gameplay systems (hazards and a chaser) that interact with a momentum-based controller.
- Explore additional traversal mechanics as time permits.

## Tools and Technologies

- Unity
- C#
- Unity Input System
- Unity Physics
- Unity Editor scripting (custom tools and validators)
- Unity Test Framework (automated generator tests)
- Blender or other 3D modeling software
- Git / GitHub
- Unity Profiler and debugging tools

---

# Section I: Unity Fundamentals and Core Movement

## Week 1: Unity Fundamentals and Scene Construction

### Goal

Become comfortable working inside Unity and understand the basic structure of a Unity project.

### Tasks

- Learn the Unity Editor, Scene View, Game View, Hierarchy, Inspector, Project window, and Console.
- Understand GameObjects, Components, Transforms, Scenes, Materials, Colliders, and Prefabs.
- Learn Unity's coordinate system and basic 3D transformations.
- Create and manipulate simple 3D objects.
- Import basic custom 3D models into Unity.
- Create a small 3D environment containing platforms, ramps, obstacles, lighting, and collision.
- Establish a Git repository and project structure.

### Deliverable

A small explorable 3D scene built from Unity primitives and imported assets.

---

## Week 2: C# and Unity Gameplay Programming

### Goal

Learn how C# scripts interact with objects, components, input, and events inside Unity.

### Tasks

- Learn `MonoBehaviour`, `Start`, `Update`, and `FixedUpdate`.
- Understand Unity's component-based scripting model.
- Read player input using Unity's Input System.
- Manipulate GameObjects and Components through scripts.
- Use serialized fields to expose configurable parameters.
- Work with collisions and trigger volumes.
- Create small scripted interactions such as moving platforms, launch pads, switches, and hazards.
- Practice communication between scripts and GameObjects.

### Deliverable

A small interactive environment containing multiple independently scripted objects and physics interactions.

---

## Week 3: 3D Physics and Vector Mathematics

### Goal

Develop an understanding of the physics and mathematics required for the final movement system.

### Tasks

- Experiment with Unity's physics systems.
- Apply forces, impulses, acceleration, drag, and gravity.
- Work with `Vector3` position, direction, and velocity.
- Use vector magnitude and normalization.
- Use dot products and cross products.
- Project vectors onto surfaces and directions.
- Calculate movement relative to surface normals.
- Use raycasts and sphere casts to detect environmental geometry.
- Experiment with ramps and angled surfaces.
- Visualize velocity vectors, collision normals, and raycasts for debugging.

### Deliverable

A physics demonstration scene showing forces, velocity, raycasts, surface normals, and interactions with differently angled surfaces.

---

## Week 4: Basic First-Person Movement

### Goal

Develop the first functional version of the first-person character controller.

### Tasks

- Implement mouse-controlled first-person camera movement.
- Implement directional WASD movement.
- Implement basic player acceleration and movement speed.
- Implement gravity.
- Implement grounded detection.
- Implement jumping.
- Handle collision with environmental geometry.
- Handle basic slopes and stairs.
- Track player velocity and current speed.
- Expose important movement parameters for testing.

### Deliverable

A functional first-person controller capable of walking, running, looking, jumping, and navigating a simple obstacle course.

---

## Week 5: Smooth Movement and Controller Architecture

### Goal

Transform the basic controller into a smooth, configurable movement system capable of supporting more advanced mechanics.

### Tasks

- Replace immediate velocity changes with acceleration-based movement.
- Implement smooth deceleration.
- Implement ground friction.
- Improve directional changes while moving.
- Tune maximum ground speed.
- Refine camera and mouse movement.
- Separate player input from movement calculations where appropriate.
- Design a movement state architecture.
- Establish initial movement states including:
  - Grounded
  - Airborne
  - Sliding
  - Wall Riding
- Create debugging tools for displaying:
  - Player speed
  - Velocity
  - Grounded state
  - Current movement state
  - Detected surface information
- Begin construction of a gray-box parkour testing environment.

### Deliverable

Version 2.0 of the first-person controller featuring smooth acceleration, deceleration, friction, configurable movement parameters, and an architecture designed for additional traversal mechanics.

---

# Section II: Momentum-Based First-Person Parkour

## Week 6: Air Movement, Momentum, and Slopes

### Goal

Expand the movement system to preserve momentum across jumps and respond naturally to terrain.

### Tasks

- Implement separate ground and air acceleration.
- Implement controllable air movement.
- Preserve horizontal momentum when leaving the ground.
- Improve transitions between grounded and airborne states.
- Improve slope detection using surface normals.
- Project movement appropriately across angled surfaces.
- Allow downhill movement to increase player speed.
- Reduce player velocity appropriately when traveling uphill.
- Prevent unintended acceleration from invalid surfaces.
- Tune gravity and jumping around the completed momentum model.
- Expand the testing environment with ramps and slope-based movement challenges.

### Deliverable

A momentum-based movement system that supports smooth ground movement, air control, jumping, and slope-based acceleration.

---

## Week 7: Sliding and Terrain Interaction

### Goal

Implement sliding as the first major momentum-based parkour mechanic.

### Tasks

- Implement crouching and sliding.
- Require appropriate player velocity to initiate a slide.
- Reduce friction while sliding.
- Preserve momentum when entering a slide.
- Gradually reduce momentum while sliding across flat terrain.
- Accelerate the player while sliding downhill.
- Decelerate the player while sliding uphill.
- Handle transitions between running, sliding, and airborne movement.
- Prevent sliding from producing unintended infinite acceleration.
- Expand the testing environment with downhill, uphill, and flat sliding sections.

### Deliverable

A functional momentum-based sliding system that responds to player speed, terrain angle, and existing velocity.

---

## Week 8: Slide Jumping, High-Speed Movement, and Core Integration

### Goal

Complete the primary ground movement system and integrate running, jumping, slopes, sliding, and slide jumping into a consistent momentum model.

### Tasks

- Implement jumping directly from a slide.
- Preserve forward momentum during a slide jump.
- Tune the relationship between slide velocity and jump velocity.
- Determine whether controlled momentum boosts should occur during successful slide jumps.
- Improve airborne control following a slide jump.
- Add speed-responsive field-of-view changes.
- Add visual feedback for high-speed movement.
- Test repeated slide-jump sequences for unintended acceleration.
- Test transitions between all existing movement states.
- Tune running, jumping, slope traversal, sliding, and slide jumping as a complete system.

### Deliverable

A complete core locomotion system supporting smooth movement, momentum, slopes, air control, sliding, slide jumping, and high-speed movement feedback.

---

> ## AHEAD OF SCHEDULE: SCOPE CHANGE AS OF WEEK 8
>
> **As of Week 8, the project has gone significantly further than this syllabus expected.** Everything this syllabus required of the movement system, originally scheduled through Week 12, is already implemented in Unity. That includes wall detection, wall jumping, wall riding, movement chaining, and integration.
>
> Because of this, the scope of the course is changing. The remaining weeks will **not** repeat or pad out the original wall-mechanics, integration, and environment-design weeks. They will push the project toward a **finalized product**: first a polished and documented movement engine, then a procedurally generated endless run built on top of it.
>
> The original Weeks 9-12 are consolidated directly below, marked complete. The redefined Weeks 9-15 follow in Sections III and IV.

### Completed Ahead of Schedule: Original Weeks 9-12

Every required item from the original Weeks 9 through 12 has been completed.

#### Original Week 9: Wall Detection and Wall Jumping (Complete)

**Goal:** Allow environmental surfaces to redirect the player's airborne momentum.

- [x] Detect walls adjacent to the player.
- [x] Determine valid wall surfaces using surface normals and angles.
- [x] Determine which side of the player contains the detected wall.
- [x] Implement directional wall jumping.
- [x] Calculate wall-jump direction using player velocity and wall normals.
- [x] Preserve appropriate forward momentum when leaving a wall.
- [x] Redirect momentum away from the contacted surface.
- [x] Prevent repeated exploitation of the same wall.
- [x] Tune wall-jump timing and player control.
- [x] Add wall-jump testing sections to the parkour environment.

**Deliverable (met):** A reliable wall-jumping system capable of redirecting player momentum using environmental geometry.

#### Original Week 10: Wall Riding and Wall Running (Complete)

**Goal:** Extend wall interaction into sustained momentum-based traversal.

- [x] Implement a wall-riding movement state.
- [x] Determine the player's direction of travel relative to a wall.
- [x] Project player velocity along the wall surface.
- [x] Modify gravity while wall riding.
- [x] Preserve momentum when entering a wall ride.
- [x] Establish minimum velocity requirements for entering and maintaining a wall ride.
- [x] Implement wall-ride termination conditions.
- [x] Transition between wall riding, wall jumping, airborne movement, and ground movement.
- [x] Add camera feedback during wall traversal.
- [x] Expand the environment with wall-riding sections.

**Deliverable (met):** A functional wall-riding system capable of preserving player momentum along valid wall surfaces.

#### Original Week 11: Wall Integration and Movement Chaining (Complete)

**Goal:** Complete the required movement feature set by integrating wall mechanics with the existing momentum system.

- [x] Support transitions between multiple wall surfaces.
- [x] Improve transitions between wall riding and wall jumping.
- [x] Preserve velocity across appropriate movement-state changes.
- [x] Redirect momentum rather than resetting player velocity.
- [x] Test transitions between walls, ground movement, slides, and airborne movement.
- [x] Eliminate unexpected momentum loss during transitions.
- [x] Prevent unintended or infinite acceleration.
- [x] Improve player control at high speeds.
- [x] Test complex movement sequences.
- [x] Tune wall mechanics alongside the existing movement controller.

Example traversal sequence:

`Run -> Downhill Ramp -> Slide -> Slide Jump -> Wall Ride -> Wall Jump -> Landing`

**Deliverable (met):** A feature-complete first-person parkour controller supporting smooth movement, momentum, slopes, sliding, slide jumping, wall jumping, and wall riding.

#### Original Week 12: Movement System Integration and Stability (Complete)

**Goal:** Improve the reliability and consistency of the completed movement system without introducing additional required mechanics.

- [x] Conduct systematic testing of all movement states.
- [x] Identify conflicts between movement mechanics.
- [x] Fix movement-state transition bugs.
- [x] Test unusual collision and terrain conditions.
- [x] Test movement at unusually high and low velocities.
- [x] Fix camera clipping or movement feedback issues.
- [x] Refine ground and wall detection.
- [x] Address physics exploits and unintended acceleration.
- [x] Tune acceleration, friction, gravity, air control, and jump forces.
- [x] Refactor or simplify movement code where necessary.

**Deliverable (met):** A stable and integrated version of the required movement system suitable for use in the final parkour environment.

#### Also Built Beyond the Original Syllabus

- Coyote time.
- Moving-platform velocity transfer.
- Launchpads.
- Pressure plates and doors.

---

# Section III: Movement Engine Polish

## Week 9: Polish of the Movement Engine

### Goal

Bring the completed movement engine to a stable, documented, and thoroughly tested state, so it can be a dependable foundation for the generated rooms.

### Tasks

- Document the movement architecture: frame order, each movement state, the transitions between them, and the math behind each mechanic.
- Fix remaining bugs, movement-state conflicts, unintended acceleration, and unexpected momentum loss.
- Build dedicated testing environments for each mechanic (slopes, sliding, slide jumping, wall jumping, wall riding, launchpads, doors) and for unusual collision cases.
- Truly push the engine: extreme speeds, tight geometry, long repeated chains, and variable frame rates.
- Refine the feel of the engine until it is as good as it feels: acceleration, friction, gravity, air control, jump forces, and camera feedback.
- Measure and record the engine's reach envelope: maximum jump distance and height, slide-jump gap, and wall-jump reach at several entry speeds. These numbers set the size limits for room gates and gaps.
- Clean up and freeze the controller functions that rooms and hazards will call, such as setting velocity and a future hazard-hit call.
- Refactor or simplify movement code where necessary.

### Deliverable

A stable, documented movement engine, a set of testing environments, and a table of reach-envelope measurements.

---

# Section IV: Procedural Endless Run

## Week 10: Room Contract and Straight Stitching

### Goal

Define what a room is, and prove that rooms can be stitched together seamlessly, entrance to exit, in a straight line.

### Tasks

- Define the room contract: Entry and Exit anchors, gate size, bounds, and room type. Size gates using the Week 9 reach envelope.
- Create the room data on each room prefab, and set up a folder layout by room type (Forward, Left, Right, SlightLeft, SlightRight, Up, Down, TopOut, BottomOut).
- Implement the heading frame: the generator keeps one position and rotation, and each room's Exit becomes the next room's Entry.
- Place each new room by aligning its Entry to the previous Exit in both position and rotation, including gates at different heights.
- Build an editor validator that checks every room for correct Entry and Exit anchors, gate size, and bounds.
- Add a seeded random source so any run can be reproduced.
- Add gizmos that draw anchors, gates, and bounds.
- Author at least ten gray-box Forward rooms.

### Deliverable

A generator that chains ten or more Forward rooms end to end with no gaps or misalignment, and a validator that passes every room.

---

## Week 11: Horizontal Turns and Overlap Prevention

### Goal

Turn the player left and right, while guaranteeing generated rooms never overlap.

### Tasks

- Author turn rooms: 90 degree left and right, and 45 degree slight left and slight right. Turn rooms only redirect the player and are never angled up or down.
- Rotate the heading frame when a turn room is placed.
- Implement weighted room selection with starting weights, and bias turns back toward the original heading when total rotation drifts too far.
- Store each room's bounds and test every new room against recently placed rooms. Re-roll on overlap, with a fallback when no turn fits.
- Delete rooms far behind the player so that only recent rooms need to be checked.
- Build an editor preview window that chains about 20 random rooms.
- Test generated turns with the real controller at speed.

### Deliverable

An endless winding path across the horizontal plane, built from a small pool of authored rooms, with no overlaps.

---

## Week 12: Vertical Sections and Recovery Rooms

### Goal

Turn the player up and down, and bring them back to level movement afterward.

### Tasks

- Author Up sections (a wall climb of 1-2 rooms) and Down sections (a drop of 5-10 floors). Author them as world-vertical shafts rather than rotated copies, because gravity always points world-down.
- Author top-out and bottom-out rooms that end a climb or drop. Each is randomly rotated 0, 90, 180, or 270 degrees, and may be slanted 45 degrees.
- Extend the generator to treat a vertical section as a sequence that must end in the matching top-out or bottom-out room.
- Extend the overlap check to three dimensions, and never place an Up directly after a Down.
- Support entrances and exits at different heights.
- Add Up and Down to the weighted selection.
- Verify with the controller that climbs are achievable with the wall mechanics and that drops end in a satisfying landing.

### Deliverable

A generator that can redirect the player left, right, up, and down, and always return them to level movement.

---

## Week 13: Runtime Streaming, Seeds, and Pacing

### Goal

Make the generator behave like a real endless run: continuous, hitch-free, reproducible, and paced.

### Tasks

- Spawn rooms by distance ahead of the player (several hundred meters of path), and delete rooms far behind.
- Avoid hitches with pooling or asynchronous instantiation, and avoid visible pop-in.
- Add a seed system for reproducing bugs and sharing runs.
- Add a pacing and difficulty ramp: change the room pool by distance and insert breather rooms.
- Check floating-point precision on very long runs, and add origin shifting if it becomes a problem.
- Confirm that lighting works correctly on rooms that are spawned at runtime.
- Profile spawn cost, memory, and garbage collection.

### Deliverable

An endless run that can continue indefinitely without hitches or growing memory use.

---

## Week 14: Core Gameplay Layer

### Goal

Add the hazards and the chaser that turn the generated rooms into a playable game.

### Tasks

- Add a controller function for hazard hits that sets a bounce velocity and a temporary air-speed limit, cleared when the player touches the ground.
- Build hazards: lava that limits the player's air speed but never ends the run, and bouncing walls along arena edges that stop running along or wall jumping off the edges.
- Build the chaser: a floating skull that pursues the player directly through walls, is always visible, and ends the run when it catches the player. Settle the open numbers in the design notes (distances, speeds, and the vertical limit).
- Build the run loop: start, fail on being caught, restart with a new seed, and a distance score.
- Author additional rooms that use the hazards.
- Tune the chaser and hazards against the movement engine.

### Deliverable

A complete playable loop: run, get chased, fail, and restart.

---

## Week 15: Final Testing, Documentation, and Demonstration

### Goal

Complete testing and documentation of the project, and use any remaining time for the stretch goals.

### Core Tasks

- Add an automated test that generates 1,000 seeds and checks for overlaps, disconnected gates, and rooms that fail validation.
- Conduct end-to-end playtesting of the endless run, and tune pacing, hazards, and the chaser.
- Fix remaining high-priority bugs.
- Record final movement parameters and final generator parameters (weights, distances, limits).
- Document the generator architecture: the room contract, the heading frame, overlap prevention, and streaming.
- Document the movement architecture and state transitions.
- Document relevant vector mathematics, rotation math, and physics calculations.
- Explain how momentum is preserved or redirected by each major traversal mechanic.
- Compare the initial first-person controller with the completed system.
- Record a final demonstration of the endless run.
- Complete a written reflection on technical challenges, design decisions, and results.

### Stretch Goals

If the required scope, documentation, and testing are complete, pursue the stretch goals listed in the Stretch Goals section below.

### Final Deliverables

- Playable procedurally generated endless run.
- Complete Unity project and C# source code.
- Room authoring guide and generator documentation.
- Documented movement architecture.
- Movement-state diagram.
- Technical explanation of physics, momentum, and generator calculations.
- Final demonstration video.
- Written reflection on development and results.
- Any completed stretch-goal work.

---

# Required Project Scope

Successful completion of the independent study will require implementation and integration of the following systems:

1. Basic first-person movement
2. Smooth acceleration and deceleration
3. Ground friction and momentum
4. Air control and jumping
5. Slope-based movement and downhill acceleration
6. Sliding
7. Slide jumping
8. Wall detection
9. Wall jumping
10. Wall riding
11. Movement chaining and momentum preservation
12. A stable, documented, and thoroughly tested movement engine
13. A room contract and generator that stitches rooms together from each exit to the next entrance
14. Procedural turns left, right, up, and down, with overlap prevention and recovery rooms
15. Runtime streaming of an endless, seeded run
16. A core gameplay layer: hazards, a chaser, and a start, fail, and restart loop

Items 1-11 were completed ahead of schedule.

---

# Stretch Goals

The following work is outside the required scope of the independent study and will only be pursued after the required systems are complete and stable.

## Automatic Bar Traversal (Bar Swings)

The player will automatically interact with horizontal traversal bars when approaching from a valid direction and speed.

The system will use the player's incoming velocity to automatically attach the player to the bar and convert horizontal momentum into rotational movement. The player will then automatically release from the bar, redirecting the incoming momentum primarily into vertical and forward velocity.

This mechanic will explore the conversion and redirection of player momentum without requiring explicit grab or release input.

If pursued, it would involve:

- Creating environmental traversal bars.
- Detecting valid bar interactions automatically.
- Determining whether player direction and speed permit interaction.
- Automatically attaching the player without requiring additional input.
- Measuring incoming player velocity.
- Converting incoming horizontal velocity into rotational movement around the bar.
- Automatically determining an appropriate release point.
- Automatically releasing the player.
- Redirecting momentum primarily into vertical and forward launch velocity.
- Scaling the resulting launch based on incoming speed.
- Integrating bar traversal with existing airborne movement, and with the room contract so that bars can appear in generated rooms.

## Visual Presentation

Making the game look good: factory-themed environment art, lighting, shaders, fog, post-processing, and audio polish. The core gameplay and the code behind it are the priority, and this work is deliberately out of the required scope.

Gate presentation ideas, to make crossing from one room into the next feel like an entrance:

- A bright frame glow on each gate, which also helps hide pop-in.
- A sound sting and a "ROOM 14" HUD tick when the player crosses a gate, to give a sense of progress.
- A light color change per section, such as hotter colors as the run goes on.
- A steam or sparks burst as the player crosses.
- An iris-style gate that opens based on the player's distance rather than on a timer. It is visual only and has no colliders, so it can never block the player's momentum.

---

# Evaluation

Progress will be evaluated based on:

- Completion of weekly milestones.
- Functionality and stability of the first-person movement controller.
- Quality and responsiveness of basic and smooth movement.
- Successful implementation of momentum-based sliding and slide jumping.
- Successful implementation of wall jumping and wall riding.
- Effective application of 3D mathematics and physics concepts.
- Preservation and redirection of momentum between movement states.
- Quality and maintainability of the C# codebase.
- Integration and consistency between movement mechanics.
- Correctness and robustness of the procedural room generation: stitching, turning in all four directions, and overlap prevention.
- Quality and playability of the endless-run demonstration, including its core gameplay.
- Clarity and completeness of technical documentation.
- Analysis of design decisions, technical challenges, and results.

Completion of automatic bar traversal and the visual presentation work is not required for successful completion of the independent study.
