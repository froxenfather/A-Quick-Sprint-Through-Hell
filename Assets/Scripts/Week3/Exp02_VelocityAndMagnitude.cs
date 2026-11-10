using UnityEngine;

// learning Objective: velocity is a vector (direction + speed);
// magnitude collapses it down to a single speed number
public class Exp02_VelocityAndMagnitude : MonoBehaviour
{
    [SerializeField] private float rayScale = 1f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        Vector3 rbVelocity =  rb.linearVelocity;
        float magnitude = rbVelocity.magnitude;
        Debug.Log("Object Vector" + rbVelocity);
        Debug.Log("Magnitude: " + magnitude);

        // TODO: draw the velocity vector as a ray from this object's position
        // pseudocode:
        //   - draw a ray starting at the object's position, pointing along the
        //     velocity vector (scaled by rayScale so it's visible), in red
        Debug.DrawRay(transform.position, rbVelocity * rayScale, Color.red);
    }
}
