using UnityEngine;

// learning Objective: gravity is acceleration — velocity changes continuously over time, it isn't an instantly-applied speed
public class Exp11_GravityAndFalling : MonoBehaviour
{
    [SerializeField] float fallingLeeway;
    private Rigidbody rb;
    private float fallStartTime;
    private bool wasFalling;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        float yVelocity = rb.linearVelocity.y;
        bool isFalling = yVelocity < -fallingLeeway;

        if (isFalling && !wasFalling)
            fallStartTime = Time.time;

        if (isFalling)
        {
            Debug.Log("falling at speed " + yVelocity);
            Debug.Log("Time since fall started " + (Time.time - fallStartTime));
        }

        wasFalling = isFalling;
    }
}
