# Movement overhaul — plan & roadmap

_Working plan for the character-movement rework. Pass 1 is done; Passes 2–5 are the roadmap._

## Context

The player controller ([BaseMovement.cs](../Assets/Scripts/Player%20Scripts/BaseMovement.cs), class `PlayerMovement`) started with two different feels:

- **Ground:** `rb.linearVelocity = moveDir * moveSpeed` — instant, snappy, no acceleration or momentum.
- **Air:** `rb.AddForce(moveDir * airControl, Acceleration)` + `LimitAirSpeed()` — gradual acceleration toward the input direction, then a **hard** magnitude clamp to `maxAirSpeed = 8`.

Goal: make ground movement acceleration-based like the air, and eventually replace every hard clamp with a **gentle ease toward a contextual target speed**, so players can freely build speed (wall-jump chains, sliding downhill) and only bleed back to "normal" when grounded and not sliding. End goal is migrating off `Rigidbody` to a hand-rolled `CustomMovementController`.

Design decisions locked in:

- **Air keeps momentum; holding a direction can only accelerate you up to a soft cap *in that direction*.** Project current horizontal velocity onto the camera-relative wish direction. If that projected speed is below `airSoftCap` (~8), ease toward it along the wish direction; once it reaches the cap, stop adding (do **not** decelerate — momentum above the cap from boosts/slides is preserved untouched). No clamp on total speed. Diagonals are handled for free: `moveDirection` is already camera-relative and normalized, so a diagonal input caps at `airSoftCap` along that diagonal. Holding a fixed direction with a fixed camera cannot pump speed past the cap; rotating the camera redirects your momentum (air control) and each new direction re-caps at `airSoftCap`, so there is no compounding "strafe to fly" exploit.
- **Speed bleeds off only when grounded and not sliding** — the one place a pull toward walk (5.5) / sprint (7.5) exists. Airborne or sliding, momentum is preserved.
- **One universal soft ceiling** (`softMaxSpeed` ~18, gentle `MoveTowards` decel) so infinite stacking eventually relents, plus a high hard clamp (`hardMaxSpeed` ~35) purely as a physics sanity guard.
- Sprint = hold LeftShift. Slide/crouch = thumb mouse buttons (`Mouse.current.backButton` / `forwardButton`).

## Devlogs (write throughout)

Devlogs live in `A Quick Sprint Through Hell/Devlogs/`. Write entries generously — this is a solo learning project (see `Course Info Folder/`, `Research/`) and the reasoning matters as much as the diff.

- **File per entry:** `A Quick Sprint Through Hell/Devlogs/YYYY-MM-DD-<short-topic>.md`. Multiple per day is fine.
- **Cadence:** at minimum one entry per pass below, plus a short entry any time a design decision is made or reversed (e.g. the air-cap model: hard clamp → tiny wish-speed → directional soft cap, and *why* each was rejected).
- **Template:**
  ```markdown
  # <date> — <topic>

  ## Goal
  What I set out to change and why it mattered.

  ## What changed
  Files touched, fields added/removed, the core code idea in a few lines.

  ## Reasoning / decisions
  Options considered, what was picked, what was rejected and why.

  ## How it felt / tested
  In-editor observations, numbers from the Inspector, tuning values landed on.

  ## Next
  What this sets up.
  ```
- Final checklist item of every pass: write the devlog entry.

---

## Pass 1 — ground movement → acceleration-based ✅ DONE (2026-09-10)

Only the grounded branch of `Move()` changed. Air branch, jump, gravity, collision handling untouched.

**Fields (replaced `moveSpeed`):**

```csharp
[Header("Ground Movement")]
[SerializeField] private float walkSpeed = 5.5f;
[SerializeField] private float sprintSpeed = 7.5f;
[SerializeField] private float groundAccel = 55f;          // ramp toward target when under it
[SerializeField] private float groundOverspeedDecel = 12f; // gentle skid toward target when over it (arrived hot)
[SerializeField] private float groundFriction = 120f;      // no input -> decelerate VERY quickly, near-instant stop
[SerializeField] private float landingGrace = 0.15f;       // after touching ground, suppress friction this long (bhop window)
```

**Input:** `sprinting = Keyboard.current.leftShiftKey.isPressed;` (hold to sprint).

**Grounded branch:** support-relative, then

```csharp
if (!wasGrounded) landTime = Time.time;
bool inLandingGrace = Time.time - landTime < landingGrace;

if (hasInput)
{
    float rate = horiz.magnitude > targetSpeed ? groundOverspeedDecel : groundAccel;
    horiz = Vector3.MoveTowards(horiz, moveDirection * targetSpeed, rate * dt);
}
else if (!inLandingGrace)
{
    horiz = Vector3.MoveTowards(horiz, Vector3.zero, groundFriction * dt);
}
// write horiz + supportVelocity back, Y untouched
```

`wasGrounded = grounded;` at the end of `Move()`.

`Vector3.MoveTowards` toward `moveDirection * targetSpeed` is self-limiting: accelerates toward the target, decelerates toward it if over (bleeds off at `groundOverspeedDecel`), redirects as you turn, never overshoots — so holding forward settles at exactly walk/sprint speed. All in `FixedUpdate`, frame-rate independent.

**Follow-up fix (same day):** landing without holding a key hit `groundFriction = 120` and zeroed momentum in ~2 frames — couldn't chain jumps. Added the `landingGrace` window that suppresses the no-input friction branch for 0.15s after touchdown. Next option if landings still bleed while steering: also skip the `groundOverspeedDecel` path during grace.

**Note:** `SampleScene.unity` had the old `moveSpeed: 8.5` serialized on the player. That field is gone, so the new fields load at code defaults — tune on the PlayerMovement component in the Inspector.

Devlog: `2026-09-10-ground-accel-movement.md`.

---

## Roadmap

### Pass 2 — Air model: directional soft cap, momentum preserved

Replace the air branch + delete `LimitAirSpeed()` and `maxAirSpeed`:

```csharp
[Header("Air Movement")]
[SerializeField] private float airSoftCap = 8f;  // max speed you can build toward the direction you're holding
[SerializeField] private float airAccel   = 12f; // how fast you ease toward airSoftCap in that direction

// in Move() else-branch (moveDirection is already camera-relative + normalized):
Vector3 hv = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
float speedInWishDir = Vector3.Dot(hv, moveDirection);          // current speed toward where you're holding
if (speedInWishDir < airSoftCap)
{
    float add = Mathf.Min(airAccel * Time.fixedDeltaTime, airSoftCap - speedInWishDir);
    hv += moveDirection * add;
    rb.linearVelocity = new Vector3(hv.x, rb.linearVelocity.y, hv.z);
}
// speedInWishDir >= airSoftCap: add nothing. Never decelerate here -> boosts/slide speed above the cap survive.
```

- Holding one direction, fixed camera: eases to `airSoftCap` along it, then stops — cannot pump past it.
- Already above `airSoftCap` in that direction (from a boost): `add` path skipped, momentum untouched.
- Rotating the camera: redirects momentum (air control); the new direction also caps at `airSoftCap`, no compounding gain.
- Diagonals: `moveDirection` normalized + camera-relative, so a diagonal hold caps at `airSoftCap` along the diagonal.
- Look-only does nothing: no W/A/S/D held → `moveInput` zero → `moveDirection` zero → no speed added. Releasing all keys midair = pure coasting.
- No clamp on *total* horizontal air speed — the universal soft ceiling (Pass 4) is the only backstop.

Optional refinement (redirect-only when already fast): if `hv.magnitude` after the add exceeds `Mathf.Max(preAddSpeed, airSoftCap)`, rescale `hv` back so air accel steers high momentum but never lengthens it. Decide during tuning.

### Pass 3 — Sprint-jump boost, slide, wall jump

- **Sprint-jump boost:** in `Jump()`, if `sprinting && moveInput` non-zero, after setting jump Y, raise horizontal speed to at least `sprintJumpBoost` (~9) along `moveDirection`.
- **Slide:** input = `Mouse.current.backButton.isPressed` while grounded + moving. Add `MovementState` enum (`Walking, Sprinting, Sliding, Airborne`). On entry, bump speed up to `slideEntrySpeed` (8) if slower; while sliding, skip the walk/sprint pull, apply low `slideFriction` (~2), and add acceleration along the downhill direction `Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized * slideSlopeAccel * (1 - groundNormal.y)` — downhill gains, uphill's opposing slope component bleeds speed. Store `groundNormal` in `OnCollisionStay` (already iterating contacts). Exit on button release / speed `< minSlideSpeed` (~3) / airborne.
- **Wall jump:** spherecast horizontally (`wallCheckDistance` ~0.6) when airborne; on jump press against a wall, reflect horizontal velocity off the wall normal, raise horizontal speed to `wallJumpBoost` (~10), set Y to `wallJumpUpVelocity` (~5.5), apply a short `wallJumpCooldown`. Chainable because air preserves momentum.

### Pass 4 — Universal ceilings + shared helper

```csharp
static Vector3 SoftClampHorizontal(Vector3 vel, float cap, float decelRate, float dt)
{
    Vector2 h = new Vector2(vel.x, vel.z);
    float sp = h.magnitude;
    if (sp > cap && sp > 1e-4f) { h *= Mathf.MoveTowards(sp, cap, decelRate * dt) / sp; vel.x = h.x; vel.z = h.y; }
    return vel;
}
```

Apply every FixedUpdate in all states: `SoftClampHorizontal(vel, softMaxSpeed ~18, softMaxDecel ~8, dt)` then a hard clamp at `hardMaxSpeed` ~35. Generous enough that normal wall-jump/slide play never feels it.

### Pass 5 — Rigidbody → CustomMovementController

Structure Passes 1–4 so gameplay logic never touches `rb` mid-pipeline: `FixedUpdate` gathers a context (grounded, `groundNormal`, `moveDirection`, `sprinting`, `sliding`, `dt`), reads `Vector3 velocity = rb.linearVelocity`, runs the pipeline of pure-ish functions, writes `rb.linearVelocity = velocity` once at the end. Then the swap only touches the integrate/collide boundary:

1. `rb.linearVelocity` read/write → a `Vector3 velocity` field the controller owns.
2. Integrate with `CharacterController.Move(velocity * Time.deltaTime)` (or a hand-rolled capsule-cast mover).
3. Gravity fully manual: `velocity.y -= gravity * dt` (already half-done via `extraGravity`).
4. Grounded: `controller.isGrounded` + a short down spherecast for `groundNormal` and coyote time; retire `OnCollisionStay` / `OnCollisionExit`.
5. Ground snap on slopes/stairs: short downward cast after `Move`, reposition if within snap distance and grounded last frame.
6. Moving platforms: the down cast returns the platform collider → `hitRb.GetPointVelocity(point)` → the same `supportVelocity` value already tracked.
7. Walls: forward/lateral spherecast each frame for wall-jump normals and to cancel into-wall velocity.
8. Give the controller `SetVelocity` / `AddVelocity`; update external `rb.linearVelocity = ...` writers — today only [LaunchBehavior.cs](../Assets/Scripts/Player%20Scripts/LaunchBehavior.cs).

The enum, all tunables, `SoftClampHorizontal`, the air-accel math, and the slide math carry over unchanged.

## Files

- **Modify:** `A Quick Sprint Through Hell/Assets/Scripts/Player Scripts/BaseMovement.cs` (Passes 1–4).
- **Devlogs:** `A Quick Sprint Through Hell/Devlogs/` — dated entry per pass and per design decision.
- **Later:** `A Quick Sprint Through Hell/Assets/Scripts/Player Scripts/LaunchBehavior.cs` (Pass 5, call-site update only).
