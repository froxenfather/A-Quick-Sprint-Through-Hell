using UnityEngine;

// marks the child object of a room that holds its footprint: one or more BoxColliders covering the space the room fills
// the RoomChain reads these boxes to make sure a new room doesn't overlap one that already exists, then removes this object when the room is spawned
// make one with the Room's context menu (the three dots on the Room component): Create Footprint From Renderers
// move, resize or add boxes with the normal Box Collider handles, they are only used for this
public class RoomFootprint : MonoBehaviour
{
    // only drawn while the Footprint object is selected, so it stays out of the way the rest of the time
    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);

        foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>())
        {
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
