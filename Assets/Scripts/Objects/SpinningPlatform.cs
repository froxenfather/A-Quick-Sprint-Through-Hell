using UnityEngine;

// test platform for rotation carry. needs a kinematic Rigidbody (turn on Interpolate for smooth riding)
[RequireComponent(typeof(Rigidbody))]
public class SpinningPlatform : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 30f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, degreesPerSecond * Time.fixedDeltaTime, 0f));
    }
}
