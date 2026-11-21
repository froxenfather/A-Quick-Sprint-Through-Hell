using UnityEngine;
using UnityEngine.Serialization;

// everything the camera does to make speed and running feel good.
// put this on the Main Camera.
//
//   FOV  the view widens as the player speeds up, and punches wider for a moment when they dash.
//   Bob  a small up-and-down, side-to-side bob of the camera while running, so movement feels like footsteps.
//        it moves the camera itself, not the CameraPivot above it (PlayerVisuals lowers the pivot for slides and crouches, and they would fight).
//        set Bob Amount to 0 to turn it off
[RequireComponent(typeof(Camera))]
public class CameraFeel : MonoBehaviour
{
    // found on the Player above the camera if left empty
    [SerializeField] private CustomPlayerController player;

    // ============================================================
    // FOV
    // ============================================================

    [Header("FOV: speed range")]
    // at or below this speed there is no FOV change (walk speed)
    [FormerlySerializedAs("startSpeed")]
    [Range(0f, 60f)]
    [SerializeField] private float fovStartSpeed = 12f;

    // the speed the FOV reacts up to: at or above this it is the full boost (sprint speed)
    [FormerlySerializedAs("fullSpeed")]
    [Range(0f, 60f)]
    [SerializeField] private float fovFullSpeed = 20f;

    [Header("FOV: boost")]
    [Range(0f, 40f)]
    [SerializeField] private float maxFovBoost = 12f;

    // degrees per second
    [Range(1f, 200f)]
    [SerializeField] private float fovChangeSpeed = 60f;

    [Header("FOV: dash punch")]
    // extra degrees of FOV right after a dash.
    // on top of the speed boost above
    [Range(0f, 60f)]
    [SerializeField] private float dashFovBoost = 25f;

    // how fast the punch opens up (degrees per second).
    // high = nearly instant
    [Range(1f, 2000f)]
    [SerializeField] private float dashFovRiseSpeed = 800f;

    // how fast it settles back (degrees per second).
    // 120 takes about a fifth of a second for the default boost
    [Range(1f, 600f)]
    [SerializeField] private float dashFovFadeSpeed = 120f;

    // ============================================================
    // BOB
    // ============================================================

    // how far the camera moves up and down (meters) at full speed.
    // the sideways sway is half of this
    [Header("Bob")]
    [Min(0f)]
    [SerializeField] private float bobAmount = 0.04f;

    // meters travelled per full step.
    // match Stride Length on PlayerAudio and the bob lands on the footsteps
    [Min(0.5f)]
    [SerializeField] private float bobStrideLength = 3.5f;

    // the flat speed (m/s) at which the bob reaches full Bob Amount
    [Min(1f)]
    [SerializeField] private float bobFullSpeed = 20f;

    // how quickly the bob fades in and out when you start and stop (per second)
    [Min(0.1f)]
    [SerializeField] private float bobBlendSpeed = 8f;

    private Camera cam;
    private float baseFov;

    // the FOV from our speed alone.
    // kept here (not read back from the camera) so the dash punch can sit on top of it without the two fighting
    private float speedFov;

    // the extra degrees from a dash right now, and where it is heading (the boost on the way up, 0 on the way down)
    private float dashPunch;
    private float dashPunchTarget;

    // where the camera sits when it isn't bobbing, how far through the step it is, and how much of the bob is showing (0 to 1)
    private Vector3 restPosition;
    private float bobPhase;
    private float bobWeight;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (player == null)
            player = GetComponentInParent<CustomPlayerController>();

        restPosition = transform.localPosition;
    }

    private void Start()
    {
        baseFov = cam.fieldOfView;
        speedFov = baseFov;
    }

    private void OnEnable()
    {
        if (player != null)
            player.Dashed += OnDashed;
    }

    private void OnDisable()
    {
        if (player != null)
            player.Dashed -= OnDashed;
    }

    private void OnDashed()
    {
        dashPunchTarget = dashFovBoost;
    }

    // camera work runs per frame, not in FixedUpdate
    private void Update()
    {
        if (player == null)
            return;

        UpdateFov();
    }

    // after everything else has moved, so nothing undoes the bob
    private void LateUpdate()
    {
        if (player == null)
            return;

        UpdateBob();
    }

    private void UpdateFov()
    {
        // 0 at walk speed, 1 at sprint speed, clamped so launch pads don't blow the FOV out
        float speedFraction = Mathf.InverseLerp(fovStartSpeed, fovFullSpeed, player.HorizontalSpeed);

        float targetFov = baseFov + maxFovBoost * speedFraction;

        speedFov = Mathf.MoveTowards(speedFov, targetFov, fovChangeSpeed * Time.deltaTime);

        // open up fast toward the punch, and as soon as it gets there start settling back to 0
        float punchRate = dashPunch < dashPunchTarget ? dashFovRiseSpeed : dashFovFadeSpeed;
        dashPunch = Mathf.MoveTowards(dashPunch, dashPunchTarget, punchRate * Time.deltaTime);

        if (dashPunchTarget > 0f && dashPunch >= dashPunchTarget)
            dashPunchTarget = 0f;

        cam.fieldOfView = speedFov + dashPunch;
    }

    private void UpdateBob()
    {
        float speed = player.HorizontalSpeed;

        bool bobbing = player.IsGrounded && !player.IsSliding && speed > 1f;

        bobWeight = Mathf.MoveTowards(bobWeight, bobbing ? 1f : 0f, bobBlendSpeed * Time.deltaTime);

        if (bobbing)
            bobPhase += speed / bobStrideLength * Time.deltaTime * Mathf.PI;

        float strength = bobAmount * bobWeight * Mathf.Clamp01(speed / bobFullSpeed);

        // two bobs up and down for every sway left and right, like a real walking head
        Vector3 offset = new Vector3(
            Mathf.Sin(bobPhase) * strength * 0.5f,
            Mathf.Abs(Mathf.Sin(bobPhase)) * strength,
            0f);

        transform.localPosition = restPosition + offset;
    }
}
