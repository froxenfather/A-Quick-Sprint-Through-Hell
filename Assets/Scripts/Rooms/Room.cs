using UnityEngine;
using UnityEngine.ProBuilder;

// put this on the root of a room prefab
// a room needs exactly one Entry gate and one Exit gate (RoomGate) somewhere inside it
// keep the root's scale at (1, 1, 1)
public class Room : MonoBehaviour
{
    // lets the RoomChain flip this room left-to-right. turn it off for rooms that have text, conveyor belts,
    // or anything else that points a direction, because those do not mirror along with the room
    [SerializeField] private bool canMirror = true;

    public bool CanMirror => canMirror;

    public RoomGate Entry { get; private set; }
    public RoomGate Exit { get; private set; }

    private bool entrySealed;

    private void Awake()
    {
        FindGates();
    }

    // fills the Entry opening with a solid wall of exactly the opening's size, so the player cannot go back
    public void SealEntry(Material material, float thickness)
    {
        if (entrySealed || Entry == null)
            return;

        entrySealed = true;

        Vector2 openingSize = Entry.OpeningSize;
        ProBuilderMesh seal = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(openingSize.x, openingSize.y, thickness));
        seal.name = "Entry Seal";
        seal.transform.SetParent(Entry.transform, false);
        seal.transform.localPosition = Vector3.zero;
        seal.transform.localRotation = Quaternion.identity;

        seal.ToMesh();
        seal.Refresh();

        seal.GetComponent<MeshRenderer>().sharedMaterial = material != null ? material : BuiltinMaterials.defaultMaterial;

        // built outside the ProBuilder editor menus, so nothing adds the collider for us
        seal.gameObject.AddComponent<MeshCollider>();
    }

#if UNITY_EDITOR
    // makes the room's footprint: the boxes the RoomChain uses to make sure rooms never overlap
    // open the room prefab first (double-click it), then use the three dots on the Room component. one box per piece of the room is the most exact
    [ContextMenu("Create Footprint From Renderers")]
    private void CreateFootprintFromRenderers()
    {
        BuildFootprint(false);
    }

    // one big box around the whole room. quick, but it counts the empty corners of an L-shaped or turning room as filled
    [ContextMenu("Create Footprint (One Box)")]
    private void CreateFootprintOneBox()
    {
        BuildFootprint(true);
    }

    private void BuildFootprint(bool oneBox)
    {
        RoomFootprint old = GetComponentInChildren<RoomFootprint>(true);

        if (old != null)
            UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);

        GameObject footprint = new GameObject("Footprint");
        UnityEditor.Undo.RegisterCreatedObjectUndo(footprint, "Create Room Footprint");
        footprint.transform.SetParent(transform, false);
        footprint.AddComponent<RoomFootprint>();

        int count = 0;

        // every piece of the room that is switched on (the Entry Seal only exists while playing)
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (oneBox)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (Renderer piece in renderers)
            {
                Bounds local = piece.localBounds;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));

                    // from the piece's own space to this room's space
                    Vector3 inRoom = transform.InverseTransformPoint(piece.transform.TransformPoint(corner));

                    min = Vector3.Min(min, inRoom);
                    max = Vector3.Max(max, inRoom);
                }

                count = 1;
            }

            if (count > 0)
            {
                BoxCollider box = footprint.AddComponent<BoxCollider>();
                box.center = (min + max) * 0.5f;
                box.size = max - min;
            }
        }
        else
        {
            foreach (Renderer piece in renderers)
            {
                // a box in the piece's own space, so a turned or tilted piece gets a turned or tilted box
                GameObject boxObject = new GameObject("Box");
                boxObject.transform.SetParent(footprint.transform, false);
                boxObject.transform.SetPositionAndRotation(piece.transform.position, piece.transform.rotation);
                boxObject.transform.localScale = piece.transform.lossyScale;

                BoxCollider box = boxObject.AddComponent<BoxCollider>();
                box.center = piece.localBounds.center;
                box.size = piece.localBounds.size;

                count++;
            }
        }

        UnityEditor.EditorUtility.SetDirty(gameObject);

        if (count == 0)
            Debug.LogWarning($"Room '{name}' has nothing to make a footprint from (no renderers found).", this);
        else
            Debug.Log($"Footprint created for '{name}' with {(oneBox ? 1 : count)} box(es). Move or resize them with the Box Collider handles, then save the prefab.", this);
    }
#endif

    // looks through the children for the gates. safe to call more than once
    // returns false (and says why in the Console) if the room is set up wrong
    public bool FindGates()
    {
        Entry = null;
        Exit = null;

        int entryCount = 0;
        int exitCount = 0;

        foreach (RoomGate gate in GetComponentsInChildren<RoomGate>(true))
        {
            if (gate.Type == GateType.Entry)
            {
                Entry = gate;
                entryCount++;
            }
            else
            {
                Exit = gate;
                exitCount++;
            }
        }

        if (entryCount != 1 || exitCount != 1)
        {
            Debug.LogError(
                $"Room '{name}' needs exactly one Entry gate and one Exit gate, but it has {entryCount} Entry and {exitCount} Exit.",
                this);
            return false;
        }

        return true;
    }
}
