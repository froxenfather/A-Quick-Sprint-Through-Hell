using System.Collections.Generic;
using UnityEngine;

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
