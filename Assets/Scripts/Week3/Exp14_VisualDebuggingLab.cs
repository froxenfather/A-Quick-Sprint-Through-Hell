using UnityEngine;

// learning Objective: build the habit of visualizing invisible math, gated behind Inspector toggles so only what you need is drawn
public class Exp14_VisualDebuggingLab : MonoBehaviour
{
    [Header("Toggles")]
    [SerializeField] private bool showVelocity;
    [SerializeField] private bool showGroundRay;
    [SerializeField] private bool showSurfaceNormal;

    [Header("Settings")]
    [SerializeField] private float groundRayDistance = 5f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (showVelocity)
        {
            // TODO: draw the current velocity vector
            // pseudocode:
            //   - draw a ray from this object's position along the rigidbody's velocity vector
            Debug.DrawRay(transform.position, rb.linearVelocity, Color.red);
        }

        if (showGroundRay || showSurfaceNormal)
        {
            // TODO: raycast down once, then draw the ray and/or the hit normal depending on which toggles are enabled
            // pseudocode:
            //   - fire one raycast straight down out to groundRayDistance, note whether it hit
            //   - if showGroundRay is on and it hit, draw the ray out to the hit distance
            //   - if showSurfaceNormal is on and it hit, draw the hit's normal from the hit point
            //   - doing the raycast once and reusing the result avoids casting twice per frame
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundRayDistance))
            {
                if (showSurfaceNormal)
                {
                    Debug.DrawRay(hit.point, hit.normal * 2f, Color.green);
                }

                if (showGroundRay)
                {
                    Debug.DrawLine(transform.position, hit.point, Color.yellow);
                }
            }
        }
    }
}
