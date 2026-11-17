using UnityEngine;

// the brain of Conseraph: chasing, fading in and out with the timer, and noticing when he touches the player.
// with the AI on he hunts (visible and chasing) while the countdown timer runs, and fades to invisible and stops when the timer says you're good.
// with the AI off he stays where he is, still looking at you, and is always visible.
// put this on the Conseraph root with a trigger collider (the menu A Quick Sprint Through Hell > Set Up Conseraph builds all of it)
[RequireComponent(typeof(SphereCollider))]
public class Conseraph : MonoBehaviour
{
    [Header("AI")]
    // off: he stays where he is and is always visible.
    // on: he hunts while the timer counts down, and is invisible and still while it says you're good
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

    [Header("Fade")]
    // how long he takes to fade in or out when the timer starts or stops
    [Min(0.01f)]
    [SerializeField] private float fadeSeconds = 1f;

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

    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private float visibility;

    // the middle of the player's capsule, not their feet or their eyes.
    // it is the same point ConseraphVisuals turns to face
    private Vector3 PlayerCenter => player.transform.TransformPoint(playerCapsule.center);

    // whether he is out hunting: visible, and able to touch.
    // with the AI off he always is.
    // with no timer in the scene there is nothing to wait for, so he always is
    public bool Hunting => !aiEnabled || !hasTimer || timer.IsCounting;

    // 1 is fully visible, 0 is invisible
    public float Visibility => visibility;

    public float DistanceToPlayer => Vector3.Distance(transform.position, PlayerCenter);

    public float Speed => currentSpeed;

    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;

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

        if (aiEnabled && hunting)
            Chase(target, distance, deltaTime);
        else
            currentSpeed = 0f;
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

        // straight at the player in 3D, through walls
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
