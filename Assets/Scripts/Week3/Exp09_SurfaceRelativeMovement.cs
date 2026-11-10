using UnityEngine;
using UnityEngine.InputSystem;

// learning Objective: combine input + surface normal to move along angled geometry (flat ground, 15/30/45 degree ramps) without the projected vector losing speed
public class Exp09_SurfaceRelativeMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private float groundedDistance = 0.3f;

    private Rigidbody rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        moveInput = Vector2.zero;
        if (Keyboard.current.upArrowKey.isPressed) moveInput.y += 1f;
        if (Keyboard.current.downArrowKey.isPressed) moveInput.y -= 1f;
        if (Keyboard.current.rightArrowKey.isPressed) moveInput.x += 1f;
        if (Keyboard.current.leftArrowKey.isPressed) moveInput.x -= 1f;
    }

    private void FixedUpdate()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, maxDistance))
            return;

        if (hit.distance > groundedDistance)
            return; // too far below to count as standing on it — let gravity fall normally

        Vector3 rawDirection =
            transform.forward * moveInput.y +
            transform.right * moveInput.x;

        Debug.DrawRay(hit.point, hit.normal * 2f, Color.green);

        // TODO: project rawDirection onto the surface, normalize it, then set velocity
        // pseudocode:
        //   - project rawDirection onto the plane defined by hit.normal (same idea as Exp08)
        //   - normalize the result - without this, speed would change with slope angle,
        //     which isn't what we want
        //   - set the rigidbody's velocity to that normalized direction scaled by moveSpeed
        Vector3 slopeDirection = Vector3.ProjectOnPlane(rawDirection, hit.normal);
        Vector3 normalizedSlopeDirection = slopeDirection.normalized;
        Debug.DrawRay(transform.position, normalizedSlopeDirection * 2f, Color.blue);

        if (moveInput.sqrMagnitude > 0.0001f)
        {
            rb.linearVelocity = normalizedSlopeDirection * moveSpeed;
        }
    }
}
