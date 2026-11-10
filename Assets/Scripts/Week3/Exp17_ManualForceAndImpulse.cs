using UnityEngine;
using UnityEngine.InputSystem;

// learning Objective: revisit Exp01's Force vs Impulse idea, but with no
// rigidbody. "Force" becomes acceleration added every frame it's held;
// "impulse" becomes a one-time jump added to velocity on the frame it's pressed
// requires: just a Transform
public class Exp17_ManualForceAndImpulse : MonoBehaviour
{
    [SerializeField] private float upwardAcceleration = 15f;
    [SerializeField] private float jumpImpulseSpeed = 6f;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float skinWidth = 0.05f;
    [SerializeField] private float groundCheckDistance = 0.2f;

    private Vector3 velocity;
    private Vector3 startPosition;
    private bool grounded;
    private float halfHeight;

    private void Awake()
    {
        startPosition = transform.position;

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

        if (Keyboard.current.upArrowKey.isPressed)
            velocity += Vector3.up * upwardAcceleration * Time.deltaTime;

        if (Keyboard.current.gKey.wasPressedThisFrame)
            velocity += Vector3.up * jumpImpulseSpeed;

        velocity += Vector3.down * gravity * Time.deltaTime;

        Vector3 nextPosition = transform.position + velocity * Time.deltaTime;
        Vector3 feetPosition = transform.position + Vector3.down * halfHeight;

        // scale how far we look for ground by how far we're about to fall this frame - a fixed small window can't catch a fast fall before it overshoots, which is what causes the "snap" onto the surface
        float fallDistanceThisFrame = Mathf.Max(-velocity.y * Time.deltaTime, 0f);
        float castDistance = Mathf.Max(groundCheckDistance, fallDistanceThisFrame) + skinWidth;

        if (velocity.y <= 0f && Physics.Raycast(feetPosition, Vector3.down, out RaycastHit hit, castDistance))
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

        Debug.DrawRay(transform.position, velocity, Color.red);
    }
}
