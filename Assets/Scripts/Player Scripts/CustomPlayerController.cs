using System.Collections.Generic;
using IObjects;
using UnityEngine;
using UnityEngine.InputSystem;

// Setup:
// Kinematic Rigidbody + CapsuleCollider on Player root.
// Feet are assumed to sit at the object's pivot.
// Camera tied to head
// Controller philosophy:
// - Rigidbody does NOT drive movement.
// - We manually integrate velocity accel etc thru good ol math
// - Capsule sweeps detect collisions before movement.
// - Collisions are resolved using "collide and slide".
// - Kinematic Rigidbody mainly exists for:
//      1. Trigger interactions
//      2. Rigidbody interpolation 
//      3. punching objects and such!

// Ladies and Gentlemen
// the FRAGNUM OPUS!

// Warning: This is a LOT of code. Explanations for the functions in detail are found in Research/How It Works
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class CustomPlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private bool allowWASD = true;

    [Header("Ground Movement")]
    [SerializeField] private float walkSpeed = 12f;
    [SerializeField] private float sprintSpeed = 20f;

    // How quickly velocity approaches target movement speed.
    [SerializeField] private float groundAccel = 90.9f;

    // Deceleration when already moving faster than our desired speed.
    [SerializeField] private float groundOverspeedDecel = 52.1f;

    // Deceleration when no movement input is held.
    [SerializeField] private float groundFriction = 68.6f;

    // Brief friction-free window after landing.
    // Lets momentum survive landings / allows bunny hopping.
    [SerializeField] private float landingGrace = 0.04f;

    [Header("Air Movement")]
    [SerializeField] private float jumpVelocity = 14.68f;

    // Downward acceleration in m/s^2.
    [SerializeField] private float gravity = 30.01f;

    // Horizontal acceleration while airborne.
    [SerializeField] private float airControl = 15.22f;

    // Prevent infinite horizontal air acceleration.
    [SerializeField] private float maxAirSpeed = 20f;

    // How fast horizontal speed bleeds off when above maxAirSpeed.
    // High = acts like a hard clamp, low = momentum carries through the air.
    [SerializeField] private float airOverspeedDecel = 52.1f;

    // Coyote time: for this many seconds after walking off an edge, pressing jump still counts as a normal ground jump.
    // Named after the cartoon coyote who hangs in the air before falling. Just saw Coyote Vs Acme and was reminded of this!
    // 0 turns it off. Around 0.1 feels forgiving without looking like a double jump.
    [SerializeField] private float coyoteTime = 0.1f;

    [Header("Slide")]
    // Friction while sliding. Way lower than groundFriction so momentum carries.
    [SerializeField] private float slidingGroundFriction = 4f;

    // Speed added the moment a slide starts...
    [SerializeField] private float slideBoost = 5f;

    // ...but only if we're going slower than this. So the most a boost can ever give is about this + slideBoost.
    [Range(0f, 60f)]
    [SerializeField] private float slideBoostMaxSpeed = 25f;

    // Minimum speed (m/s, same units as walkSpeed/sprintSpeed) needed to START a slide (grounded OR airborne). Too slow and we crouch instead.
    [Range(0f, 30f)]
    [SerializeField] private float slideMinSpeed = 6.7f;

    // How strongly A/D nudge our direction while sliding. Way weaker than groundAccel - this is a slow drift, not steering like regular ground movement. W/S do nothing at all while sliding.
    [SerializeField] private float slideSteerAccel = 10f;

    // Multiplier on gravity pulling us DOWN a slope while sliding. 1 = plain g * sin(angle).
    [SerializeField] private float slideSlopeGravityScale = 1f;

    // Same, but for slowing us down going UP a slope. Lower than slideSlopeGravityScale on purpose:
    // full gravity uphill kills speed just as hard as it builds it downhill, which eats the exact momentum you need to carry over a lip and launch. Feel over realism.
    // But too low and two ramps facing each other become a perpetual motion machine: going down gains more than going up loses, so every pass through the valley is faster than the last.
    // Rule of thumb: keep this at about 0.77 or more while slidingGroundFriction is 4 and gravity is 40, and a valley can never gain speed on any walkable slope.
    [Range(0f, 1f)]
    [SerializeField] private float slideUphillGravityScale = 0.8f;

    // One shared timer, counted from the start of a slide:
    // no new slide can start before it runs out, and a jump inside it is a slide jump.
    [SerializeField] private float slideGrace = 0.5f;

    // Extra horizontal speed a slide jump gives.
    [SerializeField] private float slideJumpBoost = 8f;

    // Landing on the ground while sliding in the air turns some of the fall speed into forward speed.
    // This is how much of it we keep: 1 = all of it (the old, very strong boost), 0 = none (a plain landing).
    [Range(0f, 1f)]
    [SerializeField] private float slideLandingBoostScale = 0.35f;

    // Hard cap (m/s) on that landing boost, so a very tall drop can't launch you.
    [SerializeField] private float slideLandingBoostMax = 12f;

    [Header("Wall Jump")]
    // Upward speed of a wall jump, as a fraction of jumpVelocity. 0.66 = two thirds of a normal jump.
    [Range(0f, 1.5f)]
    [SerializeField] private float wallJumpHeightMultiplier = 0.66f;

    // Flat speed added straight away from the wall (along the wall normal).
    [SerializeField] private float wallJumpPushAway = 10f;

    // Flat speed added along the wall, in the direction we're facing.
    // Defaults to the same value as slideJumpBoost.
    [SerializeField] private float wallJumpForwardBoost = 8f;

    // How far off "looking parallel to the wall" we can be and still wall jump. Applies to BOTH directions along the wall, so 30 gives a 60 degree cone around each one.
    [Range(0f, 90f)]
    [SerializeField] private float wallJumpLookTolerance = 30f;

    // How close (meters, from the capsule surface, on top of skinWidth) a wall must be to count.
    [SerializeField] private float wallDetectDistance = 0.15f;

    // How many horizontal directions we probe around the capsule. More = catches walls at odd angles better.
    [Range(4, 16)]
    [SerializeField] private int wallProbeDirections = 8;

    // A surface only counts as a wall if abs(normal.y) is at most this. 0 = perfectly vertical.
    [Range(0f, 1f)]
    [SerializeField] private float wallMaxNormalY = 0.3f;

    [Header("Wall Slip")]
    // The "slipping speed": while touching a wall in the air, gravity is multiplied by this. 0.5 = half gravity, 1 = walls change nothing, 0 = glued to the wall.
    // It is a multiplier (not an m/s^2 number) so it keeps following gravity if you retune that. It is an acceleration, not a top speed: we still speed up as we slip, just half as fast.
    // Applies to ANY wall touch, no look angle needed. The moment we are off the wall, gravity is back to normal.
    [Range(0f, 1f)]
    [SerializeField] private float wallSlipGravityScale = 0.5f;

    [Header("Wall Run")]
    // Flat speed (m/s) needed to start AND keep a wall run, on top of holding sprint and meeting every wall jump requirement.
    [SerializeField] private float wallRunMinSpeed = 20f;

    // Seconds of wall running allowed before Y unpauses and we slip down the wall as normal.
    // It is a budget, not a per-wall timer: it only refills on a wall jump or on touching the ground.
    [SerializeField] private float wallRunGracePeriod = 1.5f;

    [Header("Crouch")]
    // What pressing slide does instead, if we're too slow to slide (or not moving at all).
    [SerializeField] private float crouchSpeed = 5f;

    // Capsule height while crouching. Standing height is read from the CapsuleCollider itself at Awake.
    [SerializeField] private float crouchHeight = 1.2f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;

    // Small gap maintained between capsule and surfaces.
    // Helps avoid floating point collision jitter.
    [SerializeField] private float skinWidth = 0.03f;

    [SerializeField] private float maxSlopeAngle = 60f;
    [SerializeField] private float stepHeight = 0.4f;

    // How far we try to "stick" downward while already grounded.
    [SerializeField] private float groundSnapDistance = 0.3f;

    // Small landing detection distance while airborne.
    [SerializeField] private float groundProbeDistance = 0.05f;

    // Uphill slopes above this angle can launch us when we leave them.
    [SerializeField] private float launchSlopeAngle = 10f;

    // Maximum collision-and-slide attempts in one FixedUpdate.
    [SerializeField] private int maxBumps = 4;

    [Header("Pushing Rigidbodies")]
    [SerializeField] private float playerMass = 10f;
    [SerializeField] private float pushStrength = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugHUD = true;
    [SerializeField] private bool drawDebugRays = true;

    // Value for lastGroundedTime meaning "no coyote jump available". Far enough in the past to never match.
    private const float NeverGrounded = -999f;

    // Anything shorter than this counts as "not moving".
    private const float MinMoveDistance = 0.0001f;

    // If the Rigidbody is further than this from where we put it, something else moved us.
    // Squared distance, so 0.0004 means 0.02 meters.
    private const float ExternalMoveThresholdSqr = 0.0004f;

    // Upward speed (m/s) above which leaving a steep slope launches us.
    private const float MinLaunchUpSpeed = 0.05f;

    // Speed (m/s) away from a surface above which we count as leaving it.
    private const float MovingAwaySpeed = 0.1f;

    // Slack (m/s) on wallRunMinSpeed, so a speed that is "exactly 20" but 19.99999 after float math still counts.
    private const float WallRunSpeedTolerance = 0.05f;

    // Step-up tuning.
    private const float MinStepRaiseHeight = 0.02f;     // less headroom than this: can't step
    private const float StepForwardNudge = 0.05f;       // always try to move at least this far forward
    private const float MinStepForwardDistance = 0.01f; // no room even when raised: it's a real wall
    private const float MinStepHeightGained = 0.01f;    // shorter than this isn't a step
    private const float StepHeightTolerance = 0.01f;    // slack on stepHeight

    // Not worth moving us down for less than this when snapping to the ground.
    private const float MinSnapDistance = 0.002f;

    // How upright a platform must be (dot with world up) to turn us with it.
    private const float PlatformUprightDot = 0.7f;

    // ResolveGroundNormal starts a short ray this far above the contact and casts this far down.
    private const float NormalProbeHeight = 0.1f;
    private const float NormalProbeDistance = 0.2f;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    // Custom authoritative player position.
    // This is the position our controller actually reasons about.
    private Vector3 position;

    // Absolute world-space velocity.
    private Vector3 velocity;

    // Last position we told the Rigidbody to move toward.
    // Used to detect external movement / teleports.
    private Vector3 lastTarget;

    // Velocity inherited from moving platforms.
    private Vector3 currentPlatformVelocity;

    private Vector2 moveInput;
    private bool sprinting;
    private bool jumpQueued;

    // Flat speed we had when we left the ground. Air control can't push us past it.
    private float airSpeedCap;

    // Slide state.
    private bool sliding;
    private bool slideQueued;
    private bool slideHeld;
    private float slideStartTime = -999f;

    // Crouch state. Crouching and sliding share the same button; only one is ever true at once.
    private bool crouching;

    // Standing capsule size, read once at Awake so crouching can restore it exactly.
    private float standHeight;
    private float standCenterY;

    // Everything we know about the wall we're next to. Single source of truth: wall jump, wall run, wall slip gravity and the debug rays all read it.
    // Detection and the look check live ONLY in the WALL DETECTION section, filled by UpdateWallContact().
    // A plain struct (small value type), so copying it to re-check the look doesn't touch the stored one.
    private struct WallContact
    {
        public bool valid;
        public Vector3 normal;          // flat, points away from the wall
        public Vector3 point;
        public Collider collider;
        public Vector3 tangent;         // along the wall (the -tangent is the other way)
        public Vector3 forwardTangent;  // whichever of +/- tangent we're facing more
        public float lookAngle;         // degrees between our flat look and forwardTangent
        public bool lookOk;             // lookAngle within wallJumpLookTolerance
    }

    private WallContact currentWall;

    // Wall run state. wallRunning is decided once per step by UpdateWallRun(); wallRunTimeUsed is the grace period spent so far.
    private bool wallRunning;
    private float wallRunTimeUsed;

    private bool grounded;
    private Vector3 groundNormal = Vector3.up;
    private Collider groundCollider;
    private float slopeAngle;
    private float landTime = -999f;

    // The last time we ended a physics step standing on something. Coyote time counts from here.
    // -999 means "we are not allowed a coyote jump right now" (same trick as slideStartTime).
    private float lastGroundedTime = NeverGrounded;

    // Minimum Y component a surface normal needs to count as walkable.
    private float minGroundNormalY;

    // Moving platform tracking.
    private Transform platform;
    private Vector3 platformLocalPos;
    private Quaternion platformLastRot;

    // Reused buffers avoid allocating straight garbage every physics frame.
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly Collider[] overlapBuffer = new Collider[16];

    // Track collision contacts so events fire once on contact.
    private readonly HashSet<Collider> contactsThisStep = new HashSet<Collider>();
    private readonly HashSet<Collider> contactsLastStep = new HashSet<Collider>();

    public Vector3 Velocity => velocity;
    public bool IsGrounded => grounded;
    public bool IsSliding => sliding;
    public bool IsCrouching => crouching;
    public bool IsWallRunning => wallRunning;

    // Flat speed relative to whatever we're standing on, so riding a fast platform doesn't count as running.
    public float HorizontalSpeed => Flatten(velocity - currentPlatformVelocity).magnitude;

    // ============================================================
    // UNITY MESSAGES
    // ============================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        SetupRigidbody();

        position = transform.position;
        lastTarget = position;

        standHeight = capsule.height;
        standCenterY = capsule.center.y;

        CalculateMinGroundNormal();
    }

    private void OnValidate()
    {
        // Recalculate immediately when changing slope angle in Inspector.
        CalculateMinGroundNormal();
    }

    private void Update()
    {
        ReadInput();
    }

    // Everything the controller does each physics step, in order so just read this function top to bottom to see the whole controller.
    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        AcceptExternalMovement();

        // First inherit motion from whatever platform we stand on.
        // (currentPlatformVelocity still holds LAST step's value until the line below overwrites it, so grab it first: LaunchOffMovingGround needs to know how much speed the platform lost.)
        Vector3 previousPlatformVelocity = currentPlatformVelocity;
        currentPlatformVelocity = ApplyPlatformCarry(dt);

        // Fix any overlap caused by moving platforms, teleports, etc.
        Depenetrate();

        // Movement logic should happen RELATIVE to the platform.
        // playerVelocity = relativeVelocity + platformVelocity
        // Therefore
        // relativeVelocity = playerVelocity - platformVelocity
        Vector3 calculatedMovement = velocity - currentPlatformVelocity;

        bool startedGrounded = grounded;

        // Remember our takeoff speed every step we spend on the ground.
        // Whenever we leave it (jump, ledge, launch), the last value stays as the air cap.
        // Walk speed is the floor so a standing jump can still reach walking speed.
        if (startedGrounded)
            airSpeedCap = Mathf.Max(walkSpeed, Flatten(calculatedMovement).magnitude);

        // Decide slide/crouch BEFORE jumping, so pressing slide and jump on the same frame still counts as a slide jump instead of missing it by one frame.
        UpdateSlideAndCrouch(ref calculatedMovement, dt);

        // Change velocity.
        // Leaving the ground can happen two ways: we jump, or the ground launches us (like if you fly off a platform or launchpad) (falling off a platform also counts)
        bool leftGround =
            Jump(ref calculatedMovement) || LaunchOffMovingGround(previousPlatformVelocity - currentPlatformVelocity, dt);

        // Wall run is decided AFTER the jump, so a wall jump this step (which refills the grace period and pushes us off the wall) is already counted.
        UpdateWallRun(calculatedMovement, dt);

        calculatedMovement = Move(calculatedMovement, dt);

        // Change position.
        // displacement = velocity * time
        // x_new = x_old + v * dt
        MoveAndSlide(calculatedMovement * dt, ref calculatedMovement, startedGrounded && !leftGround);

        // 3. then work out where we ended up.
        UpdateGrounded(startedGrounded, leftGround, calculatedMovement);

        // make sure we remember any wall we're next to, for the next step's wall jump (and the debug rays).
        UpdateWallContact();

        // convert relative velocity back into world velocity.
        velocity = calculatedMovement + currentPlatformVelocity;

        // Tell the surrounding  world about it, and remember things for next step.
        DispatchContacts();
        RecordPlatformAnchor();
        CommitPosition();
        DrawDebugRays();
    }

    private void OnGUI()
    {
        DrawDebugHUD();
    }

    // ============================================================
    // SETUP HELPERS
    // ============================================================

    private void SetupRigidbody()
    {
        // Rigidbody exists as a Unity physics shell.
        // but the code remains responsible for actual movement.

        // better to set these here so we dont untick them ingame yknow?
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void CalculateMinGroundNormal()
    {
        // Math Time!
        // Surface is walkable when:
        //
        // normal.y >= cos(maxSlopeAngle)
        //
        // Example:
        // maxSlopeAngle = 60 degrees
        // cos(60) = 0.5
        //
        // So any surface normal with y >= 0.5 is <= 60 degrees steep.
        minGroundNormalY = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
    }

    private void AcceptExternalMovement()
    {
        // If another system moved the Rigidbody unexpectedly, accept that new position instead of snapping back.
        // Helpful trigger for doors shoving you or platforms moving you!
        if ((rb.position - lastTarget).sqrMagnitude > ExternalMoveThresholdSqr)
            position = rb.position;
    }

    private void CommitPosition()
    {
        lastTarget = position;

        // Let Rigidbody interpolation visually smooth the custom position.
        rb.MovePosition(position);
    }

    // ============================================================
    // INPUT
    // ============================================================

    private void ReadInput()
    {
        // Basic Movement
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 input = Vector2.zero;

        if (keyboard.upArrowKey.isPressed || (allowWASD && keyboard.wKey.isPressed))
            input.y += 1f;

        if (keyboard.downArrowKey.isPressed || (allowWASD && keyboard.sKey.isPressed))
            input.y -= 1f;

        if (keyboard.rightArrowKey.isPressed || (allowWASD && keyboard.dKey.isPressed))
            input.x += 1f;

        if (keyboard.leftArrowKey.isPressed || (allowWASD && keyboard.aKey.isPressed))
            input.x -= 1f;

        // So there was a funny bug at the start where diagonal movement was reasonably twice as fast as usual movement
        // Prevent diagonal input from being faster than straight input.
        //
        // Without normalize:
        // (1,1) magnitude = sqrt(2) ~= 1.414
        //
        // With normalize:
        // magnitude = 1
        moveInput = input.normalized;
        // Normalizing the direction keeps speed precise!
        sprinting = keyboard.leftShiftKey.isPressed;

        // Queue jump here so FixedUpdate cannot miss a single-frame key press.
        if (keyboard.spaceKey.wasPressedThisFrame)
            jumpQueued = true;

        // Slide is on either thumb button of the mouse. Same queue trick as jump for the press, and a plain "is it down" for holding the slide.
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        slideHeld = mouse.backButton.isPressed || mouse.forwardButton.isPressed;

        if (mouse.backButton.wasPressedThisFrame || mouse.forwardButton.wasPressedThisFrame)
            slideQueued = true;
    }

    // ============================================================
    // VELOCITY STEPS: JUMP / SLIDE / CROUCH / MOVE
    // ============================================================

    // All hail the jump button lol.
    // Returns true if we jumped OFF THE GROUND (which means we left the ground this step). A wall jump happens in the air, not a jump like this
    
    // Which jump we get, in priority order:
    //   1. Ground jump  - standing on something, OR still inside the coyote window after walking off.
    //   2. Wall jump    - airborne, next to a wall, looking roughly along it.
    //   3. Nothing      - pressing jump in mid-air with no wall does nothing.
    //
    // Each kind of jump is its own method, so future ones (wall run jump, etc.) get their own method and one more branch here instead of growing one enormous conditional.
    private bool Jump(ref Vector3 calculatedMovement)
    {
        bool jumpPressed = jumpQueued;

        // The queued press is used up either way, so pressing Space in mid-air does not secretly "buffer" a jump for later.
        jumpQueued = false;

        if (!jumpPressed)
            return false;

        // Ground first, so near a ledge AND a wall you get the stronger ground jump.
        if (grounded || InCoyoteWindow())
            return PerformGroundJump(ref calculatedMovement);

        // Wall jumps are airborne already, so they are NOT "leaving the ground":
        // returning false keeps UpdateGrounded probing for a landing like any other airborne step.
        TryWallJump(ref calculatedMovement);
        return false;
    }

    // Coyote time: true if we were standing on something a split second ago, even though we are airborne now (we walked off an edge without jumping). Lets a slightly-late press still jump.
    // lastGroundedTime is set back to "never" whenever we leave the ground ON PURPOSE (a jump or a launch), so this can only be true after simply walking/falling off something.
    // Without that jumping would leave the window open and you could jump a second time in mid-air.
    private bool InCoyoteWindow()
    {
        return Time.time - lastGroundedTime <= coyoteTime;
    }

    // Normal jump, plus the slide jump boost. Always succeeds, so always returns true.
    // Also what a coyote jump does: it is exactly the same jump, we just allow it a bit late.
    private bool PerformGroundJump(ref Vector3 calculatedMovement)
    {
        // Jump is simply an immediate upward velocity assignment.
        // (In a coyote jump we may have already started falling - this overwrites that fall.)
        calculatedMovement.y = jumpVelocity;

        // Pseudocode:
        // - Only if `sliding` is true AND we're still within slideGrace seconds of when the slide started (compare Time.time - slideStartTime against slideGrace):
        //     - Add slideJumpBoost of speed to calculatedMovement, along the flattened
        //       direction we're already moving (same trick as the slide's entry boost).
        //     - Also raise airSpeedCap to match the new speed - it was captured BEFORE this
        //       function ran this step, so without this the boost gets clamped away instantly.
        // - Otherwise: nothing extra, this is just a plain jump.
        if (sliding && (Time.time - slideStartTime) < slideGrace)
        {
            Vector3 flatDirection = Flatten(calculatedMovement).normalized;

            calculatedMovement.x += flatDirection.x * slideJumpBoost;
            calculatedMovement.z += flatDirection.z * slideJumpBoost;

            // Air Speed Change, Remove this in the future for design tweaking?
            airSpeedCap = Flatten(calculatedMovement).magnitude;
        }

        grounded = false;

        // We left on purpose, so close the coyote window: no second jump in mid-air.
        lastGroundedTime = NeverGrounded;

        return true;
    }

    // Wall jump: needs an airborne player next to a wall they're looking roughly along.
    // Returns true if the jump happened.
    //
    // Flow (same order as the spec's flowchart):
    //   grounded?            -> no wall jump (the ground jump handles it)
    //   wall in range?       -> currentWall.valid, found by UpdateWallContact() last step
    //   looking along it?    -> within wallJumpLookTolerance of either direction along the wall
    //   all yes              -> ApplyWallJump
    private bool TryWallJump(ref Vector3 calculatedMovement)
    {
        if (!TryGetWallJumpWall(out WallContact wall))
            return false;

        ApplyWallJump(wall, ref calculatedMovement);
        return true;
    }

    // The wall jump requirements: airborne, a wall in range, looking along it. Wall running needs exactly these too, so both ask here and can't drift apart.
    // Hands back the wall with the look re-checked against where we face right now.
    private bool TryGetWallJumpWall(out WallContact wall)
    {
        wall = currentWall;

        if (grounded || !currentWall.valid)
            return false;

        // currentWall was measured at the end of the last physics step, but the mouse can turn us between steps. Copy it and re-check just the look part against where we face right now.
        EvaluateWallLook(ref wall);

        return wall.lookOk;
    }

    // The wall jump's velocity change, three parts:
    //   1. UP:         jumpVelocity * wallJumpHeightMultiplier (default 66% of a normal jump).
    //   2. AWAY:       wallJumpPushAway along the wall normal.
    //   3. FORWARD:    wallJumpForwardBoost along the wall, in the direction we're facing.
    //
    // The flat parts are ADDED to the velocity we already have (not replaced), exactly like the slide jump boost.
    // Deterministic: the result only depends on current velocity + the wall.
    private void ApplyWallJump(WallContact wall, ref Vector3 calculatedMovement)
    {
        // Like any jump, upward speed is assigned, not added - so a wall jump always gives the same height no matter how fast we were falling.
        calculatedMovement.y = jumpVelocity * wallJumpHeightMultiplier;

        // Both vectors are flat (normal and forwardTangent have no height), so this is purely sideways.
        Vector3 boost =
            wall.normal * wallJumpPushAway +
            wall.forwardTangent * wallJumpForwardBoost;

        calculatedMovement.x += boost.x;
        calculatedMovement.z += boost.z;

        // Same reason as the slide jump: airSpeedCap was captured before this function ran, and AirMove would clamp the boost away on the very next line. Max() so it can never LOWER the cap.
        airSpeedCap = Mathf.Max(airSpeedCap, Flatten(calculatedMovement).magnitude);

        // A wall jump ends an air slide (SlideAirMove would lock our momentum and ignore air control).
        // Pressing slide again starts a new one as normal - air slides ignore slideGrace.
        sliding = false;

        // A wall jump refills the wall run grace period.
        wallRunTimeUsed = 0f;
        wallRunning = false;

        // We just pushed off this wall, so it must not count as touching it this step (no slip gravity, no wall run).
        // UpdateWallContact measures again at the end of the step.
        currentWall = default;
    }

    // A platform that stops (or slows down) faster than gravity can pull us back down leaves us flying with the speed it had.
    // Same idea as the old Rigidbody version launching you at the apex.
    //
    // Returns true if we launched (which means we left the ground this step).
    private bool LaunchOffMovingGround(Vector3 platformVelocityLost, float dt)
    {
        if (!grounded)
            return false;

        // How fast the ground just moved AWAY from us.
        //
        // When a platform stops, the speed it had shows up in our platform-relative velocity (velocity - platformVelocity), pointing away from it. platformVelocityLost (last step's platform velocity minus this step's) is exactly that amount.
        //
        // We deliberately do NOT use our own velocity here: walking off a ledge makes the capsule touch the edge, which gives a tilted "walkable" ground normal pointing the way we are walking. Our own speed lined up with that normal looked like the ground launching us and wiped the coyote window.
        float speedAwayFromGround = Vector3.Dot(platformVelocityLost, groundNormal);

        // Gravity can only pull us back by gravity * dt each step.
        // Moving away faster than that means the ground let go of us.
        if (speedAwayFromGround > gravity * dt)
        {
            grounded = false;

            // Launched on purpose, not walked off: no coyote jump (same reason as PerformGroundJump).
            lastGroundedTime = NeverGrounded;
            return true;
        }

        return false;
    }

    // Decides whether we're sliding or crouching this step.
    // Pressing the button chooses between them based on how fast we're going; releasing it always ends whichever one we're in.
    // (The actual slide/crouch movement math lives in SlideGroundMove/CrouchGroundMove below.)
    private void UpdateSlideAndCrouch(ref Vector3 calculatedMovement, float dt)
    {
        bool pressed = slideQueued;

        // Used up either way, same as the jump press.
        slideQueued = false;

        float flatSpeed = calculatedMovement.magnitude;

        if (pressed && !sliding && !crouching)
        {
            // Pseudocode:
            // - If flatSpeed is at least slideMinSpeed (m/s, same units as walkSpeed/sprintSpeed)
            //   AND enough time has passed since the LAST slide started
            //   (compare Time.time - slideStartTime against slideGrace):
            if ((flatSpeed >= slideMinSpeed && (Time.time - slideStartTime) >= slideGrace) || !grounded)
            {
                sliding = true;
                slideStartTime = Time.time;
                // boost
                if (flatSpeed < slideBoostMaxSpeed)
                {
                    Vector3 flatDirection = Flatten(calculatedMovement).normalized;

                    calculatedMovement.x += flatDirection.x * slideBoost;
                    calculatedMovement.z += flatDirection.z * slideBoost;
                }
            }
            
            else {
                if (grounded)
                    crouching = true;
            }
            //
            //     - Start a SLIDE: turn `sliding` on, set `slideStartTime = Time.time`.
            //     - Give the entry boost: if flatSpeed is below slideBoostMaxSpeed, add `slideBoost`
            //       of speed to calculatedMovement, along the flattened direction we're already moving.
            //     - No grounded check here on purpose - this is allowed in midair too.
            //
            // - Otherwise, if we ARE grounded: start CROUCHING instead (turn `crouching` on).
            //   (Too slow AND airborne falls through here too - there's no crouching in midair,
            //   so just do nothing.)
        }

        // Releasing the button always ends whichever state we're in. This is the ONLY way a slide ends now - you can slide to a dead stop on a ramp and keep sliding, backwards, once gravity starts winning.
        if (sliding && !slideHeld)
            sliding = false;

        if (crouching && !slideHeld)
            crouching = false;

        SetCrouchHitbox(crouching);
    }

    // Shrinks or restores the capsule for crouching. Only the TOP moves - the bottom offset from the capsule's center stays fixed, so our feet never shift.
    //
    // Known limitation: no ceiling check when standing back up, so standing under something low can clip you into it for a frame until Depenetrate() sorts it out. Fine for v1.
    private void SetCrouchHitbox(bool wantCrouching)
    {
        float targetHeight = wantCrouching ? crouchHeight : standHeight;

        if (Mathf.Approximately(capsule.height, targetHeight))
            return;

        float bottomOffset = standCenterY - standHeight * 0.5f;

        capsule.height = targetHeight;
        capsule.center = new Vector3(capsule.center.x, targetHeight * 0.5f + bottomOffset, capsule.center.z);
    }

    // Decides whether we are wall running this step. Runs every tick, right before Move().
    //
    // A wall run needs ALL of these:
    //   - every wall jump requirement (airborne, wall in range, looking along it), via TryGetWallJumpWall
    //   - sprint held
    //   - flat speed of at least wallRunMinSpeed
    //   - not already moving away from the wall. Without this, the tick after a wall jump (wall still in range if the push is small) would freeze the jump's upward speed.
    //   - grace period not used up
    //
    // Only time actually spent wall running is counted. The budget refills on landing here, and on a wall jump in ApplyWallJump.
    private void UpdateWallRun(Vector3 calculatedMovement, float dt)
    {
        wallRunning = false;

        if (grounded)
        {
            wallRunTimeUsed = 0f;
            return;
        }

        if (!TryGetWallJumpWall(out WallContact wall))
            return;

        bool fastEnough = Flatten(calculatedMovement).magnitude >= wallRunMinSpeed - WallRunSpeedTolerance;
        bool movingAway = Vector3.Dot(calculatedMovement, wall.normal) > MovingAwaySpeed;

        if (!sprinting || !fastEnough || movingAway || wallRunTimeUsed >= wallRunGracePeriod)
            return;

        wallRunning = true;
        wallRunTimeUsed += dt;
    }

    // Choose movement model based on what state we're in.
    private Vector3 Move(Vector3 calculatedMovement, float dt)
    {
        // Wall running is airborne by definition and overrides the other air models (an air slide included).
        if (wallRunning)
            return WallRunMove(calculatedMovement);

        if (sliding)
            return grounded ? SlideGroundMove(calculatedMovement, dt) : SlideAirMove(calculatedMovement, dt);

        if (grounded)
            return crouching ? CrouchGroundMove(calculatedMovement, dt) : GroundMove(calculatedMovement, dt);

        return AirMove(calculatedMovement, dt);
    }

    // ============================================================
    // The Big MOVEMENT MODELS
    // ============================================================

    private Vector3 WishDirection()
    {
        // Movement relative to where the player is facing.
        Vector3 forward = Flatten(transform.forward).normalized; //gotta do this shit every time man
        Vector3 right = Flatten(transform.right).normalized;

        // WASD becomes a world-space direction.
        return forward * moveInput.y + right * moveInput.x;
    }

    // Same vector with the height removed.
    private static Vector3 Flatten(Vector3 vector)
    {
        return new Vector3(vector.x, 0f, vector.z);
    }

    private Vector3 GroundMove(Vector3 calculatedMovement, float dt)
    {
        // target speed depends on if im sprinting or not
        float targetSpeed = walkSpeed;

        if (sprinting)
            targetSpeed = sprintSpeed;

        return GroundMoveTowards(calculatedMovement, dt, targetSpeed);
    }

    private Vector3 CrouchGroundMove(Vector3 calculatedMovement, float dt)
    {
        // Crouching always moves at crouchSpeed - sprint does nothing while crouched.
        return GroundMoveTowards(calculatedMovement, dt, crouchSpeed);
    }

    // Shared ground-acceleration model: approach targetSpeed along our wish direction, or apply friction when there's no input. Walking and crouch-walking only differ by targetSpeed.
    private Vector3 GroundMoveTowards(Vector3 calculatedMovement, float dt, float targetSpeed)
    {
        Vector3 wish = WishDirection();

        bool hasInput = wish.sqrMagnitude > 0.0001f;
        bool inLandingGrace = Time.time - landTime < landingGrace;

        // Remove velocity pointing directly into/out of the ground.

        // Projection onto a plane:
        // v_plane = v - n * dot(v, n)
        // n = ground surface normal
        // This leaves only velocity parallel to the ground.
        Vector3 velocityAlongGround = Vector3.ProjectOnPlane(calculatedMovement, groundNormal); //Take a look at week 3 exp 9! Super helpful dot product funtion

        if (hasInput)
        {
            // Arguably the coolest and smoothest model I have here!
            // Project desired movement onto the slope.
            //
            // This means "forward" becomes "forward along the ramp."
            Vector3 directionAlongGround = Vector3.ProjectOnPlane(wish, groundNormal).normalized;

            Vector3 targetVelocity = directionAlongGround * targetSpeed;

            // If above target speed, slow gently.
            // Otherwise accelerate aggressively.
            float acceleration = groundAccel;

            if (velocityAlongGround.magnitude > targetSpeed)
                acceleration = groundOverspeedDecel;

            // MoveTowards is effectively bounded acceleration:
            // Its a unity function as well.
            // deltaVelocity <= acceleration * dt
            //
            // v_new approaches targetVelocity gradually.
            velocityAlongGround = Vector3.MoveTowards(velocityAlongGround, targetVelocity, acceleration * dt);
        }
        else if (!inLandingGrace)
        {
            // Friction is implemented as constant deceleration toward zero.
            //
            // v_new = MoveTowards(v, 0, friction * dt)
            velocityAlongGround = Vector3.MoveTowards(velocityAlongGround, Vector3.zero, groundFriction * dt);
        }

        return velocityAlongGround;
    }

    // Sliding overrides normal ground movement entirely: W/S do nothing, A/D only steer weakly, and a slope's own gravity component speeds you up or slows you down.
    private Vector3 SlideGroundMove(Vector3 calculatedMovement, float dt)
    {
        Vector3 velocityAlongGround = Vector3.ProjectOnPlane(calculatedMovement, groundNormal);

        // Steer left/right while sliding: only moveInput.x matters, no forward/back from W/S.
        // Added straight into velocityAlongGround like air control - a nudge over time, not a snap to a target speed.
        float speedBeforeSteer = velocityAlongGround.magnitude;

        Vector3 right = Flatten(transform.right).normalized;
        velocityAlongGround += right * moveInput.x * slideSteerAccel * dt;

        // Steering can only turn us, never speed us up - same idea as AirMove's airSpeedCap.
        // Capping against speedBeforeSteer (not some fixed number) means this can't be used to climb from a dead stop back up to speed either - 0 can only clamp back down to 0.
        if (velocityAlongGround.magnitude > speedBeforeSteer)
            velocityAlongGround = velocityAlongGround.normalized * speedBeforeSteer;


        // Pseudocode:
        // - Find "downhill": project Vector3.down onto the ground plane (Vector3.ProjectOnPlane)
        //   and normalize it. This points down the slope we're standing on.
        // - Add to velocityAlongGround: downhill * gravity * sin(slopeAngle in RADIANS) * slideSlopeGravityScale * dt.
        //   (slopeAngle is in degrees - Mathf.Sin wants radians, so multiply by Mathf.Deg2Rad first.)
        // - Flat floor (slopeAngle = 0): sin(0) = 0, so this adds nothing.
        // - Sliding DOWN a slope: downhill points the way you're already going, so this speeds you up.
        // - Sliding UP a slope (say, into one from flat ground): downhill points backward, so this
        //   slows you down, and can eventually push you back down it.

        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

        // Moving against downhill means we're climbing - that case gets its own, gentler scale (see slideUphillGravityScale) instead of reusing the downhill one for both directions.
        bool movingUphill = Vector3.Dot(velocityAlongGround, downhill) < 0f;
        float slopeScale = movingUphill ? slideUphillGravityScale : slideSlopeGravityScale;

        velocityAlongGround += downhill * gravity * Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * slopeScale * dt;

        // Slide friction always applies, on top of everything above - this is what finally brings a slide to a stop (very slowly, since slidingGroundFriction is low).
        velocityAlongGround = Vector3.MoveTowards(velocityAlongGround, Vector3.zero, slidingGroundFriction * dt);

        return velocityAlongGround;
    }

    private Vector3 AirMove(Vector3 calculatedMovement, float dt)
    {
        // Air acceleration:
        //
        // deltaVelocity = acceleration * dt
        //
        // v_new = v_old + a * dt
        float speedBeforeAccel = Flatten(calculatedMovement).magnitude;

        calculatedMovement += WishDirection() * airControl * dt;

        Vector3 horizontal = Flatten(calculatedMovement);

        // Being midair should never speed us up.
        // Air control can turn and brake us, but not push us past the speed we took off with.
        // (If we are already faster than that, say from a launch pad, we just can't get any faster.)
        float airSpeedLimit = Mathf.Max(speedBeforeAccel, airSpeedCap);

        if (horizontal.magnitude > airSpeedLimit)
        {
            horizontal = horizontal.normalized * airSpeedLimit;

            calculatedMovement.x = horizontal.x;
            calculatedMovement.z = horizontal.z;
        }

        // Over the limit: slow down gradually toward maxAirSpeed, keeping direction.
        // MoveTowards never goes past maxAirSpeed, so this can't undershoot.
        if (horizontal.magnitude > maxAirSpeed)
        {
            float slowedSpeed = Mathf.MoveTowards(horizontal.magnitude, maxAirSpeed, airOverspeedDecel * dt);

            horizontal = horizontal.normalized * slowedSpeed;

            calculatedMovement.x = horizontal.x;
            calculatedMovement.z = horizontal.z;
        }

        // Gravity:
        // Lets go back to phys 1 lol
        // v = v0 + g * dt
        //
        // Downward direction makes acceleration negative in Y.
        calculatedMovement += Vector3.down * CurrentGravity() * dt;
        // Gotta be careful to not clamp gravity here!

        return calculatedMovement;
    }

    // Sliding in midair means momentum is locked in: no steering, no speed caps, just gravity.
    // (You get here by jumping out of a slide, or sliding off a ledge, with the button still held.)
    private Vector3 SlideAirMove(Vector3 calculatedMovement, float dt)
    {
        calculatedMovement += Vector3.down * CurrentGravity() * dt;
        return calculatedMovement;
    }

    // Wall run: Y is paused (no rising, no falling) and EVERYTHING else is left alone. No air control, no speed cap, no overspeed bleed, so forward momentum is not hindered at all.
    // Speeds above wallRunMinSpeed (slide jump, wall jump) carry through the whole run.
    private Vector3 WallRunMove(Vector3 calculatedMovement)
    {
        calculatedMovement.y = 0f;
        return calculatedMovement;
    }

    // Gravity for the air models. Touching a wall swaps it for the lower wall slip gravity; one step after we leave the wall it is normal again.
    // currentWall is only ever valid while airborne (UpdateWallContact clears it on the ground), and so is every caller, so this never touches ground movement.
    // When a wall run's grace period runs out, AirMove lands here with Y at 0: that is the "slip down the wall as normal".
    private float CurrentGravity()
    {
        return currentWall.valid ? gravity * wallSlipGravityScale : gravity;
    }

    // ============================================================
    // COLLISION QUERIES
    // ============================================================

    private void GetCapsulePoints(Vector3 pos, out Vector3 p0, out Vector3 p1, out float radius)
    {
        Vector3 scale = transform.lossyScale;

        // Capsule radius must account for object scaling.
        radius = capsule.radius * Mathf.Max(scale.x, scale.z);

        float height = Mathf.Max(capsule.height * scale.y, radius * 2f);

        Vector3 center = pos + Vector3.Scale(capsule.center, scale);

        // Distance from capsule center to each sphere center.
        float half = height * 0.5f - radius;

        p0 = center + Vector3.down * half;
        p1 = center + Vector3.up * half;
    }

    private bool IsSelf(Collider otherCollider)
    {
        // Ignore our own collider and child colliders.
        return otherCollider == null ||
               otherCollider == capsule ||
               otherCollider.transform.IsChildOf(transform);
    }

    // A surface is walkable when it is tilted less than maxSlopeAngle from straight up.
    private bool IsWalkable(Vector3 surfaceNormal)
    {
        return surfaceNormal.y >= minGroundNormalY;
    }

    private bool SweepCapsule(Vector3 pos, Vector3 direction, float distance, out RaycastHit bestHit)
    {
        bestHit = default;

        if (distance <= 0f)
            return false;

        GetCapsulePoints(pos, out Vector3 p0, out Vector3 p1, out float radius);

        // Sweep our capsule through space.
        // Think "what would we hit if the player moved this direction?"
        int hitCount = Physics.CapsuleCastNonAlloc(
            p0,
            p1,
            radius,
            direction,
            hitBuffer,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        bool foundHit = false;
        float closestDistance = float.MaxValue;

        // CapsuleCastNonAlloc is not guaranteed to return sorted hits, so manually find the nearest collision.
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = hitBuffer[i];

            if (IsSelf(candidate.collider))
                continue;

            // Weird zero-distance hits usually mean we already overlap.
            // Depenetrate() handles those separately.
            if (candidate.distance <= 0f && candidate.point == Vector3.zero)
                continue;

            if (candidate.distance < closestDistance)
            {
                bestHit = candidate;
                closestDistance = candidate.distance;
                foundHit = true;
            }
        }

        return foundHit;
    }

    // Capsule edges can produce misleading normals near corners.
    // This attempts to retrieve the actual face normal instead.
    private Vector3 ResolveGroundNormal(RaycastHit hit)
    {
        // Already walkable: trust the capsule result.
        if (IsWalkable(hit.normal))
            return hit.normal;

        // Ray straight downward from slightly above the contact.
        Vector3 rayOrigin = hit.point + Vector3.up * NormalProbeHeight;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit rayHit,
                NormalProbeDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore)
            && rayHit.collider == hit.collider)
        {
            return rayHit.normal;
        }

        return hit.normal;
    }

    private void Depenetrate()
    {
        // Repeat a few times because resolving one overlap can sometimes push us into another surface.
        for (int iteration = 0; iteration < 3; iteration++)
        {
            GetCapsulePoints(position, out Vector3 p0, out Vector3 p1, out float radius);

            int overlapCount = Physics.OverlapCapsuleNonAlloc(
                p0,
                p1,
                radius,
                overlapBuffer,
                collisionMask,
                QueryTriggerInteraction.Ignore
            );

            bool moved = false;

            for (int i = 0; i < overlapCount; i++)
            {
                Collider otherCollider = overlapBuffer[i];

                if (IsSelf(otherCollider))
                    continue;

                // ComputePenetration gives:
                //
                // direction = shortest escape direction
                // distance  = how far we must move to exit overlap
                if (Physics.ComputePenetration(
                        capsule,
                        position,
                        transform.rotation,
                        otherCollider,
                        otherCollider.transform.position,
                        otherCollider.transform.rotation,
                        out Vector3 escapeDirection,
                        out float escapeDistance))
                {
                    // Push ourselves barely outside the collider.
                    position += escapeDirection * (escapeDistance + 0.001f);

                    moved = true;
                }
            }

            if (!moved)
                break;
        }
    }

    // ============================================================
    // COLLIDE AND SLIDE
    // ============================================================

    // Core collide-and-slide algorithm.
    //
    // Basic idea:
    //
    // 1. Try moving.
    // 2. Find first surface hit.
    // 3. Move right up to it.
    // 4. Remove movement INTO the wall.
    // 5. Continue using the remaining sideways motion.
    //
    // Repeat several times for corners / multiple surfaces.
    private void MoveAndSlide(Vector3 motion, ref Vector3 calculatedMovement, bool allowStep)
    {
        Vector3 remainingMotion = motion;

        // null until we have hit a first surface.
        Vector3? previousNormal = null;

        for (int bump = 0; bump < maxBumps; bump++)
        {
            float distance = remainingMotion.magnitude;

            if (distance < MinMoveDistance)
                break;

            Vector3 direction = remainingMotion / distance;

            // Nothing ahead: perform entire remaining movement.
            if (!SweepCapsule(position, direction, distance + skinWidth, out RaycastHit hit))
            {
                position += remainingMotion;
                break;
            }

            contactsThisStep.Add(hit.collider);

            // Move as far as safely possible, keep whatever motion remains.
            remainingMotion = MoveUpToHit(hit, direction, distance);

            Rigidbody hitBody = hit.collider.attachedRigidbody;

            bool dynamicBody =
                hitBody != null &&
                !hitBody.isKinematic;

            if (dynamicBody)
                PushBody(hitBody, hit, direction, calculatedMovement);

            Vector3 surfaceNormal = hit.normal;

            bool walkable = IsWalkable(surfaceNormal);

            // Try stepping over low obstacles instead of sliding into them.
            if (allowStep &&
                !walkable &&
                surfaceNormal.y > -0.1f &&
                !dynamicBody &&
                TryStepUp(ref remainingMotion))
            {
                previousNormal = null;
                continue;
            }

            SlideAlongSurface(surfaceNormal, previousNormal, ref remainingMotion, ref calculatedMovement);

            previousNormal = surfaceNormal;
        }
    }

    // Moves us right up to the surface, leaving a small gap.
    // Returns whatever motion is still left over.
    private Vector3 MoveUpToHit(RaycastHit hit, Vector3 direction, float distance)
    {
        // Move slightly short of the collision  More math time
        
        // dot(direction, normal) tells us how directly we're approaching the surface.
        
        // Head-on collision:
        // dot ~= -1 ish
        
        // Grazing collision:
        // dot ~= 0 ish
        // More grazing -> slightly larger backoff needed.
        float backOffDistance = Mathf.Min(skinWidth / Mathf.Max(0.05f, -Vector3.Dot(direction, hit.normal)), skinWidth * 4f);

        float safeDistance = Mathf.Clamp(hit.distance - backOffDistance, 0f, distance);

        // Move as far as safely possible.
        position += direction * safeDistance;

        // Keep whatever motion remains.
        return direction * (distance - safeDistance);
    }

    // Removes the part of our motion and velocity that points INTO the surface.
    private void SlideAlongSurface(
        Vector3 surfaceNormal,
        Vector3? previousNormal,
        ref Vector3 remainingMotion,
        ref Vector3 calculatedMovement)
    {
        // Surface faces upward but is too steep to walk on. Anything within wallMaxNormalY of vertical counts as a WALL, not a slope, so a tiny float error in a wall's normal (Chisel meshes, cylinder facets) can't kill our upward motion.
        bool steepUpward =
            !IsWalkable(surfaceNormal) &&
            surfaceNormal.y > wallMaxNormalY;

        // Slide equation:
        //
        // remaining_parallel =
        // remaining - normal * dot(remaining, normal)
        //
        // The perpendicular "into wall" component disappears.
        Vector3 slidMotion = Vector3.ProjectOnPlane(remainingMotion, surfaceNormal);

        // Never magically climb walls that are too steep.
        if (steepUpward && slidMotion.y > 0f)
            slidMotion.y = 0f;

        // A floor seaming into a walkable ramp (or ramp into another ramp) registers as TWO hits in one step - the old surface's edge, then the new one - which looks like being wedged in a
        // corner even though it's still just ground changing angle. Real corners (walls, ceilings)
        // should still wedge normally; only skip it here while sliding across walkable-to-walkable
        // ground, so the single-surface redirect below (which preserves speed) handles it instead.
        bool wedgedBetweenWalkableGround =
            sliding &&
            IsWalkable(surfaceNormal) &&
            previousNormal.HasValue &&
            IsWalkable(previousNormal.Value);

        if (previousNormal.HasValue &&
            Vector3.Dot(slidMotion, previousNormal.Value) < -0.0001f &&
            !wedgedBetweenWalkableGround)
        {
            // We hit two surfaces and are effectively wedged.
            SlideAlongCrease(previousNormal.Value, surfaceNormal, remainingMotion, ref slidMotion, ref calculatedMovement);
        }
        else if (Vector3.Dot(calculatedMovement, surfaceNormal) < 0f)
        {
            float speedBeforeRedirect = calculatedMovement.magnitude;

            // If velocity points INTO the wall, remove its wall-normal component.
            calculatedMovement = Vector3.ProjectOnPlane(calculatedMovement, surfaceNormal);

            // Sliding onto a walkable ramp: keep our speed, just redirect it along the new surface.
            // A plain projection bleeds off cos(angle) of our speed on contact (~23% on a 40 degree ramp), which reads as an instant wall hit even though we're still on the ground and still sliding. Normal running doesn't need this - groundAccel re-ramps you back up fast enough to hide it - but a slide has no re-acceleration, so the loss is permanent and obvious. Actual walls (not walkable) still bleed speed exactly as before.
            if (sliding && IsWalkable(surfaceNormal) && calculatedMovement.sqrMagnitude > 0.0001f)
            {
                // After the projection above, calculatedMovement is the plain along-the-surface speed.
                float tangentSpeed = calculatedMovement.magnitude;

                // The speed that was pointing INTO the surface. Keeping it is what stops a ramp transition from bleeding speed.
                float extraSpeed = speedBeforeRedirect - tangentSpeed;

                // grounded still holds last step's value here (Jump and launches clear it, UpdateGrounded runs later), so false means we are landing from the air.
                // Ground to ground keeps all of it. Landing from the air only keeps a capped fraction of the fall speed, otherwise a tall drop turns into a huge horizontal boost.
                if (!grounded)
                    extraSpeed = Mathf.Min(extraSpeed * slideLandingBoostScale, slideLandingBoostMax);

                calculatedMovement = calculatedMovement.normalized * (tangentSpeed + extraSpeed);
            }

            if (steepUpward && calculatedMovement.y > 0f)
                calculatedMovement.y = 0f;
        }

        remainingMotion = slidMotion;
    }

    // Wedged between two surfaces: only keep motion along the line where they meet.
    //
    // Their cross product creates a vector pointing along the line where the two planes intersect.
    //
    // crease = n1 x n2
    private void SlideAlongCrease(
        Vector3 previousNormal,
        Vector3 surfaceNormal,
        Vector3 remainingMotion,
        ref Vector3 slidMotion,
        ref Vector3 calculatedMovement)
    {
        Vector3 crease = Vector3.Cross(previousNormal, surfaceNormal);

        if (crease.sqrMagnitude > 0.000001f)
        {
            crease.Normalize();

            // Only retain motion along the crease.
            slidMotion = crease * Vector3.Dot(remainingMotion, crease);

            calculatedMovement = crease * Vector3.Dot(calculatedMovement, crease);
        }
        else
        {
            // Parallel/opposing planes:
            // nowhere meaningful left to move.
            slidMotion = Vector3.zero;
            calculatedMovement = Vector3.zero;
        }
    }

    private bool TryStepUp(ref Vector3 remainingMotion)
    {
        Vector3 horizontalMotion = Flatten(remainingMotion);

        float horizontalDistance = horizontalMotion.magnitude;

        if (horizontalDistance < MinMoveDistance)
            return false;

        Vector3 horizontalDirection = horizontalMotion / horizontalDistance;

        // Step test is basically:
        //
        // 1. Can I move UP?
        // 2. Can I move FORWARD?
        // 3. Can I move DOWN onto walkable ground?

        // --------------------
        // 1. MOVE UP
        // --------------------

        float raiseHeight = stepHeight;

        if (SweepCapsule(position, Vector3.up, raiseHeight + skinWidth, out RaycastHit ceilingHit))
            raiseHeight = Mathf.Max(ceilingHit.distance - skinWidth, 0f);

        if (raiseHeight < MinStepRaiseHeight)
            return false;

        Vector3 raisedPosition = position + Vector3.up * raiseHeight;

        // --------------------
        // 2. MOVE FORWARD
        // --------------------

        float forwardDistance = Mathf.Max(horizontalDistance, StepForwardNudge);

        if (SweepCapsule(raisedPosition, horizontalDirection, forwardDistance + skinWidth, out RaycastHit wallHit))
            forwardDistance = Mathf.Max(wallHit.distance - skinWidth, 0f);

        if (forwardDistance < MinStepForwardDistance)
            return false;

        Vector3 forwardPosition = raisedPosition + horizontalDirection * forwardDistance;

        // --------------------
        // 3. DROP DOWN
        // --------------------

        if (!SweepCapsule(forwardPosition, Vector3.down, raiseHeight + skinWidth * 3f, out RaycastHit floorHit))
            return false;

        // Must land on a walkable surface.
        if (!IsWalkable(ResolveGroundNormal(floorHit)))
            return false;

        float droppedDistance = Mathf.Max(floorHit.distance - skinWidth, 0f);

        // Actual height of the step:
        //
        // gainedHeight = amountRaised - amountDropped
        float heightGained = raiseHeight - droppedDistance;

        if (heightGained < MinStepHeightGained ||
            heightGained > stepHeight + StepHeightTolerance)
        {
            return false;
        }

        position = forwardPosition + Vector3.down * droppedDistance;

        // Preserve whatever forward motion remains.
        remainingMotion = horizontalDirection * Mathf.Max(horizontalDistance - forwardDistance, 0f);

        return true;
    }

    // ============================================================
    // GROUND DETECTION- RAYCASDTING
    // ============================================================

    private void UpdateGrounded(bool startedGrounded, bool leftGround, Vector3 calculatedMovement)
    {
        bool wasGrounded =
            startedGrounded &&
            !leftGround;

        float previousSlope = slopeAngle;

        // Assume airborne until proven grounded.
        ClearGroundState();

        // Jump explicitly disables grounding this frame.
        if (leftGround)
            return;

        // Ramp launching:
        //
        // If our slope-projected velocity points upward and the old slope was sufficiently steep, we should continue through the air instead of being snapped downward.
        bool launching =
            wasGrounded &&
            calculatedMovement.y > MinLaunchUpSpeed &&
            previousSlope >= launchSlopeAngle;

        bool snapping =
            wasGrounded &&
            !launching;

        // Since the capsule touches angled ground at an offset, skinWidth / normal.y roughly converts the desired perpendicular skin gap into the needed vertical clearance.
        float probeDistance = skinWidth / minGroundNormalY + groundProbeDistance;

        // Only reach far downward when we intend to stick to the ground.
        if (snapping)
            probeDistance += groundSnapDistance;

        if (!SweepCapsule(position, Vector3.down, probeDistance, out RaycastHit groundHit))
            return;

        Vector3 surfaceNormal = ResolveGroundNormal(groundHit);

        // Too steep to stand on.
        if (!IsWalkable(surfaceNormal))
            return;

        // dot(calculatedMovement, surfaceNormal) > 0 means velocity is moving AWAY from the surface along its normal.
        //
        // So don't instantly reground while launching.
        if (!snapping &&
            Vector3.Dot(calculatedMovement, surfaceNormal) > MovingAwaySpeed)
        {
            return;
        }

        // Move downward until maintaining our skin gap.
        float snapDistance = groundHit.distance - skinWidth / Mathf.Max(surfaceNormal.y, 0.5f);

        if (snapDistance > MinSnapDistance)
            position += Vector3.down * snapDistance;

        grounded = true;
        groundNormal = surfaceNormal;
        groundCollider = groundHit.collider;

        // Every step we end on the ground refreshes the coyote window. The moment we stop being grounded this stops updating, so Time.time - lastGroundedTime starts counting up.
        lastGroundedTime = Time.time;

        // Angle between surface normal and straight up.
        //
        // Flat floor:
        // angle(up, up) = 0 degrees
        //
        // Vertical wall:
        // angle(sideways, up) = 90 degrees
        slopeAngle = Vector3.Angle(surfaceNormal, Vector3.up);

        contactsThisStep.Add(groundHit.collider);

        // Record exact landing time for bunny-hop friction grace.
        if (!wasGrounded)
            landTime = Time.time;
    }

    private void ClearGroundState()
    {
        grounded = false;
        groundCollider = null;
        groundNormal = Vector3.up;
        slopeAngle = 0f;
    }

    // ============================================================
    // WALL DETECTION
    // ============================================================

    // Looks for a wall next to us and stores what it finds in currentWall. Runs once at the end of every physics step, so TryWallJump (next step) and the debug rays share the same answer.
    //
    // Why not just use the hits MoveAndSlide already finds? MoveAndSlide only sweeps in the direction we are MOVING. Wall jumps want us moving ALONG the wall, so we would almost never actually "hit" it. Instead we reuse the same SweepCapsule, but aimed sideways around us.
    private void UpdateWallContact()
    {
        // Start empty every step so an old wall can never linger after we move away from it.
        currentWall = default;

        // Walls only matter in the air. A grounded player has the normal ground jump instead.
        if (grounded)
            return;

        if (!TryFindWall(out WallContact wall))
            return;

        // Work out the tangents and whether we're looking along the wall.
        EvaluateWallLook(ref wall);
        currentWall = wall;
    }

    // Sweeps the capsule outward in a ring of horizontal directions and picks the closest real wall.
    //
    // Top-down view, wallProbeDirections = 8 (each arrow is one short capsule sweep):
    //
    //        \  |  /
    //         \ | /
    //      <----(O)---->     O = player capsule
    //         / | \
    //        /  |  \
    //
    // The ring is built from angles, never from world axes, so the wall can face any direction.
    // A wall facing between two probes is still caught: the sweep reaches further than the gap we keep from walls (skinWidth), with a bit of room to spare.
    private bool TryFindWall(out WallContact wall)
    {
        wall = default;

        bool found = false;
        float closestDistance = float.MaxValue;
        RaycastHit closestHit = default;

        // Fewer than 4 probes would leave big blind spots.
        int directions = Mathf.Max(wallProbeDirections, 4);

        for (int i = 0; i < directions; i++)
        {
            // Spin "forward" around the up axis: i = 0 is world forward, then evenly around the circle.
            Vector3 direction = Quaternion.Euler(0f, 360f * i / directions, 0f) * Vector3.forward;

            // skinWidth is added because we already hold that gap away from surfaces, so wallDetectDistance means "how far past our normal gap" we still count.
            if (!SweepCapsule(position, direction, wallDetectDistance + skinWidth, out RaycastHit hit))
                continue;

            // Only the closest hit matters, and only if it is actually wall-shaped (this skips floors, ceilings and walkable slopes we happened to graze).
            if (hit.distance >= closestDistance || !IsWallNormal(hit.normal))
                continue;

            closestHit = hit;
            closestDistance = hit.distance;
            found = true;
        }

        if (!found)
            return false;

        // The capsule's own hit normal can be wrong near corners and edges (it may report the direction toward the capsule's rounded end). Ask the surface itself instead.
        Vector3 normal = ResolveWallNormal(closestHit);

        // Re-check with the corrected normal: the better answer might not be a wall after all.
        if (!IsWallNormal(normal))
            return false;

        // Flatten: we only care which HORIZONTAL way the wall faces.
        // A normal with no horizontal part is a floor or ceiling - not a wall.
        Vector3 flatNormal = Flatten(normal);

        if (flatNormal.sqrMagnitude < 0.0001f)
            return false;

        flatNormal.Normalize();

        wall.valid = true;
        wall.normal = flatNormal;
        wall.point = closestHit.point;
        wall.collider = closestHit.collider;

        // The direction ALONG the wall: the normal turned 90 degrees around the up axis.
        //
        // cross(up, normal) is perpendicular to both, so it lies flat in the wall's plane:
        //
        //    wall normal = (0, 0, 1)  ->  tangent = cross(up, normal) = (1, 0, 0)
        //
        // It works for walls rotated to ANY angle, which is the whole reason we use a cross product instead of hardcoding "left/right" in world space.
        // The opposite way along the wall is simply -tangent.
        wall.tangent = Vector3.Cross(Vector3.up, flatNormal).normalized;

        return true;
    }

    // A wall is a surface that is mostly vertical and not something we could stand on.
    //
    // normal.y is 0 for a perfectly vertical wall, 1 for a flat floor, -1 for a flat ceiling, so abs() lets one check reject both floors AND ceilings.
    private bool IsWallNormal(Vector3 surfaceNormal)
    {
        return Mathf.Abs(surfaceNormal.y) <= wallMaxNormalY && !IsWalkable(surfaceNormal);
    }

    // Same idea as ResolveGroundNormal: capsule hits can lie about the normal near edges, so shoot a plain ray at the contact point and use the surface normal it reports.
    private Vector3 ResolveWallNormal(RaycastHit hit)
    {
        GetCapsulePoints(position, out Vector3 p0, out Vector3 p1, out _);

        // The capsule's center line runs p0 -> p1. Find the point on it closest to the contact
        // (a "closest point on a segment" projection, clamped so it stays between p0 and p1).
        // Shooting from there makes the ray travel straight at the wall at the contact's height.
        Vector3 axis = p1 - p0;

        float along = axis.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(hit.point - p0, axis) / axis.sqrMagnitude)
            : 0f;

        Vector3 origin = p0 + axis * along;

        Vector3 toContact = hit.point - origin;
        float distance = toContact.magnitude;

        // Contact is on top of our center line: no direction to shoot in, trust the capsule.
        if (distance < 0.0001f)
            return hit.normal;

        // Overshoot by a little so the ray reliably reaches the surface.
        // Must hit the SAME collider, or we might read a different object's normal.
        if (Physics.Raycast(
                origin,
                toContact / distance,
                out RaycastHit rayHit,
                distance + NormalProbeHeight,
                collisionMask,
                QueryTriggerInteraction.Ignore)
            && rayHit.collider == hit.collider)
        {
            return rayHit.normal;
        }

        return hit.normal;
    }

    // Fills in lookAngle / forwardTangent / lookOk from where we are facing RIGHT NOW.
    //
    // Top-down, wall along the bottom, tolerance = 30:
    //
    //              (normal points up, away from the wall)
    //                      ^
    //                      |
    //     ok cone  \       |       /  ok cone
    //     (-tangent) \     |     /    (+tangent)
    //   <---------------- wall ---------------->
    //
    // The ok zone is a cone of +/- tolerance around EACH direction along the wall, so with 30 degrees that is two 60 degree cones, one per direction.
    //
    // Rules:
    //  - Only our flat (yaw) direction counts. Looking up or down changes nothing.
    //  - We compare against the wall's tangent and -tangent, and keep the closer one.
    //    We never compare against the normal: the spec wants "looking ALONG the wall".
    private void EvaluateWallLook(ref WallContact wall)
    {
        // Same flat look direction WishDirection uses for "forward".
        Vector3 look = Flatten(transform.forward);

        // No wall, or looking dead up/down so there is no flat direction: nothing to compare.
        // 180 is the worst possible angle, so this can never pass.
        if (!wall.valid || look.sqrMagnitude < 0.0001f)
        {
            wall.lookAngle = 180f;
            wall.lookOk = false;
            wall.forwardTangent = wall.tangent;
            return;
        }

        look.Normalize();

        // Angle() is always 0-180, between two directions, so we need both ways along the wall.
        // Example: facing 20 degrees off +tangent is 160 degrees off -tangent. Take the smaller.
        float anglePositive = Vector3.Angle(look, wall.tangent);
        float angleNegative = Vector3.Angle(look, -wall.tangent);

        bool facingPositive = anglePositive <= angleNegative;

        // forwardTangent is "the way along the wall we're heading" - the wall jump boosts us that way, and a future wall run would run that way too.
        wall.forwardTangent = facingPositive ? wall.tangent : -wall.tangent;
        wall.lookAngle = facingPositive ? anglePositive : angleNegative;
        wall.lookOk = wall.lookAngle <= wallJumpLookTolerance;
    }

    // ============================================================
    // PUSHING
    // ============================================================

    private void PushBody(Rigidbody body, RaycastHit hit, Vector3 moveDirection, Vector3 calculatedMovement)
    {
        // Only push horizontally.
        Vector3 pushDirection = Flatten(moveDirection);

        if (pushDirection.sqrMagnitude < 0.000001f)
            return;

        pushDirection.Normalize();

        Vector3 playerVelocity = calculatedMovement + currentPlatformVelocity;

        // Relative closing velocity:
        //
        // closingSpeed =
        // dot(playerVelocity - objectVelocity, pushDirection)
        //
        // Positive means we're moving INTO the object.
        float closingSpeed = Vector3.Dot(playerVelocity - body.GetPointVelocity(hit.point), pushDirection);

        if (closingSpeed <= 0f)
            return;

        // Reduced mass:
        //
        // μ = (m1 * m2) / (m1 + m2)
        //
        // This gives realistic collision response between two objects of different mass.
        float reducedMass = (playerMass * body.mass) / (playerMass + body.mass);

        // Impulse:
        //
        // J = μ * relativeVelocity
        //
        // Impulse changes momentum immediately:
        //
        // J = Δp = m * Δv
        body.AddForceAtPosition(pushDirection * (reducedMass * closingSpeed * pushStrength), hit.point, ForceMode.Impulse);
    }

    // ============================================================
    // MOVING PLATFORMS
    // ============================================================

    private Vector3 ApplyPlatformCarry(float dt)
    {
        if (platform == null ||
            !platform.gameObject.activeInHierarchy)
        {
            platform = null;
            return Vector3.zero;
        }

        RotateWithPlatform();

        // platformLocalPos stores our anchor relative to the platform.
        //
        // TransformPoint converts that old local anchor into its NEW world position.
        //
        // Therefore:
        //
        // platformDelta = newAnchorWorldPosition - playerPosition
        Vector3 platformDelta = platform.TransformPoint(platformLocalPos) - position;

        if (platformDelta.sqrMagnitude < 0.0000000001f)
            return Vector3.zero;

        Vector3 discardedMovement = Vector3.zero;

        // Carry us with the platform while still respecting collision.
        MoveAndSlide(platformDelta, ref discardedMovement, false);

        // Velocity equation:
        //
        // velocity = displacement / time
        //
        // v = Δx / Δt
        return platformDelta / dt;
    }

    private void RotateWithPlatform()
    {
        // Only inherit yaw from mostly upright platforms.
        if (Vector3.Dot(platform.up, Vector3.up) > PlatformUprightDot)
        {
            // Rotation since last frame:
            //
            // deltaRotation =
            // currentRotation * inverse(previousRotation)
            Quaternion rotationDelta = platform.rotation * Quaternion.Inverse(platformLastRot);

            float yawDelta = Mathf.DeltaAngle(0f, rotationDelta.eulerAngles.y);

            if (Mathf.Abs(yawDelta) > 0.0001f)
                transform.Rotate(0f, yawDelta, 0f, Space.World);
        }
    }

    private void RecordPlatformAnchor()
    {
        // Only Rigidbody-backed ground can carry us.
        Rigidbody groundBody = null;

        if (grounded && groundCollider != null)
            groundBody = groundCollider.attachedRigidbody;

        if (groundBody == null)
        {
            platform = null;
            return;
        }

        platform = groundBody.transform;

        // Convert player world position into platform-local coordinates.
        //
        // This anchor will move naturally when the platform translates or rotates.
        platformLocalPos = platform.InverseTransformPoint(position);

        platformLastRot = platform.rotation;
    }

    // ============================================================
    // CONTACTS / EXTERNAL CONTROL
    // ============================================================

    private void DispatchContacts()
    {
        foreach (Collider contact in contactsThisStep)
        {
            if (contact == null ||
                contactsLastStep.Contains(contact))
            {
                continue;
            }

            // Only fire contact when first touching the object.
            if (contact.TryGetComponent(out IPlayerContact receiver))
                receiver.OnPlayerContact(this);
        }

        // Current contacts become next frame's previous contacts.
        contactsLastStep.Clear();

        foreach (Collider contact in contactsThisStep)
            contactsLastStep.Add(contact);

        contactsThisStep.Clear();
    }

    public void SetVelocity(Vector3 worldVelocity)
    {
        // External systems like launch pads can directly set velocity.
        velocity = worldVelocity;

        grounded = false;
        platform = null;

        // Launched by something else (launch pad): no coyote jump.
        lastGroundedTime = NeverGrounded;
    }

    public void Teleport(Vector3 worldPosition)
    {
        // Synchronize every representation of position so the controller doesn't snap back afterward.
        position = worldPosition;
        lastTarget = worldPosition;

        velocity = Vector3.zero;

        grounded = false;
        platform = null;

        // Teleporting doesn't count as walking off an edge.
        lastGroundedTime = NeverGrounded;
        currentWall = default;
        wallRunning = false;
        wallRunTimeUsed = 0f;

        rb.position = worldPosition;
        transform.position = worldPosition;
    }

    // ============================================================
    // DEBUG
    // ============================================================

    private void DrawDebugRays()
    {
        if (!drawDebugRays)
            return;

        // Green = ground normal.
        Debug.DrawRay(position + Vector3.up * 0.1f, groundNormal * 1.5f, Color.green);

        // Cyan = velocity.
        Debug.DrawRay(position + Vector3.up, velocity * 0.15f, Color.cyan);

        DrawWallDebugRays();
    }

    // Shows why a wall jump would be accepted or rejected. Reads currentWall, same as the jump does, but re-checks the look so the colours follow the mouse between physics steps.
    private void DrawWallDebugRays()
    {
        if (!currentWall.valid)
            return;

        WallContact wall = currentWall;
        EvaluateWallLook(ref wall);

        Vector3 origin = position + Vector3.up * 1f;
        Color verdict = wall.lookOk ? Color.green : Color.red;

        // Magenta = wall normal.
        Debug.DrawRay(origin, wall.normal * 1.5f, Color.magenta);

        // The tangent we're facing is green (passes) or red (fails); the other one is yellow.
        Debug.DrawRay(origin, wall.forwardTangent * 2f, verdict);
        Debug.DrawRay(origin, -wall.forwardTangent * 2f, Color.yellow);

        // Edges of the allowed cone around the facing tangent, in white.
        Quaternion tolerance = Quaternion.AngleAxis(wallJumpLookTolerance, Vector3.up);
        Quaternion toleranceBack = Quaternion.AngleAxis(-wallJumpLookTolerance, Vector3.up);
        Debug.DrawRay(origin, tolerance * wall.forwardTangent * 1.5f, Color.white);
        Debug.DrawRay(origin, toleranceBack * wall.forwardTangent * 1.5f, Color.white);

        // Blue = our flattened look direction.
        Debug.DrawRay(origin, Flatten(transform.forward).normalized * 2f, Color.blue);
    }

    private void DrawDebugHUD()
    {
        if (!showDebugHUD)
            return;

        Vector3 horizontalVelocity = Flatten(velocity);

        string platformName = "none";

        if (platform != null)
            platformName = platform.name;

        string wallText = "none";

        if (currentWall.valid)
        {
            WallContact wall = currentWall;
            EvaluateWallLook(ref wall);
            wallText = $"{(wall.lookOk ? "OK" : "blocked")}  look angle: {wall.lookAngle:F0} deg";
        }

        GUI.Label(
            new Rect(10f, 10f, 420f, 220f),
            $"speed: {velocity.magnitude:F1}   horizontal: {horizontalVelocity.magnitude:F1}\n" +
            $"velocity: {velocity}\n" +
            $"grounded: {grounded}   slope: {slopeAngle:F0} deg\n" +
            $"sliding: {sliding}   crouching: {crouching}\n" +
            $"platform: {platformName}\n" +
            $"coyote left: {Mathf.Max(0f, coyoteTime - (Time.time - lastGroundedTime)):F2}s (window {coyoteTime:F2}s)\n" +
            $"wall: {wallText}\n" +
            $"wall run: {wallRunning}   grace left: {Mathf.Max(0f, wallRunGracePeriod - wallRunTimeUsed):F2}s"
        );
    }
}
;