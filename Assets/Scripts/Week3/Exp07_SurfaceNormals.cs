using UnityEngine;

// learning Objective: a surface normal points perpendicular away from a surface, and its real job is splitting an incoming velocity into "into the surface" and "along the surface" parts
// place this on a tester above Ramp_15 / Ramp_30 / Ramp_45 and compare results
public class Exp07_SurfaceNormals : MonoBehaviour
{
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private float normalDrawLength = 2f;

    [Header("Pretend incoming velocity")]
    [SerializeField] private Vector3 incomingVelocity = new Vector3(0f, -6f, 0f);
    [SerializeField] private float velocityDrawScale = 0.4f;

    private void Update()
    {
        // 1. fire a raycast straight down from this object out to maxDistance
        Vector3 origin = transform.position;
        Vector3 direction = Vector3.down;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance))
        {
            Vector3 hitPoint = hit.point;
            Vector3 normal = hit.normal;

            // 2. draw a ray starting at the hit point, pointing along the hit's surface normal, scaled by normalDrawLength
            Debug.DrawRay(hitPoint, normal * normalDrawLength, Color.white);

            // 3. log the normal vector so you can read its exact values
            Debug.Log($"Surface Normal: {normal}");


            // --- VELOCITY SPLITTING MATH ---

            // 4. into-surface part: take the dot product of incomingVelocity and the normal, then multiply the normal by that number
            float intoNormalMagnitude = Vector3.Dot(incomingVelocity, normal);
            Vector3 intoSurfacePart = normal * intoNormalMagnitude;

            // 5. along-surface part: subtract the into-surface part from incomingVelocity
            Vector3 alongSurfacePart = incomingVelocity - intoSurfacePart;

            // 6. bounce part: use the built-in Vector3 reflect method on incomingVelocity and the normal
            Vector3 bouncePart = Vector3.Reflect(incomingVelocity, normal);


            // --- VISUALIZATION & MAGNITUDE LOGGING ---

            // 7. draw all four from the hit point, each in a different color, each scaled by velocityDrawScale
            Debug.DrawRay(hitPoint, incomingVelocity * velocityDrawScale, Color.yellow);   // incoming Velocity
            Debug.DrawRay(hitPoint, intoSurfacePart * velocityDrawScale, Color.red);       // into-Surface
            Debug.DrawRay(hitPoint, alongSurfacePart * velocityDrawScale, Color.blue);     // along-Surface
            Debug.DrawRay(hitPoint, bouncePart * velocityDrawScale, Color.green);         // bounce
            Debug.DrawRay(origin, direction * maxDistance, Color.white);

            // 8. log the length of the into-surface part (impact speed) and the along-surface part (slide speed)
            float impactSpeed = Mathf.Abs(intoNormalMagnitude); // using absolute value as magnitude for speed scalar
            float slideSpeed = alongSurfacePart.magnitude;

            Debug.Log($"[Ramp Study] Impact Speed (Canceled): {impactSpeed:F2} m/s | Slide Speed (Tangential): {slideSpeed:F2} m/s");
        }
    }
}
