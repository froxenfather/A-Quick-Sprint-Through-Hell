using UnityEngine;
using UnityEngine.ProBuilder;

// Put this on the root of a room prefab.
// A room needs exactly one Entry gate and one Exit gate (RoomGate) somewhere inside it.
// Keep the root's scale at (1, 1, 1).
public class Room : MonoBehaviour
{
    public RoomGate Entry { get; private set; }
    public RoomGate Exit { get; private set; }

    private bool entrySealed;

    private void Awake()
    {
        FindGates();
    }

    // Fills the Entry opening with a solid wall of exactly the opening's size, so the player cannot go back.
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

        // Built outside the ProBuilder editor menus, so nothing adds the collider for us.
        seal.gameObject.AddComponent<MeshCollider>();
    }

    // Looks through the children for the gates. Safe to call more than once.
    // Returns false (and says why in the Console) if the room is set up wrong.
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
