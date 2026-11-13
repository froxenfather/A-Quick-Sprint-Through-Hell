using System.Collections.Generic;
using UnityEngine;

// main Driving Engine for the game!
// spawns rooms one after another.
// each new room is moved and rotated so its Entry gate lands exactly on the previous room's Exit gate, so the rooms connect no matter how they are shaped or how high their gates are
// which room comes next is decided by RoomPlanner: weighted picks from the room lists, up and down sections, and a check that a room never overlaps one that already exists
// the first room's Entry goes at this object's position and rotation.
// keep this object's scale at (1, 1, 1)
public class RoomChain : MonoBehaviour
{
    // the room every run starts with (the chute).
    // it is placed first with its Entry on this object, and the generator carries on from its Exit
    // leave empty to start with a normal random room
    [Header("Start")]
    [SerializeField] private Room starterRoom;

    // the player starts this far (meters) straight up from the starter room's Entry gate...
    [SerializeField] private float spawnHeightAboveEntry = 100f;

    // ...and this far in front of it along the gate's blue arrow, facing the arrow and looking straight down
    [SerializeField] private float spawnDistanceInFront = 1f;

    // the song that starts when the player goes through the starter room's Exit and plays once (it does not loop).
    // leave empty for no music
    [SerializeField] private AudioClip startMusic;

    // the volume the song fades up to.
    // it can be changed while playing
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.7f;

    // seconds the song takes to fade in from silence up to Music Volume.
    // 0 starts it at full volume straight away
    [Range(0f, 30f)]
    [SerializeField] private float musicFadeInSeconds = 4f;

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

    [Header("Gate colors")]
    // ticked: every gate uses the plain material below.
    // unticked: entrances are blue and exits are orange again
    // it changes the gates already in the scene as you click it, and every room that spawns
    [SerializeField] private bool plainGateLook = false;

    // the plain look (filled in with ProBuilderDefault_Copy in the editor if left empty)
    [SerializeField] private Material plainGateMaterial;

    // the usual colors (filled in with Blue and orange stretch in the editor if left empty)
    [SerializeField] private Material entryGateMaterial;
    [SerializeField] private Material exitGateMaterial;

    [Header("Entry seal")]
    // the wall that fills a room's Entry once the player is through it.
    // uses ProBuilder's default if empty
    [SerializeField] private Material sealMaterial;

    [Min(0.01f)]
    [SerializeField] private float sealThickness = 1f;

    // how many rooms behind the player an Entry has to be before it seals
    // 0 seals a room's Entry as soon as the player is through it.
    // 1 seals it once the player is through the room's Exit as well
    [Min(0)]
    [SerializeField] private int sealRoomsBehind = 1;

    // only used when Seal Rooms Behind is 0.
    // how far past the Entry gate the player has to be before it seals, so the wall never appears inside them
    [Min(0f)]
    [SerializeField] private float sealDistance = 2f;

    [Header("Debug")]
    // draws every room's footprint in the Scene view (green), and the last room that didn't fit (red).
    // off unless you want it
    [SerializeField] private bool drawFootprints = false;

    // draws the green and red gate rectangles and arrows on every room's gates in the Scene view.
    // untick to hide them all at once
    [SerializeField] private bool showGateGizmos = true;

    // how many rooms Simulate Rooms places (right-click the RoomChain component to run it)
    [Min(1)]
    [SerializeField] private int simulateRooms = 200;

    // the old single room list.
    // the rooms in it are moved into Forward Rooms the first time this is loaded in the editor
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

    // how many rooms the player has gone through in total, for the HUD.
    // unlike passedCount it never goes down when old rooms are deleted
    public int RoomsPassed { get; private set; }

    // fired the first time the player goes through the starter room's Exit (the music and the countdown timer start here)
    public event System.Action StarterExited;

    // how many rooms have had their Entry sealed.
    // rooms are sealed in order, oldest first
    private int sealedCount;

    // the starter room is room 0 and does not count toward RoomsPassed
    private bool hasStarter;
    private bool starterPassed;

    // the song's source and when it started, for the fade-in
    private AudioSource musicSource;
    private float musicStartTime;

    private void OnEnable()
    {
        RoomGate.ShowGizmos = showGateGizmos;
    }

    private void OnValidate()
    {
        RoomGate.ShowGizmos = showGateGizmos;

#if UNITY_EDITOR
        // empty slots get the project's own materials (found by their GUIDs, so renaming them is fine)
        if (plainGateMaterial == null)
            plainGateMaterial = LoadMaterial("9dd98d96b17ac804a93718596153719f");

        if (entryGateMaterial == null)
            entryGateMaterial = LoadMaterial("29cd6cfe2ff0bb24b977b82e53246ce0");

        if (exitGateMaterial == null)
            exitGateMaterial = LoadMaterial("c6b94c98f5c48fd4089cf05171c3751b");

        // not straight away: changing other objects while Unity is validating this one can be refused
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                ApplyGateLookToAll();
        };
#endif

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

        if (starterRoom != null)
        {
            if (planner.PlanStarter(starterRoom, out PlannedRoom starterPlan) && SpawnPlanned(starterPlan))
                hasStarter = true;
            else
                Debug.LogError($"The starter room '{starterRoom.name}' could not be placed (does it have an Entry and an Exit gate?). Starting with a normal room instead.", this);
        }

        BuildAhead();

        if (hasStarter)
            PlacePlayerAtStart();
    }

#if UNITY_EDITOR
    private static Material LoadMaterial(string guid)
    {
        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

        return string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
    }
#endif

    // the plain material on every gate, or the usual blue (Entry) and orange (Exit)
    private void ApplyGateLook(RoomGate gate)
    {
        Material material = plainGateLook
            ? plainGateMaterial
            : (gate.Type == GateType.Entry ? entryGateMaterial : exitGateMaterial);

        if (material == null)
            return;

        foreach (MeshRenderer meshRenderer in gate.GetComponentsInChildren<MeshRenderer>(true))
            meshRenderer.sharedMaterial = material;
    }

    private void ApplyGateLookToAll()
    {
        foreach (RoomGate gate in FindObjectsByType<RoomGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ApplyGateLook(gate);
    }

    // starts the song (once, no loop) the moment the player is out of the starter room
    private void PlayStartMusic()
    {
        if (startMusic == null)
            return;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = startMusic;
        musicSource.volume = musicFadeInSeconds > 0f ? 0f : musicVolume;
        musicSource.loop = false;
        musicSource.spatialBlend = 0f;
        musicSource.playOnAwake = false;
        musicSource.Play();

        musicStartTime = Time.unscaledTime;
    }

    // the fade-in: volume rises in a straight line from 0 to musicVolume over musicFadeInSeconds
    private void UpdateMusicFade()
    {
        if (musicSource == null)
            return;

        float fade = musicFadeInSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - musicStartTime) / musicFadeInSeconds) : 1f;
        musicSource.volume = musicVolume * fade;
    }

    // puts the player above the starter room's Entry, facing along its blue arrow and looking straight down
    private void PlacePlayerAtStart()
    {
        RoomGate entry = rooms[0].Entry;
        Vector3 forward = entry.WorldForward;

        CustomPlayerController controller = player != null ? player.GetComponent<CustomPlayerController>() : null;

        if (controller == null)
        {
            Debug.LogWarning("No player found, so it was not moved to the start.", this);
            return;
        }

        controller.Teleport(entry.transform.position + Vector3.up * spawnHeightAboveEntry + forward * spawnDistanceInFront);

        PlayerLook look = player.GetComponent<PlayerLook>();

        if (look != null)
            look.SetLook(forward, 89f);
    }

    private void Update()
    {
        UpdateMusicFade();

        if (player == null || rooms.Count == 0)
            return;

        // a room counts as passed once the player is in front of its Exit gate
        while (passedCount < rooms.Count && HasPassed(rooms[passedCount].Exit))
        {
            passedCount++;

            // the first room passed is the starter room, which is not counted
            if (hasStarter && !starterPassed)
            {
                starterPassed = true;
                PlayStartMusic();
                StarterExited?.Invoke();
            }
            else
                RoomsPassed++;
        }

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

        return SpawnPlanned(plan);
    }

    // builds the room the planner worked out.
    // false if the prefab has no Entry or Exit (the planner then forgets it)
    private bool SpawnPlanned(PlannedRoom plan)
    {
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

        foreach (RoomGate gate in room.GetComponentsInChildren<RoomGate>(true))
            ApplyGateLook(gate);

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

        // the starter room goes first, like in the game
        if (starterRoom != null)
            test.PlanStarter(starterRoom, out PlannedRoom starterPlan);

        for (int i = 0; i < simulateRooms; i++)
        {
            // like the live game, only the newest few rooms can be taken back, and the first room always stays
            if (!test.TryPlanNext(Mathf.Max(0, test.Count - 1 - roomsAhead), out PlannedRoom plan, out int removed))
                break;
        }

        simulated = test;

        // counted from the rooms that are left (rooms that were taken back and replaced don't count)
        int[] counts = new int[8];

        // how many times each room prefab was used, most used first
        Dictionary<Room, int> perRoom = new Dictionary<Room, int>();

        foreach (RoomPlanner.Record record in test.Records)
        {
            counts[(int)record.category]++;
            perRoom.TryGetValue(record.prefab, out int used);
            perRoom[record.prefab] = used + 1;
        }

        List<KeyValuePair<Room, int>> sorted = new List<KeyValuePair<Room, int>>(perRoom);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        string perRoomText = "";

        foreach (KeyValuePair<Room, int> pair in sorted)
            perRoomText += $"\n    {pair.Key.name}: {pair.Value}  ({100f * pair.Value / Mathf.Max(1, test.Count):F1}%)";

        Debug.Log(
            $"Simulated {test.Count} rooms (seed {usedSeed}):\n" +
            $"  overlapping pairs: {test.CountOverlaps()}  (should be 0, except where a room was forced)\n" +
            $"  rooms thrown away before one fit: {test.RejectedCount}  ({test.RejectedByPrevious} ran into the room before, {test.RejectedByOlder} into older rooms, {test.RejectedByClearance} had their exit clearance blocked)\n" +
            $"  times a room was taken back: {test.BacktrackCount}\n" +
            $"  rooms forced in anyway: {test.ForcedCount}\n" +
            $"  forward {counts[0]}, turns {counts[1]}, up transitions {counts[2]}, down transitions {counts[3]}, " +
            $"up rooms {counts[4]}, down rooms {counts[5]}, up returns {counts[6]}, down returns {counts[7]}\n" +
            $"  per room (most used first, a room that was never picked is missing):{perRoomText}",
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
