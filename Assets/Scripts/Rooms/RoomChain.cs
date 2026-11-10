using System.Collections.Generic;
using UnityEngine;

// main Driving Engine for the game!
// spawns rooms one after another. each new room is moved and rotated so its Entry gate lands exactly on the previous room's Exit gate, so the rooms connect no matter how they are shaped or how high their gates are
// the first room's Entry goes at this object's position and rotation. keep this object's scale at (1, 1, 1)
public class RoomChain : MonoBehaviour
{
    [Header("Rooms")]
    [SerializeField] private Room[] roomPrefabs;

    // 0 means pick a new random seed every run (it is printed to the Console, so you can reuse it to repeat a run)
    [SerializeField] private int seed = 0;

    // chance that a room is flipped left-to-right (across its Entry gate's forward direction) when it is spawned
    // only rooms with Can Mirror turned on are ever flipped
    [Range(0f, 1f)]
    [SerializeField] private float mirrorChance = 0.5f;

    [Header("Streaming")]
    // found automatically if left empty
    [SerializeField] private Transform player;

    // rooms kept built in front of the room the player is in
    [Min(1)]
    [SerializeField] private int roomsAhead = 3;

    // rooms kept behind it before they get deleted
    [Min(0)]
    [SerializeField] private int roomsBehind = 1;

    [Header("Entry seal")]
    // the wall that fills a room's Entry once the player is through it. uses ProBuilder's default if empty
    [SerializeField] private Material sealMaterial;

    [Min(0.01f)]
    [SerializeField] private float sealThickness = 1f;

    // how many rooms behind the player an Entry has to be before it seals
    // 0 seals a room's Entry as soon as the player is through it. 1 seals it once the player is through the room's Exit as well
    [Min(0)]
    [SerializeField] private int sealRoomsBehind = 1;

    // only used when Seal Rooms Behind is 0. how far past the Entry gate the player has to be before it seals,
    // so the wall never appears inside them
    [Min(0f)]
    [SerializeField] private float sealDistance = 2f;

    private readonly List<Room> rooms = new List<Room>();
    private System.Random random;

    // how many rooms the player has gone all the way through (crossed the Exit gate of)
    // the room the player is in right now is rooms[passedCount]
    private int passedCount;

    // how many rooms have had their Entry sealed. rooms are sealed in order, oldest first
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

        // a room counts as passed once the player is in front of its Exit gate
        while (passedCount < rooms.Count && HasPassed(rooms[passedCount].Exit))
            passedCount++;

        // close the way back behind the player
        while (sealedCount < rooms.Count && ShouldSeal(sealedCount))
        {
            rooms[sealedCount].SealEntry(sealMaterial, sealThickness);
            sealedCount++;
        }

        BuildAhead();
        RemoveOldRooms();
    }

    private bool ShouldSeal(int roomIndex)
    {
        if (sealRoomsBehind == 0)
            return IsPastBy(rooms[roomIndex].Entry, sealDistance);

        // passedCount is the room the player is in, so this is how many rooms behind the player this one is
        return passedCount - roomIndex >= sealRoomsBehind;
    }

    // true when the player is at least this far in front of the gate (on the side its arrow points to)
    private bool IsPastBy(RoomGate gate, float distance)
    {
        Vector3 toPlayer = player.position - gate.transform.position;

        return Vector3.Dot(toPlayer, gate.WorldForward) > distance;
    }

    // in front of the gate means on the side its arrow points to
    private bool HasPassed(RoomGate gate)
    {
        Vector3 toPlayer = player.position - gate.transform.position;

        return Vector3.Dot(toPlayer, gate.WorldForward) > 0f;
    }

    private void BuildAhead()
    {
        while (rooms.Count < passedCount + 1 + roomsAhead)
        {
            // stop instead of looping forever if a room could not be spawned
            if (!SpawnNext())
            {
                enabled = false;
                return;
            }
        }
    }

    private bool SpawnNext()
    {
        // where the new room's Entry gate has to end up
        Vector3 targetPosition;
        Quaternion targetRotation;

        if (rooms.Count == 0)
        {
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }
        else
        {
            RoomGate previousExit = rooms[rooms.Count - 1].Exit;

            targetPosition = previousExit.transform.position;
            targetRotation = previousExit.WorldRotation;
        }

        Room prefab = roomPrefabs[random.Next(roomPrefabs.Length)];

        if (prefab == null)
        {
            Debug.LogError("One of the Room Prefabs entries is empty.", this);
            return false;
        }

        // always roll, even for rooms that cannot mirror, so a seed gives the same rooms whatever Can Mirror is set to
        bool mirror = random.NextDouble() < mirrorChance && prefab.CanMirror;

        Room room = Instantiate(prefab, transform);

        // Room.Awake has already looked for the gates, and logged the problem if there is one
        if (room.Entry == null || room.Exit == null)
        {
            Destroy(room.gameObject);
            return false;
        }

        PlaceEntryAt(room, targetPosition, targetRotation, mirror);
        rooms.Add(room);

        return true;
    }

    // moves the whole room so its Entry gate lands exactly on the given position and rotation
    // if mirror is true the room is also flipped left-to-right across the Entry gate's forward direction
    // the Entry stays where it is, only the rest of the room swaps sides
    private static void PlaceEntryAt(Room room, Vector3 position, Quaternion rotation, bool mirror)
    {
        Transform entry = room.Entry.transform;

        // where the Entry sits relative to the room's own root
        Vector3 entryLocalPosition = room.transform.InverseTransformPoint(entry.position);
        Quaternion entryLocalRotation = Quaternion.Inverse(room.transform.rotation) * entry.rotation;

        // we want:
        //   rootRotation * entryLocalRotation                = rotation
        //   rootPosition + rootRotation * entryLocalPosition = position
        // so:
        Quaternion inverseEntryRotation = Quaternion.Inverse(entryLocalRotation);
        Vector3 flip = Vector3.one;

        if (mirror)
        {
            // the room is flipped by a scale of -1 along the gate's left-right (X) direction
            // flipping an axis reverses turns around the other two axes, so the inverse rotation has to flip the same way
            flip = new Vector3(-1f, 1f, 1f);
            inverseEntryRotation = new Quaternion(inverseEntryRotation.x, -inverseEntryRotation.y, -inverseEntryRotation.z, inverseEntryRotation.w);
        }

        Quaternion rootRotation = rotation * inverseEntryRotation;
        Vector3 rootPosition = position - rootRotation * Vector3.Scale(entryLocalPosition, flip);

        room.transform.SetPositionAndRotation(rootPosition, rootRotation);
        room.transform.localScale = flip;
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
