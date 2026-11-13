using UnityEngine;

// widens the camera FOV as the player speeds up, and punches it wider for a moment when they dash.
// put it on the Camera
[RequireComponent(typeof(Camera))]
public class SpeedFOV : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;

    [Header("Speed range")]
    [Range(0f, 60f)]
    [SerializeField] private float startSpeed = 12f;   // at or below this, no FOV change (walk speed)

    [Range(0f, 60f)]
    [SerializeField] private float fullSpeed = 20f;    // max speed the FOV reacts to, at or above this = full boost (sprint speed)

    [Header("FOV")]
    [Range(0f, 40f)]
    [SerializeField] private float maxFovBoost = 12f;

    [Range(1f, 200f)]
    [SerializeField] private float fovChangeSpeed = 60f;   // degrees per second

    [Header("Dash punch")]
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

    private Camera cam;
    private float baseFov;

    // the FOV from our speed alone.
    // kept here (not read back from the camera) so the dash punch can sit on top of it without the two fighting
    private float speedFov;

    // the extra degrees from a dash right now, and where it is heading (the boost on the way up, 0 on the way down)
    private float dashPunch;
    private float dashPunchTarget;

    private void Start()
    {
        cam = GetComponent<Camera>();
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
        // 0 at walk speed, 1 at sprint speed, clamped so launch pads don't blow the FOV out
        float speedFraction = Mathf.InverseLerp(startSpeed, fullSpeed, player.HorizontalSpeed);

        float targetFov = baseFov + maxFovBoost * speedFraction;

        speedFov = Mathf.MoveTowards(speedFov, targetFov, fovChangeSpeed * Time.deltaTime);

        // open up fast toward the punch, and as soon as it gets there start settling back to 0
        float punchRate = dashPunch < dashPunchTarget ? dashFovRiseSpeed : dashFovFadeSpeed;
        dashPunch = Mathf.MoveTowards(dashPunch, dashPunchTarget, punchRate * Time.deltaTime);

        if (dashPunchTarget > 0f && dashPunch >= dashPunchTarget)
            dashPunchTarget = 0f;

        cam.fieldOfView = speedFov + dashPunch;
    }
}
