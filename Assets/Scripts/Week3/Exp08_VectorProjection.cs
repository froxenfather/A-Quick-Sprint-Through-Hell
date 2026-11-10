using UnityEngine;

// learning Objective: Vector3.ProjectOnPlane removes the portion of a vector that points into a surface, leaving a vector that lies along it
public class Exp08_VectorProjection : MonoBehaviour
{
    [SerializeField] private Vector3 movementDirection = Vector3.forward;
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private float rayLength = 2f;

    private void Update()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, maxDistance))
            return;

        // TODO: project movementDirection onto the surface plane defined by hit.normal
        Vector3 projectedMovement = Vector3.ProjectOnPlane(movementDirection, hit.normal);

        

        // TODO: draw the original movement vector, the surface normal, and the projected vector
        // pseudocode:
        //   - draw the raw movementDirection (scaled by rayLength) in one color
        //   - draw the surface normal (scaled by rayLength) from the hit point in another color
        //   - draw the projected direction (scaled by rayLength) in a third color, and
        //     compare how it differs from the raw movement vector
        Debug.DrawRay(hit.point, movementDirection * rayLength, Color.white);
        Debug.DrawRay(hit.point, hit.normal * rayLength, Color.green);
        Debug.DrawRay(hit.point, projectedMovement * rayLength, Color.magenta);
    }
}
