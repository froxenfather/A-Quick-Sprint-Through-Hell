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
    [SerializeField] private float catchUpDistance = 30f;

    // closer than this he slows right down
    [Min(0f)]
    [SerializeField] private float closeDistance = 10f;

    [Header("Speeds (meters per second)")]
    // his speed between closeDistance and catchUpDistance
    [Min(0f)]
    [SerializeField] private float normalSpeed = 10f;

    // his speed closer than closeDistance
    [Min(0f)]
    [SerializeField] private float closeSpeed = 5f;

    [Header("Catching up")]
    // how fast his speed grows each second while he is further than catchUpDistance.
    // 1 means it doubles about every 0.7 seconds
    [Min(0f)]
    [SerializeField] private float catchUpGrowth = 1f;

    // he never goes faster than this
    [Min(0f)]
    [SerializeField] private float maxCatchUpSpeed = 150f;

    [Header("Changing speed (meters per second, every second)")]
    // how fast his speed rises to its target once he is inside catchUpDistance
    [Min(0f)]
    [SerializeField] private float acceleration = 20f;

    // how fast his speed drops to its target.
    // a very high number is nearly instant
    [Min(0f)]
    [SerializeField] private float slowdownRate = 1000f;

    [Header("Run start")]
    // when the run starts (the player leaves a safe room) he is put this far straight below the player (meters).
    // from there he rushes up, since he is far further than catchUpDistance
    [Min(0f)]
    [SerializeField] private float startDistanceBelow = 1000f;

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

    // these two are only here to watch while you tune him in Play mode
    [Header("Live (read only)")]
    [SerializeField] private float currentSpeed;
    [SerializeField] private float distanceToPlayer;

    // raised each time he touches the player while he is hunting.
    // PlayerDeath listens to this and handles the kill
    public event System.Action TouchedPlayer;

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

    // the middle of the player's capsule, not their feet or their eyes.
    // it is the same point ConseraphVisuals turns to face
    private Vector3 PlayerCenter => player.transform.TransformPoint(playerCapsule.center);
    
    public bool Hunting => !aiEnabled || !hasTimer || timer.IsCounting;

    // 1 is fully visible, 0 is invisible
    public float Visibility => visibility;

    public float DistanceToPlayer => Vector3.Distance(transform.position, PlayerCenter);

    public float Speed => currentSpeed;

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

        if (!aiEnabled || !hunting)
            currentSpeed = 0f;
        else if (enraged)
            Rampage(target, deltaTime);
        else
            Chase(target, distance, deltaTime);
    }

    private void OnEnable()
    {
        if (roomChain != null)
            roomChain.RunStarted += OnRunStarted;
    }

    private void OnDisable()
    {
        if (roomChain != null)
            roomChain.RunStarted -= OnRunStarted;

        // never leave the screen red
        Shader.SetGlobalFloat(RageAmountId, 0f);
    }

    // the run starts, at the first safe room or a later one.
    // he is put far below the player and rushes up from there, visible and hunting because the timer is counting by now
    private void OnRunStarted()
    {
        transform.position = PlayerCenter + Vector3.down * startDistanceBelow;
        currentSpeed = 0f;
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
        }

        if (enraged)
            enragedElapsed += deltaTime;
    }

    // the screen goes red while he is enraged.
    // the full-screen shader reads this as a global number, so no material has to be changed
    private void UpdateRage(float deltaTime)
    {
        float goal = enraged ? 1f : 0f;

        if (Mathf.Approximately(rage, goal))
            return;

        rage = Mathf.MoveTowards(rage, goal, deltaTime / redFadeSeconds);
        Shader.SetGlobalFloat(RageAmountId, rage);
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
        transform.position = Vector3.MoveTowards(transform.position, target, currentSpeed * deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Hunting)
            return;

        if (other.GetComponentInParent<CustomPlayerController>() == null)
            return;

        TouchedPlayer?.Invoke();
    }
}
