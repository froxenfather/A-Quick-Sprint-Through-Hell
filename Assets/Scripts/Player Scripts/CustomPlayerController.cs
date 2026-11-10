using System.Collections.Generic;
using IObjects;
using UnityEngine;
using UnityEngine.InputSystem;

// setup:
// kinematic Rigidbody + CapsuleCollider on Player root
// feet are assumed to sit at the object's pivot
// camera tied to head
// controller philosophy:
// - Rigidbody does NOT drive movement
// - We manually integrate velocity accel etc thru good ol math
// - Capsule sweeps detect collisions before movement
// - Collisions are resolved using "collide and slide"
// - Kinematic Rigidbody mainly exists for:
//      1. trigger interactions
//      2. rigidbody interpolation 
//      3. punching objects and such!

// ladies and Gentlemen
// the FRAGNUM OPUS!

// warning: This is a LOT of code. explanations for the functions in detail are found in Research/How It Works
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class CustomPlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private bool allowWASD = true;

    [Header("Ground Movement")]
    [SerializeField] private float walkSpeed = 12f;
    [SerializeField] private float sprintSpeed = 20f;

    // how quickly velocity approaches target movement speed
    [SerializeField] private float groundAccel = 90.9f;

    // deceleration when already moving faster than our desired speed
    [SerializeField] private float groundOverspeedDecel = 52.1f;

    // deceleration when no movement input is held
    [SerializeField] private float groundFriction = 68.6f;

    // brief friction-free window after landing
    // lets momentum survive landings / allows bunny hopping
    [SerializeField] private float landingGrace = 0.04f;

    [Header("Air Movement")]
    [SerializeField] private float jumpVelocity = 14.68f;

    // downward acceleration in m/s^2
    [SerializeField] private float gravity = 30.01f;

    // horizontal acceleration while airborne
    [SerializeField] private float airControl = 15.22f;

    // prevent infinite horizontal air acceleration
    [SerializeField] private float maxAirSpeed = 20f;

    // how fast horizontal speed bleeds off when above maxAirSpeed
    // high = acts like a hard clamp, low = momentum carries through the air
    [SerializeField] private float airOverspeedDecel = 52.1f;

    // coyote time: for this many seconds after walking off an edge, pressing jump still counts as a normal ground jump
    // named after the cartoon coyote who hangs in the air before falling. just saw Coyote Vs Acme and was reminded of this!
    // 0 turns it off. around 0.1 feels forgiving without looking like a double jump
    [SerializeField] private float coyoteTime = 0.1f;

    [Header("Slide")]
    // friction while sliding. way lower than groundFriction so momentum carries
    [SerializeField] private float slidingGroundFriction = 4f;

    // speed added the moment a slide starts...
    [SerializeField] private float slideBoost = 5f;

    // ...but only if we're going slower than this. so the most a boost can ever give is about this + slideBoost
    [Range(0f, 60f)]
    [SerializeField] private float slideBoostMaxSpeed = 25f;

    // minimum speed (m/s, same units as walkSpeed/sprintSpeed) needed to START a slide (grounded OR airborne). too slow and we crouch instead
    [Range(0f, 30f)]
    [SerializeField] private float slideMinSpeed = 6.7f;

    // how strongly A/D nudge our direction while sliding. way weaker than groundAccel - this is a slow drift, not steering like regular ground movement. w/S do nothing at all while sliding
    [SerializeField] private float slideSteerAccel = 10f;

    // multiplier on gravity pulling us DOWN a slope while sliding. 1 = plain g * sin(angle)
    [SerializeField] private float slideSlopeGravityScale = 1f;

    // same, but for slowing us down going UP a slope. lower than slideSlopeGravityScale on purpose:
    // full gravity uphill kills speed just as hard as it builds it downhill, which eats the exact momentum you need to carry over a lip and launch. feel over realism
    // but too low and two ramps facing each other become a perpetual motion machine: going down gains more than going up loses, so every pass through the valley is faster than the last
    // rule of thumb: keep this at about 0.77 or more while slidingGroundFriction is 4 and gravity is 40, and a valley can never gain speed on any walkable slope
    [Range(0f, 1f)]
    [SerializeField] private float slideUphillGravityScale = 0.8f;

    // one shared timer, counted from the start of a slide:
    // no new slide can start before it runs out, and a jump inside it is a slide jump
    [SerializeField] private float slideGrace = 0.5f;

    // extra horizontal speed a slide jump gives
    [SerializeField] private float slideJumpBoost = 8f;

    // landing on the ground while sliding in the air turns some of the fall speed into forward speed
    // this is how much of it we keep: 1 = all of it (the old, very strong boost), 0 = none (a plain landing)
    [Range(0f, 1f)]
    [SerializeField] private float slideLandingBoostScale = 0.35f;

    // hard cap (m/s) on that landing boost, so a very tall drop can't launch you
    [SerializeField] private float slideLandingBoostMax = 12f;

    // gravity multiplier while sliding in the AIR: 4 = four times normal gravity, 1 = off
    // it follows `sliding` every step, so the moment the slide ends (button released, wall jump) gravity is back to normal
    // important to know it only affects gravity ONCE YOU GO DOWN!!!!!!!!
    [Min(0f)]
    [SerializeField] private float slideAirGravityMultiplier = 4f;

    [Header("Dash")]
    // how strongly a dash turns our flat velocity toward where the camera is looking, in percent
    // 100 = we go exactly where we look. 10 = we mostly keep our old direction. only the direction changes, never the speed
    [Range(0f, 100f)]
    [SerializeField] private float dashDirectionInfluence = 80f;

    // speed (m/s) added to our current flat speed by a dash
    [SerializeField] private float dashSpeedBonus = 5f;

    // a dash never leaves us slower than this, so it is still useful from a standstill. sprint (Shift held) uses the higher one
    [SerializeField] private float dashMinSpeedSprint = 35f;
    [SerializeField] private float dashMinSpeedWalk = 20f;

    // seconds before we can dash again
    [Min(0f)]
    [SerializeField] private float dashCooldown = 1.5f;

    // seconds of no gravity right after a dash: we glide level instead of dropping. 0 turns the hang off
    // a jump or wall jump ends it early, and landing doesn't need it (gravity isn't used on the ground)
    [Min(0f)]
    [SerializeField] private float dashHangTime = 0.25f;

    [Header("Wall Jump")]
    // how high (meters) a wall jump lifts us, measured with normal gravity. a normal jump is about 3.6 m with the default jumpVelocity and gravity
    // touching a wall halves gravity (Wall Slip), so the real jump goes a little higher while we are still next to the wall
    [Min(0f)]
    [SerializeField] private float wallJumpHeight = 1.56f;

    // flat speed added straight away from the wall (along the wall normal)
    [SerializeField] private float wallJumpPushAway = 10f;

    // flat speed added along the wall, in the direction we're facing
    // defaults to the same value as slideJumpBoost
    [SerializeField] private float wallJumpForwardBoost = 8f;

    // how far off "looking parallel to the wall" we can be and still wall jump. applies to BOTH directions along the wall, so 30 gives a 60 degree cone around each one
    [Range(0f, 90f)]
    [SerializeField] private float wallJumpLookTolerance = 30f;

    // on: we can wall jump off ANY wall we touch in the air, whichever way we face (run face first into a wall, jump up it, kick off backwards). off: we must look along the wall, as before
    // wall RUNNING always needs the look, whatever this says
    // a kick from outside the look range is just up and straight away from the wall: the sideways boost along the wall (wallJumpForwardBoost) only happens when we are looking along it
    [SerializeField] private bool wallJumpAnyDirection = false;

    // how close (meters, from the capsule surface, on top of skinWidth) a wall must be to count
    [SerializeField] private float wallDetectDistance = 0.15f;

    // how many horizontal directions we probe around the capsule. more = catches walls at odd angles better
    [Range(4, 16)]
    [SerializeField] private int wallProbeDirections = 8;

    // a surface only counts as a wall if abs(normal.y) is at most this. 0 = perfectly vertical
    [Range(0f, 1f)]
    [SerializeField] private float wallMaxNormalY = 0.3f;

    [Header("Wall Slip")]
    // the "slipping speed": while touching a wall in the air, gravity is multiplied by this. 0.5 = half gravity, 1 = walls change nothing, 0 = glued to the wall
    // it is a multiplier (not an m/s^2 number) so it keeps following gravity if you retune that. it is an acceleration, not a top speed: we still speed up as we slip, just half as fast
    // applies to ANY wall touch, no look angle needed. the moment we are off the wall, gravity is back to normal
    [Range(0f, 1f)]
    [SerializeField] private float wallSlipGravityScale = 0.5f;

    [Header("Wall Run")]
    // flat speed (m/s) needed to start AND keep a wall run, on top of holding sprint and meeting every wall jump requirement
    [SerializeField] private float wallRunMinSpeed = 20f;

    // seconds of wall running allowed before Y unpauses and we slip down the wall as normal
    // it is a budget, not a per-wall timer: it only refills on a wall jump or on touching the ground
    [SerializeField] private float wallRunGracePeriod = 1.5f;

    [Header("Crouch")]
    // what pressing slide does instead, if we're too slow to slide (or not moving at all)
    [SerializeField] private float crouchSpeed = 5f;

    // capsule height while crouching. standing height is read from the CapsuleCollider itself at Awake
    [SerializeField] private float crouchHeight = 1.2f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;

    // small gap maintained between capsule and surfaces
    // helps avoid floating point collision jitter
    [SerializeField] private float skinWidth = 0.03f;

    [SerializeField] private float maxSlopeAngle = 60f;
    [SerializeField] private float stepHeight = 0.4f;

    // how far we try to "stick" downward while already grounded
    [SerializeField] private float groundSnapDistance = 0.3f;

    // small landing detection distance while airborne
    [SerializeField] private float groundProbeDistance = 0.05f;

    // uphill slopes above this angle can launch us when we leave them
    [SerializeField] private float launchSlopeAngle = 10f;

    // maximum collision-and-slide attempts in one FixedUpdate
    [SerializeField] private int maxBumps = 4;

    [Header("Pushing Rigidbodies")]
    [SerializeField] private float playerMass = 10f;
    [SerializeField] private float pushStrength = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugHUD = true;
    [SerializeField] private bool showDebugRays = false;

    // value for lastGroundedTime meaning "no coyote jump available". far enough in the past to never match
    private const float NeverGrounded = -999f;

    // anything shorter than this counts as "not moving"
    private const float MinMoveDistance = 0.0001f;

    // if the Rigidbody is further than this from where we put it, something else moved us
    // squared distance, so 0.0004 means 0.02 meters
    private const float ExternalMoveThresholdSqr = 0.0004f;

    // upward speed (m/s) above which leaving a steep slope launches us
    private const float MinLaunchUpSpeed = 0.05f;

    // speed (m/s) away from a surface above which we count as leaving it
    private const float MovingAwaySpeed = 0.1f;

    // two wall normals with a dot above this (about 15 degrees apart) are the same wall seen by two probes
    private const float SameWallNormalDot = 0.966f;

    // summed corner normals shorter than this are thrown away. two normals with angle phi between them sum to 2 * cos(phi / 2), so 0.5 means walls facing nearly opposite (over about 151 degrees apart, a narrow shaft)
    private const float MinCornerNormalLength = 0.5f;

    // slack (m/s) on wallRunMinSpeed, so a speed that is "exactly 20" but 19.99999 after float math still counts
    private const float WallRunSpeedTolerance = 0.05f;

    // step-up tuning
    private const float MinStepRaiseHeight = 0.02f;     // less headroom than this: can't step
    private const float StepForwardNudge = 0.05f;       // always try to move at least this far forward
    private const float MinStepForwardDistance = 0.01f; // no room even when raised: it's a real wall
    private const float MinStepHeightGained = 0.01f;    // shorter than this isn't a step
    private const float StepHeightTolerance = 0.01f;    // slack on stepHeight

    // not worth moving us down for less than this when snapping to the ground
    private const float MinSnapDistance = 0.002f;

    // how upright a platform must be (dot with world up) to turn us with it
    private const float PlatformUprightDot = 0.7f;

    // ResolveGroundNormal starts a short ray this far above the contact and casts this far down
    private const float NormalProbeHeight = 0.1f;
    private const float NormalProbeDistance = 0.2f;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    // custom authoritative player position
    // this is the position our controller actually reasons about
    private Vector3 position;

    // absolute world-space velocity
    private Vector3 velocity;

    // last position we told the Rigidbody to move toward
    // used to detect external movement / teleports
    private Vector3 lastTarget;

    // velocity inherited from moving platforms
    private Vector3 currentPlatformVelocity;

    private Vector2 moveInput;
    private bool sprinting;
    private bool jumpQueued;

    // sprintSpeed as it was at Awake, before any hazard lowered it
    private float startSprintSpeed;

    // flat speed we had when we left the ground. air control can't push us past it
    private float airSpeedCap;

    // slide state
    private bool sliding;
    private bool slideQueued;
    private bool slideHeld;
    private float slideStartTime = -999f;

    // dash state. dashQueued is the same one-frame-press trick as jumpQueued; lastDashTime is when the last dash happened (-999 = never, so the first one is always allowed)
    private bool dashQueued;
    private float lastDashTime = -999f;

    // gravity is switched off until this time (see dashHangTime). in the past = no hang
    private float dashHangEndTime = -999f;

    // crouch state. crouching and sliding share the same button; only one is ever true at once
    private bool crouching;

    // standing capsule size, read once at Awake so crouching can restore it exactly
    private float standHeight;
    private float standCenterY;

    // everything we know about the wall we're next to. single source of truth: wall jump, wall run, wall slip gravity and the debug rays all read it
    // detection and the look check live ONLY in the WALL DETECTION section, filled by UpdateWallContact()
    // a plain struct (small value type), so copying it to re-check the look doesn't touch the stored one
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

    // wall run state. wallRunning is decided once per step by UpdateWallRun(); wallRunTimeUsed is the grace period spent so far
    private bool wallRunning;
    private float wallRunTimeUsed;

    private bool grounded;
    private Vector3 groundNormal = Vector3.up;
    private Collider groundCollider;
    private float slopeAngle;
    private float landTime = -999f;

    // the last time we ended a physics step standing on something. coyote time counts from here
    // -999 means "we are not allowed a coyote jump right now" (same trick as slideStartTime)
    private float lastGroundedTime = NeverGrounded;

    // minimum Y component a surface normal needs to count as walkable
    private float minGroundNormalY;

    // moving platform tracking
    private Transform platform;
    private Vector3 platformLocalPos;
    private Quaternion platformLastRot;

    // reused buffers avoid allocating straight garbage every physics frame
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly Collider[] overlapBuffer = new Collider[16];
    private readonly Vector3[] wallNormalBuffer = new Vector3[16];

    // hazard surfaces (SpeedCapSurface) we are touching, each with the normal of the touch
    // filled during the step (collide and slide hits) and at its end (ground and wall), then used and cleared by ApplySurfaceHazards on the next step
    private struct SurfaceTouch
    {
        public SpeedCapSurface surface;
        public Vector3 normal;
    }

    private readonly List<SurfaceTouch> surfaceTouches = new List<SurfaceTouch>();

    // the hazards we touched the step before, so only the FIRST touch of one bounces us
    private readonly HashSet<SpeedCapSurface> surfacesTouchedLastStep = new HashSet<SpeedCapSurface>();

    // hazards already announced this step, so a surface touched twice in one step (floor and a hit) only counts once
    private readonly HashSet<SpeedCapSurface> surfacesAnnouncedThisStep = new HashSet<SpeedCapSurface>();

    // track collision contacts so events fire once on contact
    private readonly HashSet<Collider> contactsThisStep = new HashSet<Collider>();
    private readonly HashSet<Collider> contactsLastStep = new HashSet<Collider>();

    public Vector3 Velocity => velocity;
    public bool IsGrounded => grounded;
    public bool IsSliding => sliding;
    public bool IsCrouching => crouching;
    public bool IsWallRunning => wallRunning;

    // shift is held. (True even when standing still, check HorizontalSpeed too.)
    public bool IsSprinting => sprinting;

    // airborne, touching a wall, falling, and not wall running: slipping down it
    public bool IsWallSliding => !grounded && !wallRunning && currentWall.valid && velocity.y < 0f;

    // the sprint speed we have right now (hazards can lower it) and the one we started with
    public float SprintSpeed => sprintSpeed;
    public float WalkSpeed => walkSpeed;

    // fired the moment these happen, so effects (the FOV punch, sounds, animations) can react without the controller knowing about them
    public event System.Action Dashed;
    public event System.Action Jumped;
    public event System.Action WallJumped;

    // fired the first time we touch a SpeedCapSurface (lava, electric wall), with the surface we touched
    public event System.Action<SpeedCapSurface> HazardTouched;

    // flat speed relative to whatever we're standing on, so riding a fast platform doesn't count as running
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

        // hazards can lower sprintSpeed during play, and a health pickup puts it back to this
        startSprintSpeed = sprintSpeed;

        CalculateMinGroundNormal();
    }

    // lowers the sprint speed by amount, but never below floor (and never raises it). used by hazards like lava
    public void ReduceSprintSpeed(float amount, float floor)
    {
        sprintSpeed = Mathf.Max(Mathf.Min(floor, sprintSpeed), sprintSpeed - amount);
    }

    // puts the sprint speed back to what it was when the game started
    public void ResetSprintSpeed()
    {
        sprintSpeed = startSprintSpeed;
    }

    private void OnValidate()
    {
        // recalculate immediately when changing slope angle in Inspector
        CalculateMinGroundNormal();
    }

    private void Update()
    {
        ReadInput();
    }

    // everything the controller does each physics step, in order so just read this function top to bottom to see the whole controller
    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        AcceptExternalMovement();

        // first inherit motion from whatever platform we stand on
        // (currentPlatformVelocity still holds LAST step's value until the line below overwrites it, so grab it first: LaunchOffMovingGround needs to know how much speed the platform lost.)
        Vector3 previousPlatformVelocity = currentPlatformVelocity;
        currentPlatformVelocity = ApplyPlatformCarry(dt);

        // fix any overlap caused by moving platforms, teleports, etc
        Depenetrate();

        // movement logic should happen RELATIVE to the platform
        // playerVelocity = relativeVelocity + platformVelocity
        // therefore
        // relativeVelocity = playerVelocity - platformVelocity
        Vector3 calculatedMovement = velocity - currentPlatformVelocity;

        bool startedGrounded = grounded;

        // remember our takeoff speed every step we spend on the ground
        // whenever we leave it (jump, ledge, launch), the last value stays as the air cap
        // walk speed is the floor so a standing jump can still reach walking speed
        if (startedGrounded)
            airSpeedCap = Mathf.Max(walkSpeed, Flatten(calculatedMovement).magnitude);

        // decide slide/crouch BEFORE jumping, so pressing slide and jump on the same frame still counts as a slide jump instead of missing it by one frame
        UpdateSlideAndCrouch(ref calculatedMovement, dt);

        // dash before the jump, so pressing both on the same frame still gives the jump the dash's speed (and airSpeedCap)
        TryDash(ref calculatedMovement);

        // change velocity
        // leaving the ground can happen two ways: we jump, or the ground launches us (like if you fly off a platform or launchpad) (falling off a platform also counts)
        bool leftGround =
            Jump(ref calculatedMovement) || LaunchOffMovingGround(previousPlatformVelocity - currentPlatformVelocity, dt);

        // wall run is decided AFTER the jump, so a wall jump this step (which refills the grace period and pushes us off the wall) is already counted
        UpdateWallRun(calculatedMovement, dt);

        calculatedMovement = Move(calculatedMovement, dt);

        // hazard surfaces get the last word on our speed, AFTER the movement model, which would otherwise speed us straight back up. this also caps a dash. a bounce counts as leaving the ground, like a jump does
        leftGround |= ApplySurfaceHazards(ref calculatedMovement);

        // change position
        // displacement = velocity * time
        // x_new = x_old + v * dt
        MoveAndSlide(calculatedMovement * dt, ref calculatedMovement, startedGrounded && !leftGround);

        // 3. then work out where we ended up
        UpdateGrounded(startedGrounded, leftGround, calculatedMovement);

        // make sure we remember any wall we're next to, for the next step's wall jump (and the debug rays)
        UpdateWallContact();

        // remember any hazard we ended the step on or next to, for the next step
        CollectSurfaceTouches();

        // convert relative velocity back into world velocity
        velocity = calculatedMovement + currentPlatformVelocity;

        // tell the surrounding  world about it, and remember things for next step
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
        // rigidbody exists as a Unity physics shell
        // but the code remains responsible for actual movement

        // better to set these here so we dont untick them ingame yknow?
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void CalculateMinGroundNormal()
    {
        // math Time!
        // surface is walkable when:

        // normal.y >= cos(maxSlopeAngle)

        // example:
        // maxSlopeAngle = 60 degrees
        // cos(60) = 0.5

        // so any surface normal with y >= 0.5 is <= 60 degrees steep
        minGroundNormalY = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
    }

    private void AcceptExternalMovement()
    {
        // if another system moved the Rigidbody unexpectedly, accept that new position instead of snapping back
        // helpful trigger for doors shoving you or platforms moving you!
        if ((rb.position - lastTarget).sqrMagnitude > ExternalMoveThresholdSqr)
            position = rb.position;
    }

    private void CommitPosition()
    {
        lastTarget = position;

        // let Rigidbody interpolation visually smooth the custom position
        rb.MovePosition(position);
    }

    // ============================================================
    // INPUT
    // ============================================================

    private void ReadInput()
    {
        // basic Movement
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

        // so there was a funny bug at the start where diagonal movement was reasonably twice as fast as usual movement
        // prevent diagonal input from being faster than straight input

        // without normalize:
        // (1,1) magnitude = sqrt(2) ~= 1.414

        // with normalize:
        // magnitude = 1
        moveInput = input.normalized;
        // normalizing the direction keeps speed precise!
        sprinting = keyboard.leftShiftKey.isPressed;

        // queue jump here so FixedUpdate cannot miss a single-frame key press
        if (keyboard.spaceKey.wasPressedThisFrame)
            jumpQueued = true;

        // dash is E, or the forward thumb button of the mouse (checked below). same queue trick as jump
        if (keyboard.eKey.wasPressedThisFrame)
            dashQueued = true;

        // slide is the back thumb button of the mouse. same queue trick as jump for the press, and a plain "is it down" for holding the slide
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.forwardButton.wasPressedThisFrame)
            dashQueued = true;

        slideHeld = mouse.backButton.isPressed;

        if (mouse.backButton.wasPressedThisFrame)
            slideQueued = true;
    }

    // ============================================================
    // VELOCITY STEPS: JUMP / SLIDE / CROUCH / MOVE
    // ============================================================

    // dash: an instant turn toward where we look, plus some speed, for quick course corrections that keep our momentum
    //   - Direction: our flat velocity turns toward the camera's flat look direction by dashDirectionInfluence percent of the angle between them. standing still has no direction to turn, so we go the way we look
    //   - Speed: current flat speed + dashSpeedBonus, but never below dashMinSpeedSprint (Shift held) or dashMinSpeedWalk
    //   - Vertical speed is set to 0, so a dash also stops any rising or falling
    //   - airSpeedCap is raised to the dash speed (never lowered), so air control can rebuild up to it afterwards

    // doesn't care whether we are grounded, in the air, sliding or wall running: it only changes calculatedMovement, and every movement model reads that afterwards
    // the press is used up either way, so pressing dash while it is on cooldown does not secretly buffer a dash
    private void TryDash(ref Vector3 calculatedMovement)
    {
        bool dashPressed = dashQueued;
        dashQueued = false;

        if (!dashPressed || Time.time - lastDashTime < dashCooldown)
            return;

        Vector3 flat = Flatten(calculatedMovement);
        float speed = flat.magnitude;

        Vector3 look = Flatten(transform.forward);
        bool hasLook = look.sqrMagnitude > 0.0001f;

        if (hasLook)
            look.Normalize();

        Vector3 direction;

        if (speed < 0.01f)
        {
            // standing still: nothing to turn, so go where we look. (Looking straight up or down leaves nothing to go toward either, so do nothing.)
            if (!hasLook)
                return;

            direction = look;
        }
        else
        {
            direction = flat / speed;

            if (hasLook)
            {
                // turn by a fraction of the angle between where we go and where we look
                // SignedAngle also handles dashing straight backwards: it gives exactly 180, so the turn is a plain rotation and never a zero-length vector
                float angleToLook = Vector3.SignedAngle(direction, look, Vector3.up);
                direction = Quaternion.AngleAxis(angleToLook * (dashDirectionInfluence / 100f), Vector3.up) * direction;
            }
        }

        float minimumSpeed = sprinting ? dashMinSpeedSprint : dashMinSpeedWalk;
        float newSpeed = Mathf.Max(speed + dashSpeedBonus, minimumSpeed);

        calculatedMovement.x = direction.x * newSpeed;
        calculatedMovement.z = direction.z * newSpeed;

        // the dash cancels any rising or falling, so it is a clean horizontal burst
        calculatedMovement.y = 0f;

        // Max() so a dash can never LOWER the cap. same reason as the wall jump: AirMove would clamp the new speed away otherwise
        airSpeedCap = Mathf.Max(airSpeedCap, newSpeed);

        lastDashTime = Time.time;
        dashHangEndTime = Time.time + dashHangTime;
        Dashed?.Invoke();
    }

    // all hail the jump button lol
    // returns true if we jumped OFF THE GROUND (which means we left the ground this step). a wall jump happens in the air, not a jump like this
    
    // which jump we get, in priority order:
    //   1. ground jump  - standing on something, OR still inside the coyote window after walking off
    //   2. wall jump    - airborne, next to a wall, looking roughly along it
    //   3. nothing      - pressing jump in mid-air with no wall does nothing

    // each kind of jump is its own method, so future ones (wall run jump, etc.) get their own method and one more branch here instead of growing one enormous conditional
    private bool Jump(ref Vector3 calculatedMovement)
    {
        bool jumpPressed = jumpQueued;

        // the queued press is used up either way, so pressing Space in mid-air does not secretly "buffer" a jump for later
        jumpQueued = false;

        if (!jumpPressed)
            return false;

        // ground first, so near a ledge AND a wall you get the stronger ground jump
        if (grounded || InCoyoteWindow())
            return PerformGroundJump(ref calculatedMovement);

        // wall jumps are airborne already, so they are NOT "leaving the ground":
        // returning false keeps UpdateGrounded probing for a landing like any other airborne step
        TryWallJump(ref calculatedMovement);
        return false;
    }

    // coyote time: true if we were standing on something a split second ago, even though we are airborne now (we walked off an edge without jumping)
    // lastGroundedTime is set back to "never" whenever we leave the ground ON PURPOSE (a jump or a launch), so this can only be true after simply walking/falling off something
    // without that jumping would leave the window open and you could jump a second time in mid-air
    private bool InCoyoteWindow()
    {
        return Time.time - lastGroundedTime <= coyoteTime;
    }

    // normal jump, plus the slide jump boost. always succeeds, so always returns true
    // also for what a coyote jump does: it is exactly the same jump, we just allow it a bit late
    private bool PerformGroundJump(ref Vector3 calculatedMovement)
    {
        // jump is simply an immediate upward velocity assignment bc in a coyote jump we may have already started falling 
        calculatedMovement.y = jumpVelocity;

        // a jump ends a dash hang, otherwise we would rise with no gravity for the rest of it
        dashHangEndTime = -999f;

        // pseudocode:
        // - Only if `sliding` is true AND we're still within slideGrace seconds of when the slide started (compare Time.time - slideStartTime against slideGrace):
        //     - Add slideJumpBoost of speed to calculatedMovement, along the flattene direction we're already moving (same trick as the slide's entry boost)
        //     - Also raise airSpeedCap to match the new speed - it was captured BEFORE this function ran this step, so without this the boost gets clamped away instantly
        // - Otherwise: nothing extra, this is just a plain jump
        if (sliding && (Time.time - slideStartTime) < slideGrace)
        {
            Vector3 flatDirection = Flatten(calculatedMovement).normalized;

            calculatedMovement.x += flatDirection.x * slideJumpBoost;
            calculatedMovement.z += flatDirection.z * slideJumpBoost;

            // air Speed Change, Remove this in the future for design tweaking?
            airSpeedCap = Flatten(calculatedMovement).magnitude;
        }

        grounded = false;

        // we left on purpose, so close the coyote window: no second jump in mid-air
        lastGroundedTime = NeverGrounded;

        Jumped?.Invoke();

        return true;
    }

    // wall jump: needs an airborne player next to a wall they're looking roughly along (or any wall at all, with wallJumpAnyDirection)
    // returns true if the jump happened

    // flow (same order as the spec's flowchart):
    //   grounded?            -> no wall jump (the ground jump handles it)
    //   wall in range?       -> currentWall.valid, found by UpdateWallContact() last step
    //   looking along it?    -> within wallJumpLookTolerance of either direction along the wall (skipped when wallJumpAnyDirection is on)
    //   all yes              -> ApplyWallJump
    private bool TryWallJump(ref Vector3 calculatedMovement)
    {
        if (!TryGetWallContact(out WallContact wall))
            return false;

        // looking along the wall is required, unless wallJumpAnyDirection lets us kick off from any angle
        if (!wall.lookOk && !wallJumpAnyDirection)
            return false;

        ApplyWallJump(wall, ref calculatedMovement);
        WallJumped?.Invoke();
        return true;
    }

    // the part of the wall jump requirements that wall running shares: airborne with a wall in range
    // hands back the wall with the look re-checked against where we face right now (wall.lookOk may be false: the caller decides if that matters)
    private bool TryGetWallContact(out WallContact wall)
    {
        wall = currentWall;

        if (grounded || !currentWall.valid)
            return false;

        // currentWall was measured at the end of the last physics step, but the mouse can turn us between steps. copy it and re-check just the look part against where we face right now
        EvaluateWallLook(ref wall);

        return true;
    }

    // the wall jump's velocity change, three parts:
    //   1. UP:         the speed that reaches wallJumpHeight meters, sqrt(2 * gravity * height)
    //   2. AWAY:       wallJumpPushAway along the wall normal (in a corner: the normal of the corner, see ResolveWallJumpNormal)
    //   3. FORWARD:    wallJumpForwardBoost along the wall, in the direction we're facing. only when we are looking along the wall (lookOk): a wallJumpAnyDirection kick from any other angle gets no sideways boost

    // the flat parts are ADDED to the velocity we already have (not replaced), exactly like the slide jump boost
    // deterministic: the result only depends on current velocity + the wall
    private void ApplyWallJump(WallContact wall, ref Vector3 calculatedMovement)
    {
        // like any jump, upward speed is assigned, not added - so a wall jump always gives the same height no matter how fast we were falling
        calculatedMovement.y = Mathf.Sqrt(2f * gravity * wallJumpHeight);

        // same as a ground jump: ends any dash hang
        dashHangEndTime = -999f;

        // both vectors are flat (the push direction and forwardTangent have no height), so this is purely sideways
        Vector3 boost = ResolveWallJumpNormal(wall) * wallJumpPushAway;

        // facing the wall (or anywhere outside the look range) there is no "forward along the wall", and forwardTangent would just pick a random side. so no sideways boost: straight away from the wall
        if (wall.lookOk)
            boost += wall.forwardTangent * wallJumpForwardBoost;

        calculatedMovement.x += boost.x;
        calculatedMovement.z += boost.z;

        // same reason as the slide jump: airSpeedCap was captured before this function ran, and AirMove would clamp the boost away on the very next line. Max() so it can never LOWER the cap
        airSpeedCap = Mathf.Max(airSpeedCap, Flatten(calculatedMovement).magnitude);

        // a wall jump ends an air slide (SlideAirMove would lock our momentum and ignore air control)
        // pressing slide again starts a new one as normal - air slides ignore slideGrace
        sliding = false;

        // a wall jump refills the wall run grace period
        wallRunTimeUsed = 0f;
        wallRunning = false;

        // we just pushed off this wall, so it must not count as touching it this step (no slip gravity, no wall run)
        // UpdateWallContact measures again at the end of the step
        currentWall = default;
    }

    // which way a wall jump pushes us away. one wall in range: exactly that wall's normal. in a corner: the sum of EVERY wall's normal in range, which points out of the corner, away from all of them
    // pushing along just one face of a V-shaped corner would drive us into the other face, and collide-and-slide would eat the kick (we'd only go straight up the crease)

    // why only here and not in TryFindWall? slip, wall run and the look check keep using the single closest wall, and this ring of probes only runs on a wall jump, so it costs nothing per tick
    private Vector3 ResolveWallJumpNormal(WallContact wall)
    {
        // distinct wall normals found so far. the closest wall goes first, so a single wall gives back exactly wall.normal
        int count = 0;
        wallNormalBuffer[count++] = wall.normal;

        int directions = Mathf.Max(wallProbeDirections, 4);

        for (int i = 0; i < directions; i++)
        {
            // same ring as TryFindWall
            Vector3 direction = Quaternion.Euler(0f, 360f * i / directions, 0f) * Vector3.forward;

            if (!SweepCapsule(position, direction, wallDetectDistance + skinWidth, out RaycastHit hit) || !IsWallNormal(hit.normal))
                continue;

            // same correction as TryFindWall: capsule hit normals lie near edges and corners, so ask the surface itself
            Vector3 normal = ResolveWallNormal(hit);

            if (!IsWallNormal(normal))
                continue;

            Vector3 flatNormal = Flatten(normal);

            if (flatNormal.sqrMagnitude < 0.0001f)
                continue;

            flatNormal.Normalize();

            // several probes usually hit the same wall. count each wall once, or a wall with more probes on it would win the sum
            bool alreadyCounted = false;

            for (int n = 0; n < count; n++)
            {
                if (Vector3.Dot(wallNormalBuffer[n], flatNormal) > SameWallNormalDot)
                {
                    alreadyCounted = true;
                    break;
                }
            }

            if (!alreadyCounted && count < wallNormalBuffer.Length)
                wallNormalBuffer[count++] = flatNormal;
        }

        // only one wall: nothing to combine
        if (count == 1)
            return wall.normal;

        Vector3 sum = Vector3.zero;

        for (int n = 0; n < count; n++)
            sum += wallNormalBuffer[n];

        // walls facing each other (a narrow shaft) cancel out. a near-zero sum would normalize into a direction ALONG the walls, so fall back to the closest wall's normal
        if (sum.magnitude < MinCornerNormalLength)
            return wall.normal;

        return sum.normalized;
    }

    // a platform that stops (or slows down) faster than gravity can pull us back down leaves us flying with the speed it had
    // same idea as the old Rigidbody version launching you at the apex
    
    
    // returns true if we launched (which means we left the ground this step)
    private bool LaunchOffMovingGround(Vector3 platformVelocityLost, float dt)
    {
        if (!grounded)
            return false;

        // how fast the ground just moved AWAY from us

        // when a platform stops, the speed it had shows up in our platform-relative velocity (velocity - platformVelocity), pointing away from it. platformVelocityLost (last step's platform velocity minus this step's) is exactly that amount

        // we deliberately do NOT use our own velocity here: walking off a ledge makes the capsule touch the edge, which gives a tilted "walkable" ground normal pointing the way we are walking. our own speed lined up with that normal looked like the ground launching us and wiped the coyote window
        float speedAwayFromGround = Vector3.Dot(platformVelocityLost, groundNormal);

        // gravity can only pull us back by gravity * dt each step
        // moving away faster than that means the ground let go of us
        if (speedAwayFromGround > gravity * dt)
        {
            grounded = false;

            // launched on purpose, not walked off: no coyote jump (same reason as PerformGroundJump)
            lastGroundedTime = NeverGrounded;
            return true;
        }

        return false;
    }

    // decides whether we're sliding or crouching this step
    // pressing the button chooses between them based on how fast we're going; releasing it always ends whichever one we're in
    // (The actual slide/crouch movement math lives in SlideGroundMove/CrouchGroundMove below.)
    private void UpdateSlideAndCrouch(ref Vector3 calculatedMovement, float dt)
    {
        bool pressed = slideQueued;

        // used up either way, same as the jump press
        slideQueued = false;

        float flatSpeed = calculatedMovement.magnitude;

        if (pressed && !sliding && !crouching)
        {
            // pseudocode:
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

            //     - Start a SLIDE: turn `sliding` on, set `slideStartTime = Time.time`
            //     - Give the entry boost: if flatSpeed is below slideBoostMaxSpeed, add `slideBoost`
            //       of speed to calculatedMovement, along the flattened direction we're already moving
            //     - No grounded check here on purpose - this is allowed in midair too

            // - Otherwise, if we ARE grounded: start CROUCHING instead (turn `crouching` on)
            //   (Too slow AND airborne falls through here too - there's no crouching in midair,
            //   so just do nothing.)
        }

        // releasing the button always ends whichever state we're in. this is the ONLY way a slide ends now - you can slide to a dead stop on a ramp and keep sliding, backwards, once gravity starts winning
        if (sliding && !slideHeld)
            sliding = false;

        if (crouching && !slideHeld)
            crouching = false;

        SetCrouchHitbox(crouching);
    }

    // shrinks or restores the capsule for crouching. only the TOP moves - the bottom offset from the capsule's center stays fixed, so our feet never shift

    // known limitation: no ceiling check when standing back up, so standing under something low can clip you into it for a frame until Depenetrate() sorts it out. fine for v1
    private void SetCrouchHitbox(bool wantCrouching)
    {
        float targetHeight = wantCrouching ? crouchHeight : standHeight;

        if (Mathf.Approximately(capsule.height, targetHeight))
            return;

        float bottomOffset = standCenterY - standHeight * 0.5f;

        capsule.height = targetHeight;
        capsule.center = new Vector3(capsule.center.x, targetHeight * 0.5f + bottomOffset, capsule.center.z);
    }

    // decides whether we are wall running this step. runs every tick, right before Move()

    // a wall run needs ALL of these:
    //   - airborne with a wall in range, AND looking along it (TryGetWallContact + lookOk). always needed here, even when wallJumpAnyDirection lets a wall JUMP skip the look
    //   - sprint held
    //   - flat speed of at least wallRunMinSpeed
    //   - not already moving away from the wall. without this, the tick after a wall jump (wall still in range if the push is small) would freeze the jump's upward speed
    //   - grace period not used up

    // only time actually spent wall running is counted. the budget refills on landing here, and on a wall jump in ApplyWallJump
    private void UpdateWallRun(Vector3 calculatedMovement, float dt)
    {
        wallRunning = false;

        if (grounded)
        {
            wallRunTimeUsed = 0f;
            return;
        }

        if (!TryGetWallContact(out WallContact wall) || !wall.lookOk)
            return;

        bool fastEnough = Flatten(calculatedMovement).magnitude >= wallRunMinSpeed - WallRunSpeedTolerance;
        bool movingAway = Vector3.Dot(calculatedMovement, wall.normal) > MovingAwaySpeed;

        if (!sprinting || !fastEnough || movingAway || wallRunTimeUsed >= wallRunGracePeriod)
            return;

        wallRunning = true;
        wallRunTimeUsed += dt;
    }

    // choose movement model based on what state we're in
    private Vector3 Move(Vector3 calculatedMovement, float dt)
    {
        // wall running is airborne by definition and overrides the other air models (an air slide included)
        if (wallRunning)
            return WallRunMove(calculatedMovement);

        if (sliding)
            return grounded ? SlideGroundMove(calculatedMovement, dt) : SlideAirMove(calculatedMovement, dt);

        if (grounded)
            return crouching ? CrouchGroundMove(calculatedMovement, dt) : GroundMove(calculatedMovement, dt);

        return AirMove(calculatedMovement, dt);
    }

    // ============================================================
    // the Big MOVEMENT MODELS
    // ============================================================

    private Vector3 WishDirection()
    {
        // movement relative to where the player is facing
        Vector3 forward = Flatten(transform.forward).normalized; //gotta do this shit every time man
        Vector3 right = Flatten(transform.right).normalized;

        // WASD becomes a world-space direction
        return forward * moveInput.y + right * moveInput.x;
    }

    // same vector with the height removed
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
        // crouching always moves at crouchSpeed - sprint does nothing while crouched
        return GroundMoveTowards(calculatedMovement, dt, crouchSpeed);
    }

    // shared ground-acceleration model: approach targetSpeed along our wish direction, or apply friction when there's no input. walking and crouch-walking only differ by targetSpeed
    private Vector3 GroundMoveTowards(Vector3 calculatedMovement, float dt, float targetSpeed)
    {
        Vector3 wish = WishDirection();

        bool hasInput = wish.sqrMagnitude > 0.0001f;
        bool inLandingGrace = Time.time - landTime < landingGrace;

        // remove velocity pointing directly into/out of the ground

        // projection onto a plane:
        // v_plane = v - n * dot(v, n)
        // n = ground surface normal
        // this leaves only velocity parallel to the ground
        Vector3 velocityAlongGround = Vector3.ProjectOnPlane(calculatedMovement, groundNormal); //take a look at week 3 exp 9! super helpful dot product funtion

        if (hasInput)
        {
            // arguably the coolest and smoothest model I have here!
            // project desired movement onto the slope

            // this means "forward" becomes "forward along the ramp."
            Vector3 directionAlongGround = Vector3.ProjectOnPlane(wish, groundNormal).normalized;

            Vector3 targetVelocity = directionAlongGround * targetSpeed;

            // if above target speed, slow gently
            // otherwise accelerate aggressively
            float acceleration = groundAccel;

            if (velocityAlongGround.magnitude > targetSpeed)
                acceleration = groundOverspeedDecel;

            // MoveTowards is effectively bounded acceleration:
            // its a unity function as well
            // deltaVelocity <= acceleration * dt

            // v_new approaches targetVelocity gradually
            velocityAlongGround = Vector3.MoveTowards(velocityAlongGround, targetVelocity, acceleration * dt);
        }
        else if (!inLandingGrace)
        {
            // friction is implemented as constant deceleration toward zero

            // v_new = MoveTowards(v, 0, friction * dt)
            velocityAlongGround = Vector3.MoveTowards(velocityAlongGround, Vector3.zero, groundFriction * dt);
        }

        return velocityAlongGround;
    }

    // sliding overrides normal ground movement entirely: W/S do nothing, A/D only steer weakly, and a slope's own gravity component speeds you up or slows you down
    private Vector3 SlideGroundMove(Vector3 calculatedMovement, float dt)
    {
        Vector3 velocityAlongGround = Vector3.ProjectOnPlane(calculatedMovement, groundNormal);

        // steer left/right while sliding: only moveInput.x matters, no forward/back from W/S
        // added straight into velocityAlongGround like air control - a nudge over time, not a snap to a target speed
        float speedBeforeSteer = velocityAlongGround.magnitude;

        Vector3 right = Flatten(transform.right).normalized;
        velocityAlongGround += right * moveInput.x * slideSteerAccel * dt;

        // steering can only turn us, never speed us up - same idea as AirMove's airSpeedCap
        // capping against speedBeforeSteer (not some fixed number) means this can't be used to climb from a dead stop back up to speed either - 0 can only clamp back down to 0
        if (velocityAlongGround.magnitude > speedBeforeSteer)
            velocityAlongGround = velocityAlongGround.normalized * speedBeforeSteer;


        // pseudocode:
        // - Find "downhill": project Vector3.down onto the ground plane (Vector3.ProjectOnPlane)
        //   and normalize it. this points down the slope we're standing on
        // - Add to velocityAlongGround: downhill * gravity * sin(slopeAngle in RADIANS) * slideSlopeGravityScale * dt
        //   (slopeAngle is in degrees - Mathf.Sin wants radians, so multiply by Mathf.Deg2Rad first.)
        // - Flat floor (slopeAngle = 0): sin(0) = 0, so this adds nothing
        // - Sliding DOWN a slope: downhill points the way you're already going, so this speeds you up
        // - Sliding UP a slope (say, into one from flat ground): downhill points backward, so this
        //   slows you down, and can eventually push you back down it

        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

        // moving against downhill means we're climbing - that case gets its own, gentler scale (see slideUphillGravityScale) instead of reusing the downhill one for both directions
        bool movingUphill = Vector3.Dot(velocityAlongGround, downhill) < 0f;
        float slopeScale = movingUphill ? slideUphillGravityScale : slideSlopeGravityScale;

        velocityAlongGround += downhill * gravity * Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * slopeScale * dt;

        // slide friction always applies, on top of everything above - this is what finally brings a slide to a stop (very slowly, since slidingGroundFriction is low)
        velocityAlongGround = Vector3.MoveTowards(velocityAlongGround, Vector3.zero, slidingGroundFriction * dt);

        return velocityAlongGround;
    }

    private Vector3 AirMove(Vector3 calculatedMovement, float dt)
    {
        // air acceleration:

        // deltaVelocity = acceleration * dt

        // v_new = v_old + a * dt
        float speedBeforeAccel = Flatten(calculatedMovement).magnitude;

        calculatedMovement += WishDirection() * airControl * dt;

        Vector3 horizontal = Flatten(calculatedMovement);

        // being midair should never speed us up
        // air control can turn and brake us, but not push us past the speed we took off with
        // (If we are already faster than that, say from a launch pad, we just can't get any faster.)
        float airSpeedLimit = Mathf.Max(speedBeforeAccel, airSpeedCap);

        if (horizontal.magnitude > airSpeedLimit)
        {
            horizontal = horizontal.normalized * airSpeedLimit;

            calculatedMovement.x = horizontal.x;
            calculatedMovement.z = horizontal.z;
        }

        // over the limit: slow down gradually toward maxAirSpeed, keeping direction
        // MoveTowards never goes past maxAirSpeed, so this can't undershoot
        if (horizontal.magnitude > maxAirSpeed)
        {
            float slowedSpeed = Mathf.MoveTowards(horizontal.magnitude, maxAirSpeed, airOverspeedDecel * dt);

            horizontal = horizontal.normalized * slowedSpeed;

            calculatedMovement.x = horizontal.x;
            calculatedMovement.z = horizontal.z;
        }

        // gravity:
        // lets go back to phys 1 lol
        // v = v0 + g * dt

        // downward direction makes acceleration negative in Y
        calculatedMovement += Vector3.down * CurrentGravity(calculatedMovement.y) * dt;
        // gotta be careful to not clamp gravity here!

        return calculatedMovement;
    }

    // sliding in midair means momentum is locked in: no steering, no speed caps, just gravity (slideAirGravityMultiplier times heavier, see CurrentGravity)
    // (You get here by jumping out of a slide, or sliding off a ledge, with the button still held.)
    private Vector3 SlideAirMove(Vector3 calculatedMovement, float dt)
    {
        calculatedMovement += Vector3.down * CurrentGravity(calculatedMovement.y) * dt;
        return calculatedMovement;
    }

    // wall run: Y is paused (no rising, no falling) and EVERYTHING else is left alone. no air control, no speed cap, no overspeed bleed, so forward momentum is not hindered at all
    // speeds above wallRunMinSpeed (slide jump, wall jump) carry through the whole run
    private Vector3 WallRunMove(Vector3 calculatedMovement)
    {
        calculatedMovement.y = 0f;
        return calculatedMovement;
    }

    // gravity for the air models. touching a wall swaps it for the lower wall slip gravity; one step after we leave the wall it is normal again
    // currentWall is only ever valid while airborne (UpdateWallContact clears it on the ground), and so is every caller, so this never touches ground movement
    // when a wall run's grace period runs out, AirMove lands here with Y at 0: that is the "slip down the wall as normal"
    private float CurrentGravity(float verticalSpeed)
    {
        // right after a dash we glide level (see dashHangTime)
        if (Time.time < dashHangEndTime)
            return 0f;

        float currentGravity = gravity;

        // sliding in the air pulls us down harder, but only once we are already falling (so jumping and sliding doesn't cut the rise short)
        // checked every step, so it is gone the moment the slide ends
        if (sliding && verticalSpeed < 0f)
            currentGravity *= slideAirGravityMultiplier;

        return currentWall.valid ? currentGravity * wallSlipGravityScale : currentGravity;
    }

    // ============================================================
    // COLLISION QUERIES
    // ============================================================

    private void GetCapsulePoints(Vector3 pos, out Vector3 p0, out Vector3 p1, out float radius)
    {
        Vector3 scale = transform.lossyScale;

        // capsule radius must account for object scaling
        radius = capsule.radius * Mathf.Max(scale.x, scale.z);

        float height = Mathf.Max(capsule.height * scale.y, radius * 2f);

        Vector3 center = pos + Vector3.Scale(capsule.center, scale);

        // distance from capsule center to each sphere center
        float half = height * 0.5f - radius;

        p0 = center + Vector3.down * half;
        p1 = center + Vector3.up * half;
    }

    private bool IsSelf(Collider otherCollider)
    {
        // ignore our own collider and child colliders
        return otherCollider == null ||
               otherCollider == capsule ||
               otherCollider.transform.IsChildOf(transform);
    }

    // a surface is walkable when it is tilted less than maxSlopeAngle from straight up
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

        // sweep our capsule through space
        // think "what would we hit if the player moved this direction?"
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

        // CapsuleCastNonAlloc is not guaranteed to return sorted hits, so manually find the nearest collision
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = hitBuffer[i];

            if (IsSelf(candidate.collider))
                continue;

            // weird zero-distance hits usually mean we already overlap
            // Depenetrate() handles those separately
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

    // capsule edges can produce misleading normals near corners
    // this attempts to retrieve the actual face normal instead
    private Vector3 ResolveGroundNormal(RaycastHit hit)
    {
        // already walkable: trust the capsule result
        if (IsWalkable(hit.normal))
            return hit.normal;

        // ray straight downward from slightly above the contact
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
        // repeat a few times because resolving one overlap can sometimes push us into another surface
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
                    // push ourselves barely outside the collider
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

    // core collide-and-slide algorithm

    // basic idea:

    // 1. try moving
    // 2. find first surface hit
    // 3. move right up to it
    // 4. remove movement INTO the wall
    // 5. continue using the remaining sideways motion

    // repeat several times for corners / multiple surfaces
    private void MoveAndSlide(Vector3 motion, ref Vector3 calculatedMovement, bool allowStep)
    {
        Vector3 remainingMotion = motion;

        // null until we have hit a first surface
        Vector3? previousNormal = null;

        for (int bump = 0; bump < maxBumps; bump++)
        {
            float distance = remainingMotion.magnitude;

            if (distance < MinMoveDistance)
                break;

            Vector3 direction = remainingMotion / distance;

            // nothing ahead: perform entire remaining movement
            if (!SweepCapsule(position, direction, distance + skinWidth, out RaycastHit hit))
            {
                position += remainingMotion;
                break;
            }

            contactsThisStep.Add(hit.collider);

            if (hit.collider.TryGetComponent(out SpeedCapSurface hazard))
                surfaceTouches.Add(new SurfaceTouch { surface = hazard, normal = hit.normal });

            // move as far as safely possible, keep whatever motion remains
            remainingMotion = MoveUpToHit(hit, direction, distance);

            Rigidbody hitBody = hit.collider.attachedRigidbody;

            bool dynamicBody =
                hitBody != null &&
                !hitBody.isKinematic;

            if (dynamicBody)
                PushBody(hitBody, hit, direction, calculatedMovement);

            Vector3 surfaceNormal = hit.normal;

            bool walkable = IsWalkable(surfaceNormal);

            // try stepping over low obstacles instead of sliding into them
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

    // moves us right up to the surface, leaving a small gap
    // returns whatever motion is still left over
    private Vector3 MoveUpToHit(RaycastHit hit, Vector3 direction, float distance)
    {
        // move slightly short of the collision  More math time
        
        // dot(direction, normal) tells us how directly we're approaching the surface
        
        // head-on collision:
        // dot ~= -1 ish
        
        // grazing collision:
        // dot ~= 0 ish
        // more grazing -> slightly larger backoff needed
        float backOffDistance = Mathf.Min(skinWidth / Mathf.Max(0.05f, -Vector3.Dot(direction, hit.normal)), skinWidth * 4f);

        float safeDistance = Mathf.Clamp(hit.distance - backOffDistance, 0f, distance);

        // move as far as safely possible
        position += direction * safeDistance;

        // keep whatever motion remains
        return direction * (distance - safeDistance);
    }

    // removes the part of our motion and velocity that points INTO the surface
    private void SlideAlongSurface(
        Vector3 surfaceNormal,
        Vector3? previousNormal,
        ref Vector3 remainingMotion,
        ref Vector3 calculatedMovement)
    {
        // surface faces upward but is too steep to walk on. anything within wallMaxNormalY of vertical counts as a WALL, not a slope, so a tiny float error in a wall's normal (Chisel meshes, cylinder facets) can't kill our upward motion
        bool steepUpward =
            !IsWalkable(surfaceNormal) &&
            surfaceNormal.y > wallMaxNormalY;

        // slide equation:

        // remaining_parallel =
        // remaining - normal * dot(remaining, normal)

        // the perpendicular "into wall" component disappears
        Vector3 slidMotion = Vector3.ProjectOnPlane(remainingMotion, surfaceNormal);

        // never magically climb walls that are too steep
        if (steepUpward && slidMotion.y > 0f)
            slidMotion.y = 0f;

        // a floor seaming into a walkable ramp (or ramp into another ramp) registers as TWO hits in one step - the old surface's edge, then the new one - which looks like being wedged in a
        // corner even though it's still just ground changing angle. real corners (walls, ceilings)
        // should still wedge normally; only skip it here while sliding across walkable-to-walkable
        // ground, so the single-surface redirect below (which preserves speed) handles it instead
        bool wedgedBetweenWalkableGround =
            sliding &&
            IsWalkable(surfaceNormal) &&
            previousNormal.HasValue &&
            IsWalkable(previousNormal.Value);

        if (previousNormal.HasValue &&
            Vector3.Dot(slidMotion, previousNormal.Value) < -0.0001f &&
            !wedgedBetweenWalkableGround)
        {
            // we hit two surfaces and are effectively wedged
            SlideAlongCrease(previousNormal.Value, surfaceNormal, remainingMotion, ref slidMotion, ref calculatedMovement);
        }
        else if (Vector3.Dot(calculatedMovement, surfaceNormal) < 0f)
        {
            float speedBeforeRedirect = calculatedMovement.magnitude;

            // if velocity points INTO the wall, remove its wall-normal component
            calculatedMovement = Vector3.ProjectOnPlane(calculatedMovement, surfaceNormal);

            // sliding onto a walkable ramp: keep our speed, just redirect it along the new surface
            // a plain projection bleeds off cos(angle) of our speed on contact (~23% on a 40 degree ramp), which reads as an instant wall hit even though we're still on the ground and still sliding. normal running doesn't need this - groundAccel re-ramps you back up fast enough to hide it - but a slide has no re-acceleration, so the loss is permanent and obvious. actual walls (not walkable) still bleed speed exactly as before
            if (sliding && IsWalkable(surfaceNormal) && calculatedMovement.sqrMagnitude > 0.0001f)
            {
                // after the projection above, calculatedMovement is the plain along-the-surface speed
                float tangentSpeed = calculatedMovement.magnitude;

                // the speed that was pointing INTO the surface. keeping it is what stops a ramp transition from bleeding speed
                float extraSpeed = speedBeforeRedirect - tangentSpeed;

                // grounded still holds last step's value here (Jump and launches clear it, UpdateGrounded runs later), so false means we are landing from the air
                // ground to ground keeps all of it. landing from the air only keeps a capped fraction of the fall speed, otherwise a tall drop turns into a huge horizontal boost
                if (!grounded)
                    extraSpeed = Mathf.Min(extraSpeed * slideLandingBoostScale, slideLandingBoostMax);

                calculatedMovement = calculatedMovement.normalized * (tangentSpeed + extraSpeed);
            }

            if (steepUpward && calculatedMovement.y > 0f)
                calculatedMovement.y = 0f;
        }

        remainingMotion = slidMotion;
    }

    // wedged between two surfaces: only keep motion along the line where they meet

    // their cross product creates a vector pointing along the line where the two planes intersect

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

            // only retain motion along the crease
            slidMotion = crease * Vector3.Dot(remainingMotion, crease);

            calculatedMovement = crease * Vector3.Dot(calculatedMovement, crease);
        }
        else
        {
            // parallel/opposing planes:
            // nowhere meaningful left to move
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

        // step test is basically:

        // 1. can I move UP?
        // 2. can I move FORWARD?
        // 3. can I move DOWN onto walkable ground?

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

        // must land on a walkable surface
        if (!IsWalkable(ResolveGroundNormal(floorHit)))
            return false;

        float droppedDistance = Mathf.Max(floorHit.distance - skinWidth, 0f);

        // actual height of the step:

        // gainedHeight = amountRaised - amountDropped
        float heightGained = raiseHeight - droppedDistance;

        if (heightGained < MinStepHeightGained ||
            heightGained > stepHeight + StepHeightTolerance)
        {
            return false;
        }

        position = forwardPosition + Vector3.down * droppedDistance;

        // preserve whatever forward motion remains
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

        // assume airborne until proven grounded
        ClearGroundState();

        // jump explicitly disables grounding this frame
        if (leftGround)
            return;

        // ramp launching:

        // if our slope-projected velocity points upward and the old slope was sufficiently steep, we should continue through the air instead of being snapped downward
        bool launching =
            wasGrounded &&
            calculatedMovement.y > MinLaunchUpSpeed &&
            previousSlope >= launchSlopeAngle;

        bool snapping =
            wasGrounded &&
            !launching;

        // since the capsule touches angled ground at an offset, skinWidth / normal.y roughly converts the desired perpendicular skin gap into the needed vertical clearance
        float probeDistance = skinWidth / minGroundNormalY + groundProbeDistance;

        // only reach far downward when we intend to stick to the ground
        if (snapping)
            probeDistance += groundSnapDistance;

        if (!SweepCapsule(position, Vector3.down, probeDistance, out RaycastHit groundHit))
            return;

        Vector3 surfaceNormal = ResolveGroundNormal(groundHit);

        // too steep to stand on
        if (!IsWalkable(surfaceNormal))
            return;

        // dot(calculatedMovement, surfaceNormal) > 0 means velocity is moving AWAY from the surface along its normal

        // so don't instantly reground while launching
        if (!snapping &&
            Vector3.Dot(calculatedMovement, surfaceNormal) > MovingAwaySpeed)
        {
            return;
        }

        // move downward until maintaining our skin gap
        float snapDistance = groundHit.distance - skinWidth / Mathf.Max(surfaceNormal.y, 0.5f);

        if (snapDistance > MinSnapDistance)
            position += Vector3.down * snapDistance;

        grounded = true;
        groundNormal = surfaceNormal;
        groundCollider = groundHit.collider;

        // every step we end on the ground refreshes the coyote window. the moment we stop being grounded this stops updating, so Time.time - lastGroundedTime starts counting up
        lastGroundedTime = Time.time;

        // angle between surface normal and straight up

        // flat floor:
        // angle(up, up) = 0 degrees

        // vertical wall:
        // angle(sideways, up) = 90 degrees
        slopeAngle = Vector3.Angle(surfaceNormal, Vector3.up);

        contactsThisStep.Add(groundHit.collider);

        // record exact landing time for bunny-hop friction grace
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

    // looks for a wall next to us and stores what it finds in currentWall. runs once at the end of every physics step, so TryWallJump (next step) and the debug rays share the same answer

    // why not just use the hits MoveAndSlide already finds? MoveAndSlide only sweeps in the direction we are MOVING. wall jumps want us moving ALONG the wall, so we would almost never actually "hit" it. instead we reuse the same SweepCapsule, but aimed sideways around us
    private void UpdateWallContact()
    {
        // start empty every step so an old wall can never linger after we move away from it
        currentWall = default;

        // walls only matter in the air. a grounded player has the normal ground jump instead
        if (grounded)
            return;

        if (!TryFindWall(out WallContact wall))
            return;

        // work out the tangents and whether we're looking along the wall
        EvaluateWallLook(ref wall);
        currentWall = wall;
    }

    // sweeps the capsule outward in a ring of horizontal directions and picks the closest real wall

    // top-down view, wallProbeDirections = 8 (each arrow is one short capsule sweep):

    //        \  |  /
    //         \ | /
    //      <----(O)---->     O = player capsule
    //         / | \
    //        /  |  \

    // the ring is built from angles, never from world axes, so the wall can face any direction
    // a wall facing between two probes is still caught: the sweep reaches further than the gap we keep from walls (skinWidth), with a bit of room to spare
    private bool TryFindWall(out WallContact wall)
    {
        wall = default;

        bool found = false;
        float closestDistance = float.MaxValue;
        RaycastHit closestHit = default;

        // fewer than 4 probes would leave big blind spots
        int directions = Mathf.Max(wallProbeDirections, 4);

        for (int i = 0; i < directions; i++)
        {
            // spin "forward" around the up axis: i = 0 is world forward, then evenly around the circle
            Vector3 direction = Quaternion.Euler(0f, 360f * i / directions, 0f) * Vector3.forward;

            // skinWidth is added because we already hold that gap away from surfaces, so wallDetectDistance means "how far past our normal gap" we still count
            if (!SweepCapsule(position, direction, wallDetectDistance + skinWidth, out RaycastHit hit))
                continue;

            // only the closest hit matters, and only if it is actually wall-shaped (this skips floors, ceilings and walkable slopes we happened to graze)
            if (hit.distance >= closestDistance || !IsWallNormal(hit.normal))
                continue;

            closestHit = hit;
            closestDistance = hit.distance;
            found = true;
        }

        if (!found)
            return false;

        // the capsule's own hit normal can be wrong near corners and edges (it may report the direction toward the capsule's rounded end). ask the surface itself instead
        Vector3 normal = ResolveWallNormal(closestHit);

        // re-check with the corrected normal: the better answer might not be a wall after all
        if (!IsWallNormal(normal))
            return false;

        // flatten: we only care which HORIZONTAL way the wall faces
        // a normal with no horizontal part is a floor or ceiling - not a wall
        Vector3 flatNormal = Flatten(normal);

        if (flatNormal.sqrMagnitude < 0.0001f)
            return false;

        flatNormal.Normalize();

        wall.valid = true;
        wall.normal = flatNormal;
        wall.point = closestHit.point;
        wall.collider = closestHit.collider;

        // the direction ALONG the wall: the normal turned 90 degrees around the up axis

        // cross(up, normal) is perpendicular to both, so it lies flat in the wall's plane:

        //    wall normal = (0, 0, 1)  ->  tangent = cross(up, normal) = (1, 0, 0)

        // it works for walls rotated to ANY angle, which is the whole reason we use a cross product instead of hardcoding "left/right" in world space
        // the opposite way along the wall is simply -tangent
        wall.tangent = Vector3.Cross(Vector3.up, flatNormal).normalized;

        return true;
    }

    // a wall is a surface that is mostly vertical and not something we could stand on

    // normal.y is 0 for a perfectly vertical wall, 1 for a flat floor, -1 for a flat ceiling, so abs() lets one check reject both floors AND ceilings
    private bool IsWallNormal(Vector3 surfaceNormal)
    {
        return Mathf.Abs(surfaceNormal.y) <= wallMaxNormalY && !IsWalkable(surfaceNormal);
    }

    // same idea as ResolveGroundNormal: capsule hits can lie about the normal near edges, so shoot a plain ray at the contact point and use the surface normal it reports
    private Vector3 ResolveWallNormal(RaycastHit hit)
    {
        GetCapsulePoints(position, out Vector3 p0, out Vector3 p1, out _);

        // the capsule's center line runs p0 -> p1. find the point on it closest to the contact
        // (a "closest point on a segment" projection, clamped so it stays between p0 and p1)
        // shooting from there makes the ray travel straight at the wall at the contact's height
        Vector3 axis = p1 - p0;

        float along = axis.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(hit.point - p0, axis) / axis.sqrMagnitude)
            : 0f;

        Vector3 origin = p0 + axis * along;

        Vector3 toContact = hit.point - origin;
        float distance = toContact.magnitude;

        // contact is on top of our center line: no direction to shoot in, trust the capsule
        if (distance < 0.0001f)
            return hit.normal;

        // overshoot by a little so the ray reliably reaches the surface
        // must hit the SAME collider, or we might read a different object's normal
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

    // fills in lookAngle / forwardTangent / lookOk from where we are facing RIGHT NOW

    // top-down, wall along the bottom, tolerance = 30:

    //              (normal points up, away from the wall)
    //                      ^
    //                      |
    //     ok cone  \       |       /  ok cone
    //     (-tangent) \     |     /    (+tangent)
    //   <---------------- wall ---------------->

    // the ok zone is a cone of +/- tolerance around EACH direction along the wall, so with 30 degrees that is two 60 degree cones, one per direction

    // rules:
    //  - Only our flat (yaw) direction counts. looking up or down changes nothing
    //  - We compare against the wall's tangent and -tangent, and keep the closer one
    //    we never compare against the normal: the spec wants "looking ALONG the wall"
    private void EvaluateWallLook(ref WallContact wall)
    {
        // same flat look direction WishDirection uses for "forward"
        Vector3 look = Flatten(transform.forward);

        // no wall, or looking dead up/down so there is no flat direction: nothing to compare
        // 180 is the worst possible angle, so this can never pass
        if (!wall.valid || look.sqrMagnitude < 0.0001f)
        {
            wall.lookAngle = 180f;
            wall.lookOk = false;
            wall.forwardTangent = wall.tangent;
            return;
        }

        look.Normalize();

        // Angle() is always 0-180, between two directions, so we need both ways along the wall
        // example: facing 20 degrees off +tangent is 160 degrees off -tangent. take the smaller
        float anglePositive = Vector3.Angle(look, wall.tangent);
        float angleNegative = Vector3.Angle(look, -wall.tangent);

        bool facingPositive = anglePositive <= angleNegative;

        // forwardTangent is "the way along the wall we're heading" - the wall jump boosts us that way, and a future wall run would run that way too
        wall.forwardTangent = facingPositive ? wall.tangent : -wall.tangent;
        wall.lookAngle = facingPositive ? anglePositive : angleNegative;
        wall.lookOk = wall.lookAngle <= wallJumpLookTolerance;
    }

    // ============================================================
    // PUSHING
    // ============================================================

    private void PushBody(Rigidbody body, RaycastHit hit, Vector3 moveDirection, Vector3 calculatedMovement)
    {
        // only push horizontally
        Vector3 pushDirection = Flatten(moveDirection);

        if (pushDirection.sqrMagnitude < 0.000001f)
            return;

        pushDirection.Normalize();

        Vector3 playerVelocity = calculatedMovement + currentPlatformVelocity;

        // relative closing velocity:

        // closingSpeed =
        // dot(playerVelocity - objectVelocity, pushDirection)

        // positive means we're moving INTO the object
        float closingSpeed = Vector3.Dot(playerVelocity - body.GetPointVelocity(hit.point), pushDirection);

        if (closingSpeed <= 0f)
            return;

        // reduced mass:

        // μ = (m1 * m2) / (m1 + m2)

        // this gives realistic collision response between two objects of different mass
        float reducedMass = (playerMass * body.mass) / (playerMass + body.mass);

        // impulse:

        // j = μ * relativeVelocity

        // impulse changes momentum immediately:

        // j = Δp = m * Δv
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

        // platformLocalPos stores our anchor relative to the platform

        // TransformPoint converts that old local anchor into its NEW world position

        // therefore:

        // platformDelta = newAnchorWorldPosition - playerPosition
        Vector3 platformDelta = platform.TransformPoint(platformLocalPos) - position;

        if (platformDelta.sqrMagnitude < 0.0000000001f)
            return Vector3.zero;

        Vector3 discardedMovement = Vector3.zero;

        // carry us with the platform while still respecting collision
        MoveAndSlide(platformDelta, ref discardedMovement, false);

        // velocity equation:

        // velocity = displacement / time

        // v = Δx / Δt
        return platformDelta / dt;
    }

    private void RotateWithPlatform()
    {
        // only inherit yaw from mostly upright platforms
        if (Vector3.Dot(platform.up, Vector3.up) > PlatformUprightDot)
        {
            // rotation since last frame:

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
        // only Rigidbody-backed ground can carry us
        Rigidbody groundBody = null;

        if (grounded && groundCollider != null)
            groundBody = groundCollider.attachedRigidbody;

        if (groundBody == null)
        {
            platform = null;
            return;
        }

        platform = groundBody.transform;

        // convert player world position into platform-local coordinates

        // this anchor will move naturally when the platform translates or rotates
        platformLocalPos = platform.InverseTransformPoint(position);

        platformLastRot = platform.rotation;
    }

    // ============================================================
    // HAZARD SURFACES
    // ============================================================

    // adds the hazards we are standing on or next to (the collide and slide hits were added as they happened)
    private void CollectSurfaceTouches()
    {
        if (grounded && groundCollider != null && groundCollider.TryGetComponent(out SpeedCapSurface floor))
            surfaceTouches.Add(new SurfaceTouch { surface = floor, normal = groundNormal });

        if (currentWall.valid && currentWall.collider != null && currentWall.collider.TryGetComponent(out SpeedCapSurface wall))
            surfaceTouches.Add(new SurfaceTouch { surface = wall, normal = currentWall.normal });
    }

    // what touching a SpeedCapSurface does, for the touches the last step collected:
    //   1. caps our flat speed at its maxSpeed. speed AWAY from a wall is left alone, so a bounce off an electric wall isn't squashed, but running along it is
    //   2. lowers airSpeedCap to it. airSpeedCap only refreshes when we are on the ground, so after leaving the hazard in the air we cannot get faster until we land
    //   3. on the FIRST touch of a hazard, bounces us along its normal: our speed along the normal becomes at least bounceSpeed (assigned, like a jump)

    // runs after Move() so the movement model can't speed us back up, and before MoveAndSlide
    // returns true if the bounce threw us off the ground (like a jump does)
    private bool ApplySurfaceHazards(ref Vector3 calculatedMovement)
    {
        bool leftGround = false;
        bool bounced = false;

        foreach (SurfaceTouch touch in surfaceTouches)
        {
            if (touch.surface == null)
                continue;

            float maxSpeed = touch.surface.MaxSpeed;
            Vector3 flat = Flatten(calculatedMovement);

            // for a wall, the part of our speed pointing away from it is exempt from the cap
            Vector3 away = Vector3.zero;
            Vector3 awayDirection = Flatten(touch.normal);

            if (!IsWalkable(touch.normal) && awayDirection.sqrMagnitude > 0.0001f)
            {
                awayDirection.Normalize();
                away = awayDirection * Mathf.Max(0f, Vector3.Dot(flat, awayDirection));
            }

            Vector3 rest = flat - away;

            if (rest.magnitude > maxSpeed)
                rest = rest.normalized * maxSpeed;

            flat = rest + away;
            calculatedMovement.x = flat.x;
            calculatedMovement.z = flat.z;

            airSpeedCap = Mathf.Min(airSpeedCap, maxSpeed);
        }

        foreach (SurfaceTouch touch in surfaceTouches)
        {
            // only the FIRST touch of a surface counts: it announces itself, and bounces us
            if (touch.surface == null || surfacesTouchedLastStep.Contains(touch.surface) || !surfacesAnnouncedThisStep.Add(touch.surface))
                continue;

            HazardTouched?.Invoke(touch.surface);

            if (bounced || touch.surface.BounceSpeed <= 0f)
                continue;

            bounced = true;

            float speedAway = Vector3.Dot(calculatedMovement, touch.normal);

            if (speedAway < touch.surface.BounceSpeed)
                calculatedMovement += touch.normal * (touch.surface.BounceSpeed - speedAway);

            if (IsWalkable(touch.normal))
            {
                // thrown off the floor on purpose: no coyote jump, same as a jump or a launch
                grounded = false;
                lastGroundedTime = NeverGrounded;
                leftGround = true;
            }
            else
            {
                // we just got thrown off this wall, so it must not count as touching it this step (no slip gravity), same as after a wall jump
                currentWall = default;
            }
        }

        surfacesAnnouncedThisStep.Clear();
        surfacesTouchedLastStep.Clear();

        foreach (SurfaceTouch touch in surfaceTouches)
        {
            if (touch.surface != null)
                surfacesTouchedLastStep.Add(touch.surface);
        }

        surfaceTouches.Clear();

        return leftGround;
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

            // only fire contact when first touching the object
            if (contact.TryGetComponent(out IPlayerContact receiver))
                receiver.OnPlayerContact(this);
        }

        // current contacts become next frame's previous contacts
        contactsLastStep.Clear();

        foreach (Collider contact in contactsThisStep)
            contactsLastStep.Add(contact);

        contactsThisStep.Clear();
    }

    public void SetVelocity(Vector3 worldVelocity)
    {
        // external systems like launch pads can directly set velocity
        velocity = worldVelocity;

        grounded = false;
        platform = null;

        // launched by something else (launch pad): no coyote jump
        lastGroundedTime = NeverGrounded;
    }

    public void Teleport(Vector3 worldPosition)
    {
        // synchronize every representation of position so the controller doesn't snap back afterward
        position = worldPosition;
        lastTarget = worldPosition;

        velocity = Vector3.zero;

        grounded = false;
        platform = null;

        // teleporting doesn't count as walking off an edge
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
        if (!showDebugRays)
            return;

        // green = ground normal
        Debug.DrawRay(position + Vector3.up * 0.1f, groundNormal * 1.5f, Color.green);

        // cyan = velocity
        Debug.DrawRay(position + Vector3.up, velocity * 0.15f, Color.cyan);

        DrawWallDebugRays();
    }

    // shows why a wall jump would be accepted or rejected. reads currentWall, same as the jump does, but re-checks the look so the colours follow the mouse between physics steps
    private void DrawWallDebugRays()
    {
        if (!currentWall.valid)
            return;

        WallContact wall = currentWall;
        EvaluateWallLook(ref wall);

        Vector3 origin = position + Vector3.up * 1f;
        Color verdict = wall.lookOk ? Color.green : Color.red;

        // magenta = wall normal
        Debug.DrawRay(origin, wall.normal * 1.5f, Color.magenta);

        // the tangent we're facing is green (passes) or red (fails); the other one is yellow
        Debug.DrawRay(origin, wall.forwardTangent * 2f, verdict);
        Debug.DrawRay(origin, -wall.forwardTangent * 2f, Color.yellow);

        // edges of the allowed cone around the facing tangent, in white
        Quaternion tolerance = Quaternion.AngleAxis(wallJumpLookTolerance, Vector3.up);
        Quaternion toleranceBack = Quaternion.AngleAxis(-wallJumpLookTolerance, Vector3.up);
        Debug.DrawRay(origin, tolerance * wall.forwardTangent * 1.5f, Color.white);
        Debug.DrawRay(origin, toleranceBack * wall.forwardTangent * 1.5f, Color.white);

        // blue = our flattened look direction
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
            string verdictText = wall.lookOk ? "OK" : (wallJumpAnyDirection ? "jump only (any direction)" : "blocked");
            wallText = $"{verdictText}  look angle: {wall.lookAngle:F0} deg";
        }

        GUI.Label(
            new Rect(10f, 10f, 420f, 240f),
            $"speed: {velocity.magnitude:F1}   horizontal: {horizontalVelocity.magnitude:F1}\n" +
            $"velocity: {velocity}\n" +
            $"grounded: {grounded}   slope: {slopeAngle:F0} deg\n" +
            $"sliding: {sliding}   crouching: {crouching}\n" +
            $"platform: {platformName}\n" +
            $"coyote left: {Mathf.Max(0f, coyoteTime - (Time.time - lastGroundedTime)):F2}s (window {coyoteTime:F2}s)\n" +
            $"wall: {wallText}\n" +
            $"wall run: {wallRunning}   grace left: {Mathf.Max(0f, wallRunGracePeriod - wallRunTimeUsed):F2}s\n" +
            $"dash cooldown left: {Mathf.Max(0f, dashCooldown - (Time.time - lastDashTime)):F2}s"
        );
    }
}
;