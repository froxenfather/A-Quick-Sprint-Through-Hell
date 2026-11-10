using UnityEngine;

// learning Objective: understand how a raycast detects geometry (origin, direction, max distance, layer mask, RaycastHit)
public class Exp06_RaycastBasics : MonoBehaviour
{
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private LayerMask hitMask = ~0; // ~0 = "everything"

    private void Update()
    {
        // TODO: cast a ray downward from this object and report what it hits
        // pseudocode:
        //   - fire a raycast from this object's position, straight down, out to maxDistance,
        //     restricted to hitMask, and capture the hit info
        //   - if it hit something: log the hit collider's name, hit point, distance, and
        //     normal, and draw a ray to the hit point in one color
        //   - if it didn't hit anything: draw the full-length ray in a different color
        //     so you can visually tell a miss from a hit
        Vector3 origin = transform.position;
        Vector3 direction = Vector3.down;
        Vector3 forward = transform.forward;

        // 2. fire the raycast and capture hit info
        if (Physics.Raycast(origin, direction, out RaycastHit downHit, maxDistance, hitMask))
        {
            // IF IT HIT SOMETHING:
            // log the hit details
            Debug.Log($"Hit Below: {downHit.collider.name} | Point: {downHit.point} | Distance: {downHit.distance} | Normal: {downHit.normal}");

            // draw a green ray from origin to the exact hit point
            Debug.DrawLine(origin, downHit.point, Color.green);
        }
        else
        {
            // IF IT DID NOT HIT ANYTHING:
            // calculate the full-length endpoint for the missed ray
            Vector3 endPoint = origin + (direction * maxDistance);

            // draw a red ray showing the full length of the miss
            Debug.DrawLine(origin, endPoint, Color.red);
        }
        
        if (Physics.Raycast(origin, forward, out RaycastHit forwardHit, maxDistance, hitMask))
        {
            // IF IT HIT SOMETHING:
            // log the hit details
            Debug.Log($"Hit Front: {forwardHit.collider.name} | Point: {forwardHit.point} | Distance: {forwardHit.distance} | Normal: {forwardHit.normal}");

            // draw a green ray from origin to the exact hit point
            Debug.DrawLine(origin, forwardHit.point, Color.green);
        }
        else
        {
            // IF IT DID NOT HIT ANYTHING:
            // calculate the full-length endpoint for the missed ray
            Vector3 endPoint = origin + (forward * maxDistance);

            // draw a red ray showing the full length of the miss
            Debug.DrawLine(origin, endPoint, Color.red);
        }
    }
}
