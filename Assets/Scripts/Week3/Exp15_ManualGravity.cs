using UnityEngine;
using UnityEngine.InputSystem;

// learning Objective: model gravity as acceleration applied directly to a Transform - no Rigidbody, no physics engine.
// you own every step of acceleration -> velocity -> position yourself (Euler integration)
// requires: just a Transform (no Rigidbody, no Collider needed)
public class Exp15_ManualGravity : MonoBehaviour
{
    [SerializeField] private float gravity = 9.81f;

    private Vector3 velocity;
    private Vector3 startPosition;

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
        transform.position += velocity * Time.deltaTime;

        Debug.DrawRay(transform.position, velocity, Color.red);
    }
}
