using System.Collections.Generic;
using UnityEngine;

// Spawns rooms one after another. Each new room is moved and rotated so its Entry gate lands exactly on the
// previous room's Exit gate, so the rooms connect no matter how they are shaped or how high their gates are.
//
// The first room's Entry goes at this object's position and rotation. Keep this object's scale at (1, 1, 1).
public class RoomChain : MonoBehaviour
{
    [Header("Rooms")]
    [SerializeField] private Room[] roomPrefabs;

    // 0 means pick a new random seed every run (it is printed to the Console, so you can reuse it to repeat a run).
    [SerializeField] private int seed = 0;

    [Header("Streaming")]
    // Found automatically if left empty.
    [SerializeField] private Transform player;

    // Rooms kept built in front of the room the player is in.
    [Min(1)]
    [SerializeField] private int roomsAhead = 3;

    // Rooms kept behind it before they get deleted.
    [Min(0)]
    [SerializeField] private int roomsBehind = 1;

    [Header("Entry seal")]
    // The wall that fills a room's Entry once the player is through it. Uses ProBuilder's default if empty.
    [SerializeField] private Material sealMaterial;

    [Min(0.01f)]
    [SerializeField] private float sealThickness = 1f;

    // How far past the Entry gate the player has to be before it seals, so the wall never appears inside them.
    [Min(0f)]
    [SerializeField] private float sealDistance = 2f;

    private readonly List<Room> rooms = new List<Room>();
    private System.Random random;

    // How many rooms the player has gone all the way through (crossed the Exit gate of).
    // The room the player is in right now is rooms[passedCount].
    private int passedCount;

    // How many rooms have had their Entry sealed. Rooms are sealed in order, oldest first.
    private int sealedCount;

    private void Start()
    {
        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            Debug.LogError("RoomChain has no room prefabs. Add some to the Room Prefabs list.", this);
            enabled = false;
            return;
        }

        int usedSeed = seed != 0 ? seed : System.Environment.TickCount;
        random = new System.Random(usedSeed);
        Debug.Log($"RoomChain seed: {usedSeed}");

        if (player == null)
        {
            CustomPlayerController controller = FindFirstObjectByType<CustomPlayerController>();

            if (controller != null)
                player = controller.transform;
        }

        BuildAhead();
    }

    private void Update()
    {
        if (player == null || rooms.Count == 0)
            return;

        // A room counts as passed once the player is in front of its Exit gate.
        while (passedCount < rooms.Count && HasPassed(rooms[passedCount].Exit))
            passedCount++;

        // Close the way back behind the player.
        while (sealedCount < rooms.Count && IsPastBy(rooms[sealedCount].Entry, sealDistance))
        {
            rooms[sealedCount].SealEntry(sealMaterial, sealThickness);
            sealedCount++;
        }

        BuildAhead();
        RemoveOldRooms();
    }

    // True when the player is at least this far in front of the gate (on the side its arrow points to).
    private bool IsPastBy(RoomGate gate, float distance)
    {
        Vector3 toPlayer = player.position - gate.transform.position;

        return Vector3.Dot(toPlayer, gate.transform.forward) > distance;
    }

    // In front of the gate means on the side its arrow points to.
    private bool HasPassed(RoomGate gate)
    {
        Vector3 toPlayer = player.position - gate.transform.position;

        return Vector3.Dot(toPlayer, gate.transform.forward) > 0f;
    }

    private void BuildAhead()
    {
        while (rooms.Count < passedCount + 1 + roomsAhead)
        {
            // Stop instead of looping forever if a room could not be spawned.
            if (!SpawnNext())
            {
                enabled = false;
                return;
            }
        }
    }

    private bool SpawnNext()
    {
        // Where the new room's Entry gate has to end up.
        Vector3 targetPosition;
        Quaternion targetRotation;

        if (rooms.Count == 0)
        {
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }
        else
        {
            Transform previousExit = rooms[rooms.Count - 1].Exit.transform;

            targetPosition = previousExit.position;
            targetRotation = previousExit.rotation;
        }

        Room prefab = roomPrefabs[random.Next(roomPrefabs.Length)];

        if (prefab == null)
        {
            Debug.LogError("One of the Room Prefabs entries is empty.", this);
            return false;
        }

        Room room = Instantiate(prefab, transform);

        // Room.Awake has already looked for the gates, and logged the problem if there is one.
        if (room.Entry == null || room.Exit == null)
        {
            Destroy(room.gameObject);
            return false;
        }

        PlaceEntryAt(room, targetPosition, targetRotation);
        rooms.Add(room);

        return true;
    }

    // Moves the whole room so its Entry gate lands exactly on the given position and rotation.
    private static void PlaceEntryAt(Room room, Vector3 position, Quaternion rotation)
    {
        Transform entry = room.Entry.transform;

        // Where the Entry sits relative to the room's own root.
        Vector3 entryLocalPosition = room.transform.InverseTransformPoint(entry.position);
        Quaternion entryLocalRotation = Quaternion.Inverse(room.transform.rotation) * entry.rotation;

        // We want:
        //   rootRotation * entryLocalRotation                = rotation
        //   rootPosition + rootRotation * entryLocalPosition = position
        // so:
        Quaternion rootRotation = rotation * Quaternion.Inverse(entryLocalRotation);
        Vector3 rootPosition = position - rootRotation * entryLocalPosition;

        room.transform.SetPositionAndRotation(rootPosition, rootRotation);
    }

    private void RemoveOldRooms()
    {
        while (passedCount > roomsBehind)
        {
            Destroy(rooms[0].gameObject);
            rooms.RemoveAt(0);
            passedCount--;
            sealedCount = Mathf.Max(0, sealedCount - 1);
        }
    }
}
