using UnityEngine;

// serves as both a script for the main engine and a nifty builder tool!
public enum GateType
{
    Entry,
    Exit
}

// A connection point on a room. The transform IS the gate:
//   position = the middle of the opening
//   forward  = the blue Z arrow = the direction the player travels THROUGH the gate

// Entry and Exit gates both point the way the player travels, so stitching two rooms together is just
// "put the next room's Entry exactly on top of the previous room's Exit".
// Nothing here knows about floors or heights, a gate can be anywhere in a room.
// Keep the scale at (1, 1, 1).
public class RoomGate : MonoBehaviour
{
    [SerializeField] private GateType type = GateType.Entry;

    // Only used to draw the gizmo, so you can see the opening. Match it to your gate frame.
    [SerializeField] private Vector2 openingSize = new Vector2(8f, 18f);

    public GateType Type
    {
        get => type;
        set => type = value;
    }

    public Vector2 OpeningSize
    {
        get => openingSize;
        set => openingSize = value;
    }

    private void OnDrawGizmos()
    {
        // Gizmo settings stay set after we draw, so remember them and put them back at the end.
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        // Draw in the gate's own space, so the rectangle and arrow follow its rotation.
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = type == GateType.Entry ? Color.green : Color.red;

        float halfWidth = openingSize.x * 0.5f;
        float halfHeight = openingSize.y * 0.5f;

        Vector3 bottomLeft = new Vector3(-halfWidth, -halfHeight, 0f);
        Vector3 bottomRight = new Vector3(halfWidth, -halfHeight, 0f);
        Vector3 topRight = new Vector3(halfWidth, halfHeight, 0f);
        Vector3 topLeft = new Vector3(-halfWidth, halfHeight, 0f);

        // The opening.
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);

        // An arrow pointing the way the player travels.
        Vector3 tip = Vector3.forward * 4f;
        Gizmos.DrawLine(Vector3.zero, tip);
        Gizmos.DrawLine(tip, new Vector3(0.75f, 0f, 3f));
        Gizmos.DrawLine(tip, new Vector3(-0.75f, 0f, 3f));
        Gizmos.DrawLine(tip, new Vector3(0f, 0.75f, 3f));
        Gizmos.DrawLine(tip, new Vector3(0f, -0.75f, 3f));

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
