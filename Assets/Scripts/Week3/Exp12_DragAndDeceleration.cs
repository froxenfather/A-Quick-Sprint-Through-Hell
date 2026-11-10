using UnityEngine;
using UnityEngine.InputSystem;

// learning Objective: compare three ways of losing speed —
// 1 = Rigidbody linearDamping (set in Inspector, no code), 2 = opposing force, 3 = direct velocity change
public class Exp12_DragAndDeceleration : MonoBehaviour
{
    [SerializeField] private float manualDecelForce = 10f;
    [SerializeField] private float directVelocityDecelRate = 5f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
    
    private void Start()
    {
        Invoke(nameof(Push), 5f);
    }

    private void Push()
    {
        rb.AddForce(Vector3.forward * 10f, ForceMode.Impulse);
    }

    private void FixedUpdate()
    {
        // key 1: rely entirely on Rigidbody.linearDamping in the Inspector — no code needed

        if (Keyboard.current.digit2Key.isPressed)
        {
            // TODO: apply a force opposing current velocity to slow down manually
            // pseudocode:
            //   - get the current velocity's direction (normalized), and flip it (negate it)
            //     to get the opposing direction
            //   - add a force in that opposing direction, scaled by manualDecelForce, using
            //     the continuous force mode
            
            Vector3 opposing = -rb.linearVelocity.normalized;
            rb.AddForce(opposing * manualDecelForce, ForceMode.Force);
        }

        if (Keyboard.current.digit3Key.isPressed)
        {
            // TODO: directly manipulate velocity toward zero (no forces involved)
            // pseudocode:
            //   - there's a Vector3 method that moves a value toward a target by a fixed
            //     step per call. use it to move the rigidbody's velocity toward zero, at a
            //     rate of directVelocityDecelRate per second (multiply by fixed delta time
            //     for a frame-rate independent step)
            //   - assign the result back to the rigidbody's velocity
            rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, directVelocityDecelRate * Time.fixedDeltaTime);
        }
        if (rb.linearVelocity.magnitude > 0.1f)
            Debug.unityLogger.Log("LinearVelocity: " + rb.linearVelocity);
    }
}
