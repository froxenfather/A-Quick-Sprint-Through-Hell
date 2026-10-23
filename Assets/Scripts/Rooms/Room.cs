using UnityEngine;

// Put this on the root of a room prefab.
// A room needs exactly one Entry gate and one Exit gate (RoomGate) somewhere inside it.
// Keep the root's scale at (1, 1, 1).
public class Room : MonoBehaviour
{
    public RoomGate Entry { get; private set; }
    public RoomGate Exit { get; private set; }

    private void Awake()
    {
        FindGates();
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
