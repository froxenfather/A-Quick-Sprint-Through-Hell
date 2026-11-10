using UnityEngine;

// learning Objective: bring raycasts, surface normals, slope angle, and projection together. put one of these on (or above) each of Ramp_15 / Ramp_30 / Ramp_45
public class Exp13_RampComparisonTest : MonoBehaviour
{
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private Vector3 testMovementDirection = Vector3.forward;
    [SerializeField] private float maxDistance = 5f;

    private void Update()
    {
        Vector3 origin = rayOrigin != null ? rayOrigin.position : transform.position;

        // 1. fire a raycast straight down from origin out to maxDistance
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance))
        {
            // 2. find the angle between the hit normal and world up (Vector3.up)
            // Vector3.Angle returns a positive float value in degrees (0 to 180)
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

            // 3. project testMovementDirection onto the surface plane, and normalize the result
            Vector3 projectedDirection = Vector3.ProjectOnPlane(testMovementDirection, hit.normal);
            Vector3 normalizedProjectedDirection = projectedDirection.normalized;

            // 4. draw the downward ray out to the hit distance (White)
            Debug.DrawLine(origin, hit.point, Color.white);

            // 5. draw the surface normal from the hit point (Green)
            // extended by 2 units so it stands out visibly from the surface
            Debug.DrawRay(hit.point, hit.normal * 2f, Color.green);

            // 6. draw the projected movement direction from the hit point (Cyan/Blue)
            // extended by 2 units so you can see the direction it points down/up the slope
            Debug.DrawRay(hit.point, normalizedProjectedDirection * 2f, Color.cyan);

            // 7. log this ramp's name alongside its computed slope angle
            // uses hit.transform.name to get the specific ramp it struck
            Debug.Log($"[{hit.transform.name}] Computed Slope Angle: {slopeAngle:F1}°");
        }
        else
        {
            // optional: Draw a red line if the setup is too high and misses the ramp entirely
            Debug.DrawRay(origin, Vector3.down * maxDistance, Color.red);
        }
    }
}
