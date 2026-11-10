using UnityEngine;

// learning Objective: the dot product of two normalized vectors measures alignment
// 1 = same direction, 0 = perpendicular, -1 = opposite direction
public class Exp04_DotProduct : MonoBehaviour
{
    [SerializeField] private Transform target;

    private enum Facing { InFront, ToTheSide, Behind }

    private void Update()
    {
        if (target == null) return;

        // TODO: compute the dot product between this object's forward and the normalized direction to the target
        // pseudocode:
        //   - get the normalized direction from this object to the target
        //   - take the dot product of this object's forward vector and that
        //     normalized direction - result is a single float between -1 and 1
        //   - log the result
        Vector3 diffVector = target.position - transform.position;
        float distance = Vector3.Distance(transform.position, target.position);
        Vector3 direction = diffVector.normalized;
        
        float dot = Vector3.Dot(transform.forward, direction);
        Debug.Log("DotProduct: " + dot);

        // TODO (Homework Task): classify the target using dot thresholds you choose
        float inFrontThreshold = 0.5f;
        float behindThreshold = -0.5f;

        Facing facing;
        if (dot > inFrontThreshold) facing = Facing.InFront;
        else if (dot < behindThreshold) facing = Facing.Behind;
        else facing = Facing.ToTheSide;

        Debug.Log("Facing: " + facing);
    }
}
