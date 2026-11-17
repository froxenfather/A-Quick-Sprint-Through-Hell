using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

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

    // the song that starts each time the run starts (when the player leaves a safe room) and plays once (it does not loop).
    // starting it again restarts it from the beginning.
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

    // seconds the song takes to fade out to silence when the player goes into a safe room, and then it stops.
    // 0 stops it at once
    [Range(0f, 30f)]
    [SerializeField] private float musicFadeOutSeconds = 2f;

    [Header("Tutorial")]
    // rooms that are built right after the starter room, in this order, before any random room.
    // the last one should be a safe room (tick Safe Room on its Room component), because leaving the first safe room is what starts the run.
    // leave empty to skip the tutorial: the run then starts when the player leaves the starter room
    [SerializeField] private Room[] tutorialRooms = new Room[0];

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

    // how far (meters) in front of a room's Exit door the next room's Entry door goes.
    // the gate frames are 1 m deep, so 1 makes them touch face to face and 0 puts them on the same spot
    [Header("Gate spacing")]
    [Min(0f)]
    [SerializeField] private float gateDepth = 1f;

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
    // ticked: every room that spawns gets the gate material below on all of its gates, entrances and exits alike.
    // unticked: gates keep the colors they have in their prefabs (blue entrances, orange exits)
    [SerializeField] private bool cautionGates = true;

    // the material for the gates (the caution stripes).
    // filled in with caution_stripes in the editor if it is left empty, and ProBuilder's own default is used if there is still nothing
    [SerializeField] private Material gateMaterial;

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
    // starts the countdown timer at Debug Timer Seconds instead of the full time, and doesn't play the song.
    // that way the end of a run (Conseraph becoming enraged) can be watched without waiting.
    // the timer still starts when the player leaves the starter room.
    // leave this off for the real game
    [SerializeField] private bool debugMode = false;

    // where the timer starts from while debug mode is on (seconds)
    [Min(1f)]
    [SerializeField] private float debugTimerSeconds = 5f;

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

    // the GUID of Assets/Materials/caution_stripes.mat
    private const string CautionMaterialGuid = "f5840fd810bb4eca8cab6144c5f23efc";

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

    // debug mode (see the Debug section).
    // the countdown timer reads these when it starts
    public bool DebugMode => debugMode;
    public float DebugTimerSeconds => debugTimerSeconds;

    // fired the first time the player goes through the starter room's Exit (out of the chute)
    public event System.Action StarterExited;

    // fired when the run starts: the player left a safe room (or the starter room, when there is no tutorial).
    // the music, the countdown timer and Conseraph's hunt all start from this
    public event System.Action RunStarted;

    // fired when the player goes into a safe room.
    // the countdown timer says you're good and Conseraph is paused
    public event System.Action SafeRoomEntered;

    // true while the run is going: from leaving a safe room until the next one is entered
    public bool RunActive { get; private set; }

    // fired every time the player clears a room (goes through its Exit), not counting the starter room.
    // the player's burn heals one level for each
    public event System.Action RoomCleared;

    // how many rooms have had their Entry sealed.
    // rooms are sealed in order, oldest first
    private int sealedCount;

    // the starter room is room 0 and does not count toward RoomsPassed
    private bool hasStarter;
    private bool starterPassed;

    // the tutorial has at least one room, and how many of its rooms have been built so far
    private bool hasTutorial;
    private int nextTutorial;

    // the song's source and when it started, for the fade-in
    private AudioSource musicSource;
    private float musicStartTime;

    // for the fade-out when a safe room is entered: whether it is happening, when it began, and the volume it started from
    private bool musicFadingOut;
    private float musicFadeOutStartTime;
    private float musicFadeOutStartVolume;

    private void OnEnable()
    {
        RoomGate.ShowGizmos = showGateGizmos;
    }

    private void OnValidate()
    {
        RoomGate.ShowGizmos = showGateGizmos;

#if UNITY_EDITOR
        // found by the material's GUID, so renaming or moving it is fine
        if (gateMaterial == null)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(CautionMaterialGuid);

            if (!string.IsNullOrEmpty(path))
                gateMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        }
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

        // so it is never left on by accident
        if (debugMode)
            Debug.LogWarning($"RoomChain debug mode is ON: the timer starts at {debugTimerSeconds} seconds and the song doesn't play. Untick Debug Mode on the RoomChain for the real game.", this);

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

        CheckTutorial();

        BuildAhead();

        if (hasStarter)
            PlacePlayerAtStart();
    }

    // finds out whether there is a tutorial at all, and warns if it doesn't end in a safe room (the run would never start)
    private void CheckTutorial()
    {
        Room last = null;

        if (tutorialRooms != null)
        {
            foreach (Room room in tutorialRooms)
            {
                if (room != null)
                    last = room;
            }
        }

        hasTutorial = last != null;

        if (hasTutorial && !last.IsSafeRoom)
            Debug.LogWarning($"The last tutorial room '{last.name}' is not a safe room, so the run will never start. Tick Safe Room on its Room component.", this);
    }

    // swaps the material of every gate in a room for the gate material
    private void ApplyGateMaterial(Room room)
    {
        Material material = gateMaterial != null ? gateMaterial : BuiltinMaterials.defaultMaterial;

        foreach (RoomGate gate in room.GetComponentsInChildren<RoomGate>(true))
        {
            foreach (MeshRenderer meshRenderer in gate.GetComponentsInChildren<MeshRenderer>(true))
                meshRenderer.sharedMaterial = material;
        }
    }

    // starts the song (once, no loop) each time the run starts
    private void PlayStartMusic()
    {
        // debug mode keeps the song quiet
        if (debugMode || startMusic == null)
            return;

        // one source for the whole game, so starting the song again restarts it instead of stacking a second one on top
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = false;
            musicSource.spatialBlend = 0f;
            musicSource.playOnAwake = false;
        }

        // a run starting in the middle of a fade-out cancels it
        musicFadingOut = false;

        musicSource.Stop();
        musicSource.clip = startMusic;
        musicSource.volume = musicFadeInSeconds > 0f ? 0f : musicVolume;
        musicSource.Play();

        musicStartTime = Time.unscaledTime;
    }

    // fades the song out to silence and stops it, from wherever its volume is now.
    // nothing happens if it isn't playing (it may have finished already, or there is no song)
    private void StopMusic()
    {
        if (musicSource == null || !musicSource.isPlaying)
            return;

        if (musicFadeOutSeconds <= 0f)
        {
            musicSource.Stop();
            return;
        }

        musicFadingOut = true;
        musicFadeOutStartTime = Time.unscaledTime;
        musicFadeOutStartVolume = musicSource.volume;
    }

    // the fade-out: volume falls in a straight line to 0 over musicFadeOutSeconds, then the song stops
    // the fade-in: volume rises in a straight line from 0 to musicVolume over musicFadeInSeconds
    private void UpdateMusicFade()
    {
        if (musicSource == null)
            return;

        if (musicFadingOut)
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - musicFadeOutStartTime) / musicFadeOutSeconds);
            musicSource.volume = Mathf.Lerp(musicFadeOutStartVolume, 0f, progress);

            if (progress >= 1f)
            {
                musicSource.Stop();
                musicFadingOut = false;
            }

            return;
        }

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
            Room left = rooms[passedCount];
            passedCount++;

            // the next room is always built ahead already, so this is the room the player just went into
            Room entered = passedCount < rooms.Count ? rooms[passedCount] : null;

            OnRoomPassed(left, entered);
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

    // the player went through the Exit of left, and is now in entered
    private void OnRoomPassed(Room left, Room entered)
    {
        // the first room passed is the starter room (the chute), which is not counted and heals nothing
        if (hasStarter && !starterPassed)
        {
            starterPassed = true;
            StarterExited?.Invoke();

            // with no tutorial there is no safe room to wait for, so leaving the chute starts the run
            if (!hasTutorial)
                StartRun();
        }
        else
        {
            // only rooms of a running game count: never a tutorial room, and never a safe room
            if (RunActive && !left.IsSafeRoom)
                RoomsPassed++;

            RoomCleared?.Invoke();
        }

        // coming out of a safe room starts the run (or restarts it)
        if (left.IsSafeRoom)
            StartRun();

        // going into a safe room pauses it
        if (entered != null && entered.IsSafeRoom)
            PauseRun();
    }

    // the run starts: the song, the timer, and Conseraph's hunt.
    // the same thing every time, whether it is the first safe room or a later one
    private void StartRun()
    {
        RunActive = true;

        PlayStartMusic();
        RunStarted?.Invoke();
    }

    // the player is in a safe room: the timer says you're good and Conseraph is paused.
    // the song fades out and stops
    private void PauseRun()
    {
        RunActive = false;

        StopMusic();

        SafeRoomEntered?.Invoke();
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

    // the next tutorial room, placed exactly where the chain ends.
    // an empty slot is skipped
    private bool SpawnTutorial()
    {
        Room prefab = tutorialRooms[nextTutorial];
        nextTutorial++;

        if (prefab == null)
        {
            Debug.LogWarning($"Tutorial Rooms slot {nextTutorial - 1} is empty, so it is skipped.", this);
            return true;
        }

        if (!planner.PlanFixed(prefab, out PlannedRoom plan))
        {
            Debug.LogError($"The tutorial room '{prefab.name}' could not be placed (does it have an Entry and an Exit gate?).", this);
            return false;
        }

        return SpawnPlanned(plan);
    }

    private bool SpawnNext()
    {
        // the tutorial rooms come first, in order, before anything random
        if (tutorialRooms != null && nextTutorial < tutorialRooms.Length)
            return SpawnTutorial();

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

        if (cautionGates)
            ApplyGateMaterial(room);

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
        settings.gateDepth = gateDepth;

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

        // the tutorial rooms follow it, like in the game
        if (tutorialRooms != null)
        {
            foreach (Room tutorial in tutorialRooms)
            {
                if (tutorial != null)
                    test.PlanFixed(tutorial, out PlannedRoom tutorialPlan);
            }
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
