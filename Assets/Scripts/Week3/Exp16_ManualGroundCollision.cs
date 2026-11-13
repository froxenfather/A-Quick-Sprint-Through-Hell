using UnityEngine;
using UnityEngine.InputSystem;

// learning Objective: without a Rigidbody, nothing stops the object from falling through the floor - moving a Transform never triggers physics collision resolution.
// YOU have to detect the ground and cancel velocity
// requires: a Collider on the ground below (no Rigidbody on this object)
public class Exp16_ManualGroundCollision : MonoBehaviour
{
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float skinWidth = 0.05f; // small buffer above the surface to prevent jitter/penetration
    [SerializeField] private float groundCheckDistance = 0.2f;

    private Vector3 velocity;
    private Vector3 startPosition;
    private bool grounded;
    private float halfHeight;

    private void Awake()
    {
        startPosition = transform.position;

        // im a fucking GOD bro this makes the raycast start at thge actual bottom, not its pivot - works for any object size, nmanual "height" number to keep in sync
        Renderer rend = GetComponentInChildren<Renderer>();
        halfHeight = rend != null ? rend.bounds.extents.y : 0f;
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
        Vector3 feetPosition = transform.position + Vector3.down * halfHeight;

        if (Physics.Raycast(feetPosition, Vector3.down, out RaycastHit hit, groundCheckDistance + skinWidth))
        {
            grounded = true;
            nextPosition.y = hit.point.y + halfHeight + skinWidth;
            velocity.y = 0f;
        }
        else
        {
            grounded = false;
        }

        transform.position = nextPosition;

        Debug.DrawRay(feetPosition, Vector3.down * (groundCheckDistance + skinWidth), grounded ? Color.green : Color.red);
    }
}
