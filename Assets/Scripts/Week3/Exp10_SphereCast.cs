using UnityEngine;

// learning Objective: a raycast is an infinitely thin line; a spherecast
// sweeps a volume through space and can catch edges a raycast would miss
public class Exp10_SphereCast : MonoBehaviour
{
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private float sphereRadius = 0.4f;

    private void Update()
    {
        // TODO: cast both a raycast and a spherecast downward and compare results
        // pseudocode:
        //   - fire a standard raycast straight down out to maxDistance, note whether it hit
        //   - fire a spherecast of radius sphereRadius, same origin and direction and
        //     distance, note whether it hit
        //   - log both results side by side so you can compare them on the same frame
        bool rayHit = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, maxDistance);
        bool sphereHit = Physics.SphereCast(transform.position, sphereRadius, Vector3.down, out RaycastHit sHit, maxDistance);

        Debug.Log($"ray: {rayHit}  sphere: {sphereHit}");

        // TODO: visualize both casts
        // pseudocode:
        //   - draw the raycast as a line in one color
        
        if (sphereHit)
            Debug.DrawLine(transform.position, sHit.point, Color.red);
        if (rayHit)
            Debug.DrawLine(transform.position, hit.point, Color.blue);
        //   - (spherecasts don't have a built-in debug draw - comparing the two Hit booleans
        //     in the Console, especially right at an edge, is enough to see the difference)
    }
}
