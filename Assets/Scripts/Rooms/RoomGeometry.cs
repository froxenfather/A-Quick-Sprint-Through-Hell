using System.Collections.Generic;
using UnityEngine;

// the geometry the room generator works with (RoomPlanner and RoomChain use these, nothing in a scene does).
//   OrientedBox    a box that can be turned and mirrored, with the overlap test the generator uses to make sure rooms never overlap
//   RoomBlueprint  everything the generator needs to know about a room prefab, worked out once from the prefab asset

// a box that can be turned and mirrored: a center plus three half-extent vectors (an axis direction times half the size along it)
// the three vectors stay at right angles under any rotation or mirror, so one transform handles a room turning, flipping, or pointing up
public struct OrientedBox
{
    // a box never gets thinner than this when shrunk, so it keeps its direction (a flat box still has a normal)
    private const float MinHalfSize = 0.01f;

    public Vector3 center;
    public Vector3 halfX;
    public Vector3 halfY;
    public Vector3 halfZ;

    public OrientedBox(Vector3 center, Vector3 halfX, Vector3 halfY, Vector3 halfZ)
    {
        this.center = center;
        this.halfX = halfX;
        this.halfY = halfY;
        this.halfZ = halfZ;
    }

    // a box from a local center and size, in whatever space the matrix leads to
    public static OrientedBox FromCenterSize(Vector3 center, Vector3 size, Matrix4x4 matrix)
    {
        Vector3 half = size * 0.5f;

        return new OrientedBox(
            matrix.MultiplyPoint3x4(center),
            matrix.MultiplyVector(new Vector3(half.x, 0f, 0f)),
            matrix.MultiplyVector(new Vector3(0f, half.y, 0f)),
            matrix.MultiplyVector(new Vector3(0f, 0f, half.z)));
    }

    public OrientedBox Transformed(Matrix4x4 matrix)
    {
        return new OrientedBox(
            matrix.MultiplyPoint3x4(center),
            matrix.MultiplyVector(halfX),
            matrix.MultiplyVector(halfY),
            matrix.MultiplyVector(halfZ));
    }

    // the same box with every half size reduced by amount, so boxes that only just touch or slightly overlap don't count
    public OrientedBox Shrunk(float amount)
    {
        return new OrientedBox(center, Shorten(halfX, amount), Shorten(halfY, amount), Shorten(halfZ, amount));
    }

    private static Vector3 Shorten(Vector3 half, float amount)
    {
        float length = half.magnitude;

        if (length <= amount + MinHalfSize)
            return length > 0.0001f ? half / length * MinHalfSize : Vector3.zero;

        return half * ((length - amount) / length);
    }

    // radius of a sphere that holds the whole box
    public float Radius => Mathf.Sqrt(halfX.sqrMagnitude + halfY.sqrMagnitude + halfZ.sqrMagnitude);

    // the separating axis test: two boxes are apart if there is any direction along which their shadows don't overlap
    // for two boxes that is the 3 axes of each, plus the 9 directions at right angles to a pair of edges
    public bool Overlaps(OrientedBox other)
    {
        Vector3 offset = other.center - center;

        // far apart boxes can't touch, no need for the full test
        float reach = Radius + other.Radius;

        if (offset.sqrMagnitude > reach * reach)
            return false;

        return !(
            IsSeparatedBy(halfX, other, offset) ||
            IsSeparatedBy(halfY, other, offset) ||
            IsSeparatedBy(halfZ, other, offset) ||
            IsSeparatedBy(other.halfX, other, offset) ||
            IsSeparatedBy(other.halfY, other, offset) ||
            IsSeparatedBy(other.halfZ, other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfX, other.halfX), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfX, other.halfY), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfX, other.halfZ), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfY, other.halfX), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfY, other.halfY), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfY, other.halfZ), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfZ, other.halfX), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfZ, other.halfY), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfZ, other.halfZ), other, offset));
    }

    private bool IsSeparatedBy(Vector3 axis, OrientedBox other, Vector3 offset)
    {
        float squared = axis.sqrMagnitude;

        // parallel edges give a zero direction, which can't separate anything
        if (squared < 0.0000001f)
            return false;

        axis /= Mathf.Sqrt(squared);

        float reachA = Mathf.Abs(Vector3.Dot(halfX, axis)) + Mathf.Abs(Vector3.Dot(halfY, axis)) + Mathf.Abs(Vector3.Dot(halfZ, axis));
        float reachB = Mathf.Abs(Vector3.Dot(other.halfX, axis)) + Mathf.Abs(Vector3.Dot(other.halfY, axis)) + Mathf.Abs(Vector3.Dot(other.halfZ, axis));

        return Mathf.Abs(Vector3.Dot(offset, axis)) > reachA + reachB;
    }

    // draws the 12 edges, for the Scene view
    public void DrawWire()
    {
        for (int i = 0; i < 4; i++)
        {
            // the four corners of the face on each side of the box, and the four edges joining the two faces
            float y = (i & 1) == 0 ? -1f : 1f;
            float z = (i & 2) == 0 ? -1f : 1f;

            Gizmos.DrawLine(Corner(-1f, y, z), Corner(1f, y, z));
            Gizmos.DrawLine(Corner(y, -1f, z), Corner(y, 1f, z));
            Gizmos.DrawLine(Corner(y, z, -1f), Corner(y, z, 1f));
        }
    }

    private Vector3 Corner(float x, float y, float z)
    {
        return center + halfX * x + halfY * y + halfZ * z;
    }
}

// everything the generator needs to know about a room prefab, worked out once from the prefab asset (no spawning needed):
// where its gates are and what space it fills.
// all of it is relative to the room's root
public class RoomBlueprint
{
    public Room room;

    // the Entry gate, relative to the room's root
    public Vector3 entryPosition;
    public Quaternion entryRotation;

    // the Exit gate, relative to the room's root (the whole matrix, so it follows a mirror too)
    public Matrix4x4 exitMatrix;

    // the footprint boxes, relative to the room's root.
    // empty if the room has no footprint yet
    public List<OrientedBox> boxes = new List<OrientedBox>();

    // a sphere around all the boxes, to skip rooms that are nowhere near each other
    public Vector3 boundsCenter;
    public float boundsRadius;

    // returns null (and says why) if the room is set up wrong
    public static RoomBlueprint Build(Room prefab)
    {
        RoomGate entry = null;
        RoomGate exit = null;
        int entryCount = 0;
        int exitCount = 0;

        foreach (RoomGate gate in prefab.GetComponentsInChildren<RoomGate>(true))
        {
            if (gate.Type == GateType.Entry)
            {
                entry = gate;
                entryCount++;
            }
            else
            {
                exit = gate;
                exitCount++;
            }
        }

        if (entryCount != 1 || exitCount != 1)
        {
            Debug.LogError($"Room '{prefab.name}' needs exactly one Entry gate and one Exit gate, but it has {entryCount} Entry and {exitCount} Exit, so the generator skips it.", prefab);
            return null;
        }

        RoomBlueprint blueprint = new RoomBlueprint();
        blueprint.room = prefab;

        Transform root = prefab.transform;
        Matrix4x4 rootToLocal = root.worldToLocalMatrix;

        blueprint.entryPosition = root.InverseTransformPoint(entry.transform.position);
        blueprint.entryRotation = Quaternion.Inverse(root.rotation) * entry.transform.rotation;
        blueprint.exitMatrix = rootToLocal * exit.transform.localToWorldMatrix;

        RoomFootprint footprint = prefab.GetComponentInChildren<RoomFootprint>(true);

        if (footprint != null)
        {
            foreach (BoxCollider box in footprint.GetComponentsInChildren<BoxCollider>(true))
                blueprint.boxes.Add(OrientedBox.FromCenterSize(box.center, box.size, rootToLocal * box.transform.localToWorldMatrix));
        }

        if (blueprint.boxes.Count == 0)
            Debug.LogWarning($"Room '{prefab.name}' has no footprint, so it can't be checked for overlap. Open it and use the Room's context menu: Create Footprint From Renderers.", prefab);

        blueprint.CalculateBounds();

        return blueprint;
    }

    // the clear space in front of the Exit gate that a room has to leave free, so there is always room for one more room after it
    public OrientedBox ExitClearanceBox(Vector3 size)
    {
        // the box is made in the gate's own space (it starts at the gate and runs forward), then moved to the room's space
        return OrientedBox.FromCenterSize(new Vector3(0f, 0f, size.z * 0.5f), size, exitMatrix);
    }

    // where the room's root has to go so its Entry gate lands exactly on the given position and rotation
    // if mirror is true the room is also flipped left-to-right across the Entry gate's forward direction, and the Entry stays where it is
    public static void ComputePlacement(
        Vector3 entryLocalPosition, Quaternion entryLocalRotation,
        Vector3 position, Quaternion rotation, bool mirror,
        out Vector3 rootPosition, out Quaternion rootRotation, out Vector3 flip)
    {
        // we want:
        //   rootRotation * entryLocalRotation                = rotation
        //   rootPosition + rootRotation * entryLocalPosition = position
        // so:
        Quaternion inverseEntryRotation = Quaternion.Inverse(entryLocalRotation);
        flip = Vector3.one;

        if (mirror)
        {
            // the room is flipped by a scale of -1 along the gate's left-right (X) direction
            // flipping an axis reverses turns around the other two axes, so the inverse rotation has to flip the same way
            flip = new Vector3(-1f, 1f, 1f);
            inverseEntryRotation = new Quaternion(inverseEntryRotation.x, -inverseEntryRotation.y, -inverseEntryRotation.z, inverseEntryRotation.w);
        }

        rootRotation = rotation * inverseEntryRotation;
        rootPosition = position - rootRotation * Vector3.Scale(entryLocalPosition, flip);
    }

    private void CalculateBounds()
    {
        if (boxes.Count == 0)
        {
            boundsCenter = entryPosition;
            boundsRadius = 0f;
            return;
        }

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (OrientedBox box in boxes)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = box.center +
                    box.halfX * ((i & 1) == 0 ? -1f : 1f) +
                    box.halfY * ((i & 2) == 0 ? -1f : 1f) +
                    box.halfZ * ((i & 4) == 0 ? -1f : 1f);

                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }
        }

        boundsCenter = (min + max) * 0.5f;
        boundsRadius = (max - min).magnitude * 0.5f;
    }
}
