using UnityEngine;

// a small up-and-down, side-to-side bob of the camera while running, so movement feels like footsteps.
// put this on the Main Camera

// it moves the camera itself, not the CameraPivot above it (SlideVisuals lowers the pivot for slides and crouches, and they would fight)
// set Amount to 0 to turn it off
public class CameraBob : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;

    // how far the camera moves up and down (meters) at full speed.
    // the sideways sway is half of this
    [Min(0f)]
    [SerializeField] private float amount = 0.04f;

    // meters travelled per full step.
    // match PlayerAudio's stride length and the bob lands on the footsteps
    [Min(0.5f)]
    [SerializeField] private float strideLength = 3.5f;

    // the flat speed (m/s) at which the bob reaches full Amount
    [Min(1f)]
    [SerializeField] private float fullBobSpeed = 20f;

    // how quickly the bob fades in and out when you start and stop (per second)
    [Min(0.1f)]
    [SerializeField] private float blendSpeed = 8f;

    private Vector3 restPosition;
    private float phase;
    private float weight;

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<CustomPlayerController>();

        restPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        float speed = player.HorizontalSpeed;

        bool bobbing = player.IsGrounded && !player.IsSliding && speed > 1f;

        weight = Mathf.MoveTowards(weight, bobbing ? 1f : 0f, blendSpeed * Time.deltaTime);

        if (bobbing)
            phase += speed / strideLength * Time.deltaTime * Mathf.PI;

        float strength = amount * weight * Mathf.Clamp01(speed / fullBobSpeed);

        // two bobs up and down for every sway left and right, like a real walking head
        Vector3 offset = new Vector3(
            Mathf.Sin(phase) * strength * 0.5f,
            Mathf.Abs(Mathf.Sin(phase)) * strength,
            0f);

        transform.localPosition = restPosition + offset;
    }
}
