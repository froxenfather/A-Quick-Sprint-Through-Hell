using UnityEngine;

// learning Objective: the cross product of two vectors returns a vector perpendicular to both — useful for finding a direction along a surface
public class Exp05_CrossProduct : MonoBehaviour
{
    [SerializeField] private float rayLength = 3f;

    private void Update()
    {
        // TODO: compute a "right" direction using the cross product of world up and forward
        // pseudocode:
        //   - take the cross product of world up and this object's forward vector
        //   - store the result - it should point perpendicular to both inputs
        Vector3 rightDirection = Vector3.Cross(Vector3.up, transform.forward);
        Debug.Log("My right is " + rightDirection);

        // TODO: draw forward, up, and the cross product result with different colors
        // pseudocode:
        //   - draw a ray for forward, scaled by rayLength, in one color
        //   - draw a ray for world up, scaled by rayLength, in another color
        //   - draw a ray for the cross product result, scaled by rayLength, in a third color
        Debug.DrawRay(transform.position, rightDirection * rayLength, Color.red);
        Debug.DrawRay(transform.position, Vector3.up * rayLength, Color.blue);
        Debug.DrawRay(transform.position, transform.forward * rayLength, Color.green);
    }
}
