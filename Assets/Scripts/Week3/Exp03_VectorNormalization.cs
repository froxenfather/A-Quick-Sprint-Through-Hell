using UnityEngine;

// learning Objective: understand what normalizing a vector does — direction keeps distance baked in, normalizedDirection has magnitude 1
public class Exp03_VectorNormalization : MonoBehaviour
{
    [SerializeField] private Transform target;

    private void Update()
    {
        if (target == null) return;

        // TODO: compute the direction to target, and its normalized form
        // pseudocode:
        //   - subtract this object's position from the target's position to get
        //     a vector pointing from here to the target (its length IS the distance)
        //   - get the normalized version of that vector (same direction, length 1)
        //   - log both vectors along with their magnitudes so you can compare them
        Vector3 diffVector = target.position - transform.position;
        float distance = Vector3.Distance(transform.position, target.position);
        Vector3 direction = diffVector.normalized;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);

        Debug.Log("Distance Vector" + diffVector);
        Debug.Log("The Tower is " + distance + " far away");
        Debug.Log("The Tower is facing" + direction);

        // TODO: draw both vectors with different colors so the length difference is visible
        // pseudocode:
        //   - draw a ray for the raw direction vector in one color
        //   - draw a ray for the normalized direction (scaled up a bit so it's
        //     visible, e.g. x3) in a different color
        Debug.DrawRay(transform.position, direction, Color.yellow, 1f);
        Debug.DrawRay(transform.position, diffVector, Color.red, 3f);
    }
}
