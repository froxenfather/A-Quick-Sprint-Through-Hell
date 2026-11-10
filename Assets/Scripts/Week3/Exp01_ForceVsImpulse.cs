using UnityEngine;
using UnityEngine.InputSystem;

// learning Objective: understand the difference between a continuous Force, an instant Impulse, and directly setting velocity
// controls: Up arrow = up force, Left arrow = forward force, Right arrow = forward impulse, Down arrow = reset
public class Exp01_ForceVsImpulse : MonoBehaviour
{
    [Header("Force Settings")]
    [SerializeField] private float upwardForce = 10f;
    [SerializeField] private float forwardForce = 10f;
    [SerializeField] private float forwardImpulse = 5f;

    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        if (Keyboard.current.upArrowKey.isPressed)
        {
            // TODO: apply an upward CONTINUOUS force
            // pseudocode:
            //   - add a force to the rigidbody, pointing straight up, scaled by upwardForce
            //   - use the force mode meant for something applied over time, not an instant kick
            rb.AddForce(Vector3.up * upwardForce, ForceMode.Force);
        }

        if (Keyboard.current.leftArrowKey.isPressed)
        {
            // TODO: apply a forward CONTINUOUS force, every frame it's held
            // pseudocode:
            //   - same idea as above, but pointing in the object's forward direction
            //   - since this block runs every frame the key is held, the force builds up over time
            rb.AddForce(transform.forward * forwardForce, ForceMode.Force);
        }

        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            // TODO: apply a forward INSTANT impulse
            // pseudocode:
            //   - add a force to the rigidbody in the forward direction, scaled by forwardImpulse
            //   - use the force mode meant for an instant momentum change, applied once
            rb.AddForce(transform.forward * forwardImpulse, ForceMode.Impulse);
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            ResetCube();
        }
    }

    private void ResetCube()
    {
        // TODO: reset position, rotation, and velocity so the test can repeat cleanly
        // pseudocode:
        //   - put the transform's position and rotation back to the values saved in Awake
        //   - clear both linear and angular velocity on the rigidbody, otherwise it'll
        //     snap back but keep flying
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.linearVelocity =  Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
