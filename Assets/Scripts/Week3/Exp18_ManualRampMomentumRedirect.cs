using UnityEngine;
using UnityEngine.InputSystem;

// learning Objective: this is Exp16's manual ground collision, but instead of just zeroing out downward velocity on impact, the FULL incoming velocity gets projected onto the surface plane - so a fast fall onto a steep ramp gets redirected down-slope instead of just stopping dead
// requires: a Collider on the ramp below (no Rigidbody on this object)
public class Exp18_ManualRampMomentumRedirect : MonoBehaviour
{
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float skinWidth = 0.05f;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Tooltip("If true, the redirected velocity keeps the same speed it had on impact. If false, it keeps whatever magnitude ProjectOnPlane naturally produces.")]
    [SerializeField] private bool preserveImpactSpeed = true;

    private Vector3 velocity;
    private Vector3 startPosition;
    private bool grounded;

    private void Awake()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            transform.position = startPosition;
            velocity = Vector3.zero;
        }

        velocity += Vector3.down * gravity * Time.deltaTime;

        Vector3 nextPosition = transform.position + velocity * Time.deltaTime;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance + skinWidth))
        {
            grounded = true;
            nextPosition.y = hit.point.y + skinWidth;

            float impactSpeed = velocity.magnitude;
            Vector3 redirected = Vector3.ProjectOnPlane(velocity, hit.normal);
            velocity = preserveImpactSpeed ? redirected.normalized * impactSpeed : redirected;

            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            Debug.Log($"impact speed: {impactSpeed:F2}  slope: {slopeAngle:F1} deg  redirected speed: {velocity.magnitude:F2}");
        }
        else
        {
            grounded = false;
        }

        transform.position = nextPosition;

        Debug.DrawRay(transform.position, velocity, grounded ? Color.cyan : Color.red);
    }
}
