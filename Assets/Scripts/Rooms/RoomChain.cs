using System.Collections.Generic;
using UnityEngine;

// main Driving Engine for the game!
// spawns rooms one after another. each new room is moved and rotated so its Entry gate lands exactly on the previous room's Exit gate, so the rooms connect no matter how they are shaped or how high their gates are
// which room comes next is decided by RoomPlanner: weighted picks from the room lists, up and down sections, and a check that a room never overlaps one that already exists
// the first room's Entry goes at this object's position and rotation. keep this object's scale at (1, 1, 1)
public class RoomChain : MonoBehaviour
{
    // each list has a weight box beside every room: 1 is normal, 0.5 is half as likely, 4 is four times as likely, 0 means never
    [Header("Rooms (normal level)")]
    [SerializeField] private RoomEntry[] forwardRooms;
    [SerializeField] private RoomEntry[] turnRooms;

    // a transition room starts an up or down section (its Exit gate should point up or down)
    [SerializeField] private RoomEntry[] upTransitions;
    [SerializeField] private RoomEntry[] downTransitions;

    [Header("Rooms (inside an up or down section)")]
    // after an up transition, 1 to 3 rooms come from this list
    [SerializeField] private RoomEntry[] upRooms;

    // then one of these brings the level back to normal
    [SerializeField] private RoomEntry[] upReturns;

    [SerializeField] private RoomEntry[] downRooms;
    [SerializeField] private RoomEntry[] downReturns;

    // how many rooms an up or down section lasts, rolled between these two (both included)
    [Header("Up/Down Configuration")]
    [Min(1)]
    [SerializeField] private int sectionRoomsMin = 1;

    [Min(1)]
    [SerializeField] private int sectionRoomsMax = 3;

    // 0 means pick a new random seed every run (it is printed to the Console, so you can reuse it to repeat a run)
    [Header("Randomness")]
    [SerializeField] private int seed = 0;

    // chance that a room is flipped left-to-right (across its Entry gate's forward direction) when it is spawned
    // only rooms with Can Mirror turned on are ever flipped
    [Range(0f, 1f)]
    [SerializeField] private float mirrorChance = 0.5f;

    // a new room is only allowed if its footprint and this clear space in front of its Exit (width, height, length) are free
    // that way every room leaves space for at least one more, and the generator never paints itself into a corner
    [Header("Overlap safeguard")]
    [SerializeField] private Vector3 exitClearanceSize = new Vector3(30f, 30f, 30f);

    // how far (meters) a new room's footprint may reach back over the room it connects to (their gates meet and the frames overlap by design)
    // a new room is tested pushed forward by this much, so raise it if the footprints stick out far past the gates
    [Min(0f)]
    [SerializeField] private float gateTolerance = 5f;

    // how far it may overlap any other room before it counts as overlapping
    [Min(0f)]
    [SerializeField] private float overlapTolerance = 0.25f;

    // when no room fits, how many rooms in a row may be taken back to try something else
    [Min(0)]
    [SerializeField] private int maxBacktracks = 6;

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

    [Header("Debug")]
    // draws every room's footprint in the Scene view (green), and the last room that didn't fit (red). off unless you want it
    [SerializeField] private bool drawFootprints = false;

    // how many rooms Simulate Rooms places (right-click the RoomChain component to run it)
    [Min(1)]
    [SerializeField] private int simulateRooms = 200;

    // the old single room list. the rooms in it are moved into Forward Rooms the first time this is loaded in the editor
    [SerializeField, HideInInspector] private Room[] roomPrefabs;

    // the most rooms one frame may try to spawn (including taking rooms back), see BuildAhead
    private const int MaxSpawnAttemptsPerFrame = 16;

    private readonly List<Room> rooms = new List<Room>();
    private RoomPlanner planner;
    private bool warnedAboutAttempts;

    // the last Simulate Rooms run, kept so its footprints can be drawn in the Scene view
    private RoomPlanner simulated;

    // how many rooms the player has gone all the way through (crossed the Exit gate of)
    // the room the player is in right now is rooms[passedCount]
    private int passedCount;

    // how many rooms have had their Entry sealed. rooms are sealed in order, oldest first
    private int sealedCount;

    private void OnValidate()
    {
        sectionRoomsMax = Mathf.Max(sectionRoomsMin, sectionRoomsMax);

        // move the rooms from before the lists existed into Forward Rooms, each with a weight of 1
        if (roomPrefabs != null && roomPrefabs.Length > 0 && (forwardRooms == null || forwardRooms.Length == 0))
        {
            List<RoomEntry> moved = new List<RoomEntry>();

            foreach (Room room in roomPrefabs)
            {
                if (room != null)
                    moved.Add(new RoomEntry { room = room });
            }

            forwardRooms = moved.ToArray();
            roomPrefabs = new Room[0];

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    private void Start()
    {
        int usedSeed = seed != 0 ? seed : System.Environment.TickCount;
        Debug.Log($"RoomChain seed: {usedSeed}");

        planner = CreatePlanner(new System.Random(usedSeed));

        if (!planner.HasLevelRooms())
        {
            Debug.LogError("RoomChain has no rooms to pick from. Add rooms (with a weight above 0) to Forward Rooms, Turn Rooms or the transition lists.", this);
            enabled = false;
            return;
        }

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
        int attempts = 0;

        while (rooms.Count < passedCount + 1 + roomsAhead)
        {
            // a hard limit on the work done in one frame, so a layout that nothing fits (or a bug) can never freeze the editor
            // taking rooms back to find a better one can make this loop run more times than there are rooms to build
            if (attempts++ >= MaxSpawnAttemptsPerFrame)
            {
                if (!warnedAboutAttempts)
                {
                    warnedAboutAttempts = true;
                    Debug.LogWarning($"RoomChain tried {MaxSpawnAttemptsPerFrame} times in one frame and still has rooms to build ahead. It will keep trying on the next frames. The rooms and footprints may not fit together, run Simulate Rooms to see why.", this);
                }

                return;
            }

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
        // the planner never touches a room the player has reached: rooms after passedCount can be taken back if nothing fits
        if (!planner.TryPlanNext(passedCount, out PlannedRoom plan, out int removed))
        {
            Debug.LogError("RoomChain could not find any room to spawn next. Check that the lists for the current section have rooms with a weight above 0.", this);
            return false;
        }

        // the planner stepped back to find a room that fits, so take the same rooms away here
        for (int i = 0; i < removed; i++)
        {
            Room taken = rooms[rooms.Count - 1];
            rooms.RemoveAt(rooms.Count - 1);

            taken.gameObject.SetActive(false);
            Destroy(taken.gameObject);
        }

        Room room = Instantiate(plan.prefab, transform);

        // the footprint boxes were only for the planner, they must not exist in the game
        RoomFootprint footprint = room.GetComponentInChildren<RoomFootprint>(true);

        if (footprint != null)
        {
            footprint.gameObject.SetActive(false);
            Destroy(footprint.gameObject);
        }

        // Room.Awake has already looked for the gates, and logged the problem if there is one
        if (room.Entry == null || room.Exit == null)
        {
            Destroy(room.gameObject);
            planner.DropLast();
            return false;
        }

        // the planner worked out exactly where this room goes when it checked that it fits
        room.transform.SetPositionAndRotation(plan.rootPosition, plan.rootRotation);
        room.transform.localScale = plan.flip;

        rooms.Add(room);

        return true;
    }

    private void RemoveOldRooms()
    {
        while (passedCount > roomsBehind)
        {
            Destroy(rooms[0].gameObject);
            rooms.RemoveAt(0);
            planner.RemoveOldest();
            passedCount--;
            sealedCount = Mathf.Max(0, sealedCount - 1);
        }
    }

    // ============================================================
    // PLANNER SETUP
    // ============================================================

    private RoomPlanner CreatePlanner(System.Random random)
    {
        RoomPlannerSettings settings = new RoomPlannerSettings();

        // one list per RoomCategory, in the same order as the enum
        settings.lists = new[]
        {
            forwardRooms, turnRooms, upTransitions, downTransitions,
            upRooms, downRooms, upReturns, downReturns
        };

        settings.mirrorChance = mirrorChance;
        settings.sectionRoomsMin = sectionRoomsMin;
        settings.sectionRoomsMax = Mathf.Max(sectionRoomsMin, sectionRoomsMax);
        settings.exitClearanceSize = exitClearanceSize;
        settings.gateTolerance = gateTolerance;
        settings.overlapTolerance = overlapTolerance;
        settings.maxBacktracks = maxBacktracks;

        return new RoomPlanner(settings, random, transform.position, transform.rotation);
    }

    // ============================================================
    // DEBUGGING
    // ============================================================

    // places a lot of rooms with the planner alone (nothing is spawned) and keeps every one, so you can see how often the safeguard has to step in
    // the live game deletes rooms behind the player, so this is a much harder test than playing
    [ContextMenu("Simulate Rooms")]
    private void Simulate()
    {
        int usedSeed = seed != 0 ? seed : System.Environment.TickCount;
        RoomPlanner test = CreatePlanner(new System.Random(usedSeed));

        if (!test.HasLevelRooms())
        {
            Debug.LogError("RoomChain has no rooms to simulate. Add rooms to the lists first.", this);
            return;
        }

        for (int i = 0; i < simulateRooms; i++)
        {
            // like the live game, only the newest few rooms can be taken back, and the first room always stays
            if (!test.TryPlanNext(Mathf.Max(0, test.Count - 1 - roomsAhead), out PlannedRoom plan, out int removed))
                break;
        }

        simulated = test;

        // counted from the rooms that are left (rooms that were taken back and replaced don't count)
        int[] counts = new int[8];

        foreach (RoomPlanner.Record record in test.Records)
            counts[(int)record.category]++;

        Debug.Log(
            $"Simulated {test.Count} rooms (seed {usedSeed}):\n" +
            $"  overlapping pairs: {test.CountOverlaps()}  (should be 0, except where a room was forced)\n" +
            $"  rooms thrown away before one fit: {test.RejectedCount}  ({test.RejectedByPrevious} ran into the room before, {test.RejectedByOlder} into older rooms, {test.RejectedByClearance} had their exit clearance blocked)\n" +
            $"  times a room was taken back: {test.BacktrackCount}\n" +
            $"  rooms forced in anyway: {test.ForcedCount}\n" +
            $"  forward {counts[0]}, turns {counts[1]}, up transitions {counts[2]}, down transitions {counts[3]}, " +
            $"up rooms {counts[4]}, down rooms {counts[5]}, up returns {counts[6]}, down returns {counts[7]}",
            this);
    }

    private void OnDrawGizmos()
    {
        if (!drawFootprints)
            return;

        RoomPlanner shown = planner != null ? planner : simulated;

        if (shown == null)
            return;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 1f);

        foreach (RoomPlanner.Record record in shown.Records)
        {
            foreach (OrientedBox box in record.boxes)
                box.DrawWire();
        }

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 1f);

        foreach (OrientedBox box in shown.RejectedBoxes)
            box.DrawWire();
    }
}
