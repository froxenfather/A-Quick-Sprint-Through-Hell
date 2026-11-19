using UnityEngine;

// Can you feel your sins crawling down your back?
[RequireComponent(typeof(SphereCollider))]
public class Conseraph : MonoBehaviour
{
    [Header("AI")]
    [SerializeField] private bool aiEnabled = true;

    [Header("Distances (meters)")]
    // further from the player than this, his speed climbs and climbs until he is back inside it
    [Min(0f)]
    [SerializeField] private float catchUpDistance = 150f;

    // closer than this he slows right down
    [Min(0f)]
    [SerializeField] private float closeDistance = 60f;

    [Header("Speeds (meters per second)")]
    // his speed between closeDistance and catchUpDistance
    [Min(0f)]
    [SerializeField] private float normalSpeed = 25f;

    // his speed closer than closeDistance
    [Min(0f)]
    [SerializeField] private float closeSpeed = 20f;

    [Header("Catching up")]
    // how fast his speed grows each second while he is further than catchUpDistance.
    // 1 means it doubles about every 0.7 seconds
    [Min(0f)]
    [SerializeField] private float catchUpGrowth = 1f;

    // he never goes faster than this
    [Min(0f)]
    [SerializeField] private float maxCatchUpSpeed = 80f;

    [Header("Changing speed (meters per second, every second)")]
    // how fast his speed rises to its target once he is inside catchUpDistance
    [Min(0f)]
    [SerializeField] private float acceleration = 20f;

    // how fast his speed drops to its target.
    // a very high number is nearly instant
    [Min(0f)]
    [SerializeField] private float slowdownRate = 100f;

    [Header("Run start")]
    // when the run starts (the player leaves a safe room) he is put this far straight below the player (meters).
    // from there he rushes up, since he is far further than catchUpDistance
    [Min(0f)]
    [SerializeField] private float startDistanceBelow = 1500f;

    [Header("Fade")]
    // how long he takes to fade in or out when the timer starts or stops
    [Min(0.01f)]
    [SerializeField] private float fadeSeconds = 1f;

    [Header("Enraged")]
    // when the timer reaches 00:00 he becomes enraged.
    // his speed drops to Enraged Start Speed and then climbs with no top, and the screen turns red.
    // it ends when the timer says you're good.
    // untick this to turn all of that off
    [SerializeField] private bool enragedEnabled = true;

    // his speed the moment he becomes enraged
    [Min(0f)]
    [SerializeField] private float enragedStartSpeed = 1f;

    // how much faster he gets each step
    [Min(0f)]
    [SerializeField] private float enragedSpeedStep = 1f;

    // how often he gets faster (seconds).
    // 0.2 is five times a second
    [Min(0.01f)]
    [SerializeField] private float enragedStepSeconds = 0.2f;

    // how long the screen takes to turn red, and to clear again (seconds)
    [Min(0.01f)]
    [SerializeField] private float redFadeSeconds = 1f;

    // how red the screen gets at the most.
    // 1 turns the whole picture red, lower keeps part of the real picture showing through the red, and 0 is no red at all.
    // it is read every frame, so you can drag it during Play mode and see the change straight away
    [Range(0f, 1f)]
    [SerializeField] private float rageIntensity = 1f;

    [Header("Parry")]
    // while he is enraged, a dash straight at him makes you immune to him for a moment.
    // if he touches you in that moment he is parried instead of killing you: he is thrown back the way he came and slows to a stop, and you are thrown the way he was moving at the same speed.
    // untick this to turn the parry off
    [SerializeField] private bool parryEnabled = true;

    // how fast you must be going (m/s) when you dash for it to parry.
    // it is your flat speed just before the dash gives its own boost, the same number as the speedometer.
    // a dash below this gives no immunity, so he kills you as normal.
    // 0 lets any dash parry
    [Min(0f)]
    [SerializeField] private float parryMinSpeed = 26f;

    // how far from the middle of the screen (degrees) he can be when you dash and still count as looked at
    [Range(0f, 90f)]
    [SerializeField] private float lookToleranceDegrees = 20f;

    // how far (degrees, sideways) the dash can point away from him and still count as straight at him
    [Range(0f, 90f)]
    [SerializeField] private float dashToleranceDegrees = 25f;

    // how long you stay immune after the dash glide ends (seconds).
    // the glide itself is the player's dash hang time
    [Min(0f)]
    [SerializeField] private float parryGraceSeconds = 0.5f;

    // how long he takes to stop after being parried (seconds).
    // he is thrown back at the speed he had and slows in a straight line to 0 over this time, so the faster he was, the further he goes.
    // then his speed starts over and the enraged climb with it
    [Min(0.01f)]
    [SerializeField] private float knockbackSeconds = 1f;

    // how many times harder he is thrown.
    // 1 throws him at the speed he had, 5 throws him five times as fast, so five times as far in the same time.
    // it only changes him, the player is still thrown at his real speed
    [Min(0f)]
    [SerializeField] private float knockbackMultiplier = 1f;

    // plays once, from the speakers and not from him, when he starts to fly back.
    // with the freeze frame (ParryImpact on him) that is when the freeze ends, otherwise it is the moment the parry works.
    // leave empty for no sound
    [SerializeField] private AudioClip parrySound;

    [Range(0f, 1f)]
    [SerializeField] private float parryVolume = 1f;

    // these two are only here to watch while you tune him in Play mode
    [Header("Live (read only)")]
    [SerializeField] private float currentSpeed;
    [SerializeField] private float distanceToPlayer;

    // raised each time he touches the player while he is hunting.
    // PlayerDeath listens to this and handles the kill
    public event System.Action TouchedPlayer;

    // raised the moment the player parries him.
    // ParryImpact listens to this for the freeze frame
    public event System.Action Parried;

    private CustomPlayerController player;
    private CapsuleCollider playerCapsule;
    private CountdownTimerHUD timer;
    private bool hasTimer;

    // the room chain tells him when the run starts, so he can be put below the player
    private RoomChain roomChain;

    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private float visibility;

    // the global number the red full-screen shader reads
    private static readonly int RageAmountId = Shader.PropertyToID("_RageAmount");

    private bool enraged;
    private float enragedElapsed;

    // 0 to 1: how red the screen is right now
    private float rage;

    // closer than this sideways (meters) there is no sideways direction to the player, so the dash can't be off by an angle
    private const float MinFlatAimDistance = 0.5f;

    // the player's camera, to tell whether they are looking at him
    private Camera playerCamera;
    private bool hasCamera;

    // a flat 2D source for the parry sound, made at the start if there is a sound
    private AudioSource parrySource;

    // true from the parry until he really starts to fly, which is when its sound plays.
    // a freeze frame holds time at 0, so that can be a moment after the parry
    private bool parrySoundPending;

    // seconds of parry immunity left, 0 when there is none
    private float parryTimeLeft;

    // true while he is flying back from a parry
    private bool knockedBack;
    private Vector3 knockDirection;
    private float knockSpeed;
    private float knockElapsed;

    // the way he moved last, as a unit vector.
    // a parry throws him the opposite way
    private Vector3 lastMoveDirection = Vector3.up;

    // the middle of the player's capsule, not their feet or their eyes.
    // it is the same point ConseraphVisuals turns to face
    private Vector3 PlayerCenter => player.transform.TransformPoint(playerCapsule.center);
    
    public bool Hunting => !aiEnabled || !hasTimer || timer.IsCounting;

    // 1 is fully visible, 0 is invisible
    public float Visibility => visibility;

    public float DistanceToPlayer => Vector3.Distance(transform.position, PlayerCenter);

    public float Speed => currentSpeed;

    // the two distances his speed depends on, so other scripts (the heartbeat) can follow them
    public float CatchUpDistance => catchUpDistance;
    public float CloseDistance => closeDistance;

    // true from the moment the timer reaches 00:00 until it says you're good again
    public bool Enraged => enraged;

    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;

        // start with a clear screen, whatever a last run left behind
        Shader.SetGlobalFloat(RageAmountId, 0f);

        player = FindFirstObjectByType<CustomPlayerController>();

        if (player == null)
        {
            Debug.LogWarning("Conseraph can't find a CustomPlayerController, so he is switched off.", this);
            enabled = false;
            return;
        }

        // CustomPlayerController requires a capsule collider, so it is always there
        playerCapsule = player.GetComponent<CapsuleCollider>();

        timer = FindFirstObjectByType<CountdownTimerHUD>();
        hasTimer = timer != null;

        roomChain = FindFirstObjectByType<RoomChain>();

        // the camera sits under the player, so he can tell whether the player is looking at him
        playerCamera = player.GetComponentInChildren<Camera>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        hasCamera = playerCamera != null;

        if (parryEnabled && !hasCamera)
            Debug.LogWarning("Conseraph can't find the player's camera, so the parry is switched off.", this);

        if (parrySound != null)
        {
            parrySource = gameObject.AddComponent<AudioSource>();
            parrySource.playOnAwake = false;
            parrySource.spatialBlend = 0f;
        }

        if (aiEnabled && !hasTimer)
            Debug.LogWarning("Conseraph can't find the countdown timer in this scene, so he hunts from the start. Untick Ai Enabled to keep him still.", this);

        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].color;

        // start in the right state with no fade
        visibility = Hunting ? 1f : 0f;
        ApplyVisibility();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        Vector3 target = PlayerCenter;
        float distance = Vector3.Distance(transform.position, target);

        distanceToPlayer = distance;

        bool hunting = Hunting;

        UpdateVisibility(hunting, deltaTime);
        UpdateEnraged(deltaTime);
        UpdateRage(deltaTime);

        // the immunity from a dash runs out
        if (parryTimeLeft > 0f)
            parryTimeLeft = Mathf.Max(0f, parryTimeLeft - deltaTime);

        if (!aiEnabled || !hunting)
        {
            currentSpeed = 0f;
            CancelParry();
        }
        else if (knockedBack)
            Knockback(deltaTime);
        else if (enraged)
            Rampage(target, deltaTime);
        else
            Chase(target, distance, deltaTime);
    }

    private void OnEnable()
    {
        if (roomChain != null)
            roomChain.RunStarted += OnRunStarted;

        if (player != null)
            player.Dashed += OnPlayerDashed;
    }

    private void OnDisable()
    {
        if (roomChain != null)
            roomChain.RunStarted -= OnRunStarted;

        if (player != null)
            player.Dashed -= OnPlayerDashed;

        // never leave the screen red
        Shader.SetGlobalFloat(RageAmountId, 0f);
    }

    // the run starts, at the first safe room or a later one.
    // he is put far below the player and rushes up from there, visible and hunting because the timer is counting by now
    private void OnRunStarted()
    {
        transform.position = PlayerCenter + Vector3.down * startDistanceBelow;
        currentSpeed = 0f;
        CancelParry();
    }

    // enraged from the moment the timer reaches 00:00 until it says you're good again.
    // with the AI off he never moves, so he is never enraged
    private void UpdateEnraged(float deltaTime)
    {
        bool shouldBeEnraged = enragedEnabled && aiEnabled && hasTimer && timer.IsFinished && timer.IsCounting;

        if (shouldBeEnraged && !enraged)
        {
            enraged = true;
            enragedElapsed = 0f;
        }
        else if (!shouldBeEnraged && enraged)
        {
            // back to normal wherever he is.
            // the usual distance speeds take over from his normal speed
            enraged = false;
            currentSpeed = normalSpeed;

            // the parry only exists while he is enraged
            CancelParry();
        }

        if (enraged)
            enragedElapsed += deltaTime;
    }

    // the screen goes red while he is enraged.
    // the full-screen shader reads this as a global number, so no material has to be changed
    private void UpdateRage(float deltaTime)
    {
        float goal = enraged ? 1f : 0f;

        rage = Mathf.MoveTowards(rage, goal, deltaTime / redFadeSeconds);

        // sent every frame, so a change to Rage Intensity in the Inspector shows even while the screen is fully red
        Shader.SetGlobalFloat(RageAmountId, rage * rageIntensity);
    }

    private void UpdateVisibility(bool hunting, float deltaTime)
    {
        float goal = hunting ? 1f : 0f;

        if (Mathf.Approximately(visibility, goal))
            return;

        visibility = Mathf.MoveTowards(visibility, goal, deltaTime / fadeSeconds);
        ApplyVisibility();
    }

    // every part keeps the color it has in the scene, with its alpha scaled by the fade
    private void ApplyVisibility()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = baseColors[i];
            color.a *= visibility;
            renderers[i].color = color;
        }
    }

    private void Chase(Vector3 target, float distance, float deltaTime)
    {
        if (distance > catchUpDistance)
        {
            // exponential: the faster he goes, the faster he speeds up.
            // it starts from at least his normal speed, so he never creeps from a standstill
            float grown = Mathf.Max(currentSpeed, normalSpeed) * Mathf.Exp(catchUpGrowth * deltaTime);
            currentSpeed = Mathf.Min(maxCatchUpSpeed, grown);
        }
        else
        {
            float targetSpeed = distance < closeDistance ? closeSpeed : normalSpeed;
            float rate = currentSpeed < targetSpeed ? acceleration : slowdownRate;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);
        }

        MoveToward(target, deltaTime);
    }

    // enraged: a stepped climb with no top, one step more every enragedStepSeconds.
    // the zone speeds are ignored, they would fight the climb
    private void Rampage(Vector3 target, float deltaTime)
    {
        currentSpeed = enragedStartSpeed + Mathf.Floor(enragedElapsed / enragedStepSeconds) * enragedSpeedStep;

        MoveToward(target, deltaTime);
    }

    // straight at the player in 3D, through walls.
    // he stops exactly on the player and can't go past them, however fast he is
    private void MoveToward(Vector3 target, float deltaTime)
    {
        Vector3 toTarget = target - transform.position;

        // remember which way he is heading, it is his velocity direction for a parry
        if (toTarget.sqrMagnitude > 0f)
            lastMoveDirection = toTarget.normalized;

        transform.position = Vector3.MoveTowards(transform.position, target, currentSpeed * deltaTime);
    }

    // a dash while he is enraged: if it was aimed at him, you are immune for the glide plus the grace time.
    // both checks are made now, at the moment of the dash
    private void OnPlayerDashed()
    {
        if (!parryEnabled || !hasCamera || !enraged)
            return;

        // too slow: the parry needs real speed behind it
        if (player.DashStartSpeed < parryMinSpeed)
            return;

        // looking at him: he is close to the middle of the screen
        Transform cameraTransform = playerCamera.transform;
        Vector3 fromCamera = transform.position - cameraTransform.position;

        if (Vector3.Angle(cameraTransform.forward, fromCamera) > lookToleranceDegrees)
            return;

        // dashing at him: flattened, the dash points at him.
        // straight above or below him there is no sideways direction, so that passes
        Vector3 flatToHim = transform.position - PlayerCenter;
        flatToHim.y = 0f;

        if (flatToHim.sqrMagnitude > MinFlatAimDistance * MinFlatAimDistance && Vector3.Angle(player.DashDirection, flatToHim) > dashToleranceDegrees)
            return;

        parryTimeLeft = player.DashHangTime + parryGraceSeconds;
    }

    // Knuckle sandwich.
    // The way to fight back against the Lie
    // Parries throw both you and him back!
    private void Parry()
    {
        knockSpeed = currentSpeed * knockbackMultiplier;
        knockDirection = -lastMoveDirection;
        knockElapsed = 0f;
        knockedBack = true;
        parryTimeLeft = 0f;

        // the player is thrown exactly the way he was moving, up or down included, at his speed.
        // it replaces their whole velocity
        player.SetVelocity(lastMoveDirection * currentSpeed);

        parrySoundPending = true;

        Parried?.Invoke();
    }

    // flying back from a parry: his speed falls in a straight line to 0 over knockbackSeconds.
    // the distance he covers is knockSpeed * knockbackSeconds / 2.
    // at the end his speed starts over, and so does the enraged climb
    private void Knockback(float deltaTime)
    {
        // time is stopped (the freeze frame): wait.
        // the frame that stopped it still has its old deltaTime, so the scale itself is checked
        if (Time.timeScale <= 0f)
            return;

        // the sound waits for the first step that really moves him, which is the first after the freeze
        if (parrySoundPending && deltaTime > 0f)
        {
            parrySoundPending = false;

            if (parrySource != null)
                parrySource.PlayOneShot(parrySound, parryVolume);
        }

        knockElapsed += deltaTime;

        float progress = Mathf.Clamp01(knockElapsed / knockbackSeconds);
        currentSpeed = knockSpeed * (1f - progress);
        transform.position += knockDirection * currentSpeed * deltaTime;

        if (progress >= 1f)
        {
            knockedBack = false;
            currentSpeed = 0f;
            enragedElapsed = 0f;
        }
    }

    private void CancelParry()
    {
        parryTimeLeft = 0f;
        knockedBack = false;
        parrySoundPending = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTouch(other);
    }

    // he can be harmless while the player is already inside him (the parry, the flight), and no new Enter comes when that ends.
    // so staying inside counts as touching too
    private void OnTriggerStay(Collider other)
    {
        HandleTouch(other);
    }

    private void HandleTouch(Collider other)
    {
        if (!Hunting)
            return;

        if (other.GetComponentInParent<CustomPlayerController>() == null)
            return;

        // dashed at him while he is enraged: he is parried instead of killing
        if (enraged && parryTimeLeft > 0f)
        {
            Parry();
            return;
        }

        // flying back from a parry he can't hurt anyone
        if (knockedBack)
            return;

        TouchedPlayer?.Invoke();
    }
}
