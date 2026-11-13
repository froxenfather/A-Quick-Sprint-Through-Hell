using System.Collections.Generic;
using UnityEngine;

// which list a room comes from.
// the first four are what the generator picks from normally, the other four are used inside an up or down section
public enum RoomCategory
{
    Forward,
    Turns,
    UpTransitions,
    DownTransitions,
    UpRooms,
    DownRooms,
    UpReturns,
    DownReturns
}

// where the generator is: building the normal level, or inside an up or down section
public enum ChainMode
{
    Level,
    Up,
    Down
}

// one slot in a room list: the room, and how likely it is to be picked
// 1 is normal, 0.5 is half as likely, 4 is four times as likely, 0 means never
[System.Serializable]
public class RoomEntry
{
    public Room room;

    [Range(0f, 10f)]
    public float weight = 1f;
}

// what a new room should be and where it goes, worked out before anything is spawned
public struct PlannedRoom
{
    public Room prefab;
    public RoomCategory category;
    public bool mirror;
    public Vector3 rootPosition;
    public Quaternion rootRotation;
    public Vector3 flip;

    // true if nothing fit and this room was placed anyway, as a last resort
    public bool forced;
}

public class RoomPlannerSettings
{
    // one list per RoomCategory, in the same order as the enum
    public RoomEntry[][] lists;

    public float mirrorChance;
    public int sectionRoomsMin;
    public int sectionRoomsMax;
    public Vector3 exitClearanceSize;

    // how far boxes are allowed to overlap before it counts: next to the room just before it (their gates meet and the frames overlap by design), and everywhere else
    public float gateTolerance;
    public float overlapTolerance;

    // how many rooms in a row it may take back while looking for a room that fits
    public int maxBacktracks;
}

// decides which room comes next and where it goes, without spawning anything:
//   1. every room in the lists for the current state (times its mirrored and unmirrored version) is a candidate
//   2. a candidate is dropped if its footprint, or the clear space in front of its Exit, overlaps a room that already exists
//   3. one of the candidates that are left is picked, by weight
//   4. if none are left, the last room is taken back and a different one is tried there
//   5. if even that fails, the best room is placed anyway (and a warning is logged), so the run never stops
// all pure geometry, so RoomChain uses it while playing and can also run it with no scene at all (Simulate Rooms)
public class RoomPlanner
{
    public struct ChainState
    {
        public ChainMode mode;

        // in an up or down section: how many rooms are left before the return transition
        public int roomsLeft;
    }

    // a room that has been placed
    public class Record
    {
        public Room prefab;
        public RoomCategory category;
        public bool mirror;
        public bool forced;

        // its footprint, in world space
        public List<OrientedBox> boxes;
        public Vector3 boundsCenter;
        public float boundsRadius;

        // where its Exit gate is, which is where the next room's Entry goes
        public Vector3 exitPosition;
        public Quaternion exitRotation;

        public ChainState stateAfter;
    }

    private class Candidate
    {
        public RoomEntry entry;
        public RoomCategory category;
        public RoomBlueprint blueprint;
        public bool mirror;
        public float weight;
        public Vector3 rootPosition;
        public Quaternion rootRotation;
        public Vector3 flip;
        public Matrix4x4 matrix;
        public List<OrientedBox> boxes;
        public Vector3 boundsCenter;
    }

    private struct Ban
    {
        public int index;
        public Room prefab;
        public bool mirror;
    }

    // why a candidate was thrown away
    private enum BlockReason
    {
        None,

        // it runs into the room it connects to (more than the join between the two gates allows)
        Previous,

        // it runs into an older room (a turn folding back on itself, for example)
        Older,

        // the clear space in front of its Exit is blocked
        Clearance
    }

    private static readonly RoomCategory[] LevelPool = { RoomCategory.Forward, RoomCategory.Turns, RoomCategory.UpTransitions, RoomCategory.DownTransitions };
    private static readonly RoomCategory[] UpPool = { RoomCategory.UpRooms };
    private static readonly RoomCategory[] DownPool = { RoomCategory.DownRooms };
    private static readonly RoomCategory[] UpReturnPool = { RoomCategory.UpReturns };
    private static readonly RoomCategory[] DownReturnPool = { RoomCategory.DownReturns };

    private readonly RoomPlannerSettings settings;
    private readonly System.Random random;
    private readonly Vector3 startPosition;
    private readonly Quaternion startRotation;

    private readonly List<Record> records = new List<Record>();
    private readonly Dictionary<Room, RoomBlueprint> blueprints = new Dictionary<Room, RoomBlueprint>();
    private readonly List<Candidate> candidates = new List<Candidate>();
    private readonly List<Ban> bans = new List<Ban>();
    private readonly List<OrientedBox> rejectedBoxes = new List<OrientedBox>();

    // the rooms placed so far, oldest first.
    // same order as RoomChain's rooms
    public IReadOnlyList<Record> Records => records;
    public int Count => records.Count;

    // the footprint of the last candidate that was thrown away, for drawing in the Scene view
    public IReadOnlyList<OrientedBox> RejectedBoxes => rejectedBoxes;

    // how often each thing happened, for the Simulate Rooms report
    public int RejectedCount { get; private set; }
    public int RejectedByPrevious { get; private set; }
    public int RejectedByOlder { get; private set; }
    public int RejectedByClearance { get; private set; }
    public int BacktrackCount { get; private set; }
    public int ForcedCount { get; private set; }
    public int[] CategoryCounts { get; } = new int[8];

    // the same three reasons, for the search that is running right now (used in the warning when a room has to be forced)
    private int searchPrevious;
    private int searchOlder;
    private int searchClearance;

    public RoomPlanner(RoomPlannerSettings settings, System.Random random, Vector3 startPosition, Quaternion startRotation)
    {
        this.settings = settings;
        this.random = random;
        this.startPosition = startPosition;
        this.startRotation = startRotation;
    }

    // true if the normal level has at least one room it could pick
    public bool HasLevelRooms()
    {
        foreach (RoomCategory category in LevelPool)
        {
            if (HasUsable(category))
                return true;
        }

        return false;
    }

    // places a fixed first room (the starter room) with its Entry on the start of the chain, so every later room is checked against it
    // it is committed as forced, so backtracking never takes it back.
    // it doesn't come from a list, so it is left out of the per-list counts
    public bool PlanStarter(Room prefab, out PlannedRoom planned)
    {
        planned = default;

        if (prefab == null || records.Count > 0)
            return false;

        RoomBlueprint blueprint = GetBlueprint(prefab);

        if (blueprint == null)
            return false;

        GetTarget(out Vector3 position, out Quaternion rotation);

        RoomEntry entry = new RoomEntry { room = prefab };
        Candidate candidate = MakeCandidate(entry, RoomCategory.Forward, blueprint, false, 1f, position, rotation);

        planned = Commit(candidate, CurrentState, true);
        CategoryCounts[(int)RoomCategory.Forward]--;

        return true;
    }

    // plans the next room.
    // lastLockedIndex is the newest room that must stay (the one the player is in), anything after it can be taken back
    // removed is how many rooms were taken back from the end first, the caller has to remove the same number of spawned rooms
    public bool TryPlanNext(int lastLockedIndex, out PlannedRoom planned, out int removed)
    {
        removed = 0;
        bans.Clear();

        searchPrevious = 0;
        searchOlder = 0;
        searchClearance = 0;

        while (true)
        {
            GetTarget(out Vector3 position, out Quaternion rotation);
            ChainState state = CurrentState;

            BuildCandidates(state, position, rotation);

            if (candidates.Count > 0)
            {
                planned = Commit(Draw(), state, false);
                return true;
            }

            // nothing fits: take the last room back and try something else there
            // (never a room the player has reached, never a room that was forced in, and only so many in a row)
            // a forced room staying put is what stops this from taking back and forcing the same rooms forever
            if (removed < settings.maxBacktracks && records.Count - 1 > lastLockedIndex && !records[records.Count - 1].forced)
            {
                Record last = records[records.Count - 1];

                // bans for positions after the one we are stepping back to no longer apply
                bans.RemoveAll(ban => ban.index >= records.Count);

                records.RemoveAt(records.Count - 1);
                bans.Add(new Ban { index = records.Count, prefab = last.prefab, mirror = last.mirror });

                removed++;
                BacktrackCount++;
                continue;
            }

            return TryForce(out planned);
        }
    }

    // forgets the oldest room, because RoomChain deleted it behind the player
    public void RemoveOldest()
    {
        if (records.Count > 0)
            records.RemoveAt(0);
    }

    // forgets the newest room, if it could not be spawned
    public void DropLast()
    {
        if (records.Count > 0)
            records.RemoveAt(records.Count - 1);
    }

    // how many pairs of rooms overlap, checked every room against every other (the live game only checks the rooms that exist)
    public int CountOverlaps()
    {
        int overlaps = 0;

        for (int i = 0; i < records.Count; i++)
        {
            for (int j = i + 1; j < records.Count; j++)
            {
                if (RecordsOverlap(records[i], records[j], j == i + 1))
                    overlaps++;
            }
        }

        return overlaps;
    }

    // ============================================================
    // STATE
    // ============================================================

    private ChainState CurrentState
    {
        get
        {
            if (records.Count == 0)
                return new ChainState { mode = ChainMode.Level, roomsLeft = 0 };

            return records[records.Count - 1].stateAfter;
        }
    }

    // the Exit of the last room, or the start of the chain if there are no rooms yet
    private void GetTarget(out Vector3 position, out Quaternion rotation)
    {
        if (records.Count == 0)
        {
            position = startPosition;
            rotation = startRotation;
            return;
        }

        Record last = records[records.Count - 1];
        position = last.exitPosition;
        rotation = last.exitRotation;
    }

    // which lists the next room may come from
    private static RoomCategory[] PoolFor(ChainState state)
    {
        switch (state.mode)
        {
            case ChainMode.Up:
                return state.roomsLeft > 0 ? UpPool : UpReturnPool;

            case ChainMode.Down:
                return state.roomsLeft > 0 ? DownPool : DownReturnPool;

            default:
                return LevelPool;
        }
    }

    // the state after a room from this list is placed
    private ChainState NextState(ChainState state, RoomCategory category)
    {
        if (state.mode == ChainMode.Level)
        {
            // a transition room starts a section, and rolls how many rooms it lasts
            if (category == RoomCategory.UpTransitions)
                return new ChainState { mode = ChainMode.Up, roomsLeft = RollSectionLength() };

            if (category == RoomCategory.DownTransitions)
                return new ChainState { mode = ChainMode.Down, roomsLeft = RollSectionLength() };

            return state;
        }

        // inside a section: each room uses one up, and the return transition after the last one ends it
        if (state.roomsLeft > 0)
            return new ChainState { mode = state.mode, roomsLeft = state.roomsLeft - 1 };

        return new ChainState { mode = ChainMode.Level, roomsLeft = 0 };
    }

    private int RollSectionLength()
    {
        return random.Next(settings.sectionRoomsMin, settings.sectionRoomsMax + 1);
    }

    private static bool IsUsable(RoomEntry entry)
    {
        return entry != null && entry.room != null && entry.weight > 0f;
    }

    private bool HasUsable(RoomCategory category)
    {
        RoomEntry[] list = settings.lists[(int)category];

        if (list == null)
            return false;

        foreach (RoomEntry entry in list)
        {
            if (IsUsable(entry))
                return true;
        }

        return false;
    }

    // a transition room is only worth picking if there are rooms for the section and a way back out of it
    private bool CanStartSection(RoomCategory transition)
    {
        if (transition == RoomCategory.UpTransitions)
            return HasUsable(RoomCategory.UpRooms) && HasUsable(RoomCategory.UpReturns);

        if (transition == RoomCategory.DownTransitions)
            return HasUsable(RoomCategory.DownRooms) && HasUsable(RoomCategory.DownReturns);

        return true;
    }

    // ============================================================
    // CANDIDATES
    // ============================================================

    private void BuildCandidates(ChainState state, Vector3 position, Quaternion rotation)
    {
        candidates.Clear();
        rejectedBoxes.Clear();

        foreach (RoomCategory category in PoolFor(state))
        {
            if (!CanStartSection(category))
                continue;

            RoomEntry[] list = settings.lists[(int)category];

            if (list == null)
                continue;

            foreach (RoomEntry entry in list)
            {
                if (!IsUsable(entry))
                    continue;

                RoomBlueprint blueprint = GetBlueprint(entry.room);

                if (blueprint == null)
                    continue;

                // a room that can't mirror only has the one version.
                // one that can has both, split by mirrorChance
                float plainWeight = entry.room.CanMirror ? 1f - settings.mirrorChance : 1f;
                float mirroredWeight = entry.room.CanMirror ? settings.mirrorChance : 0f;

                TryAddCandidate(entry, category, blueprint, false, entry.weight * plainWeight, position, rotation);
                TryAddCandidate(entry, category, blueprint, true, entry.weight * mirroredWeight, position, rotation);
            }
        }
    }

    private void TryAddCandidate(RoomEntry entry, RoomCategory category, RoomBlueprint blueprint, bool mirror, float weight, Vector3 position, Quaternion rotation)
    {
        if (weight <= 0f)
            return;

        // a pick that was already tried here and led nowhere
        foreach (Ban ban in bans)
        {
            if (ban.index == records.Count && ban.prefab == entry.room && ban.mirror == mirror)
                return;
        }

        Candidate candidate = MakeCandidate(entry, category, blueprint, mirror, weight, position, rotation);

        BlockReason reason = GetBlockReason(candidate, rotation);

        if (reason != BlockReason.None)
        {
            RejectedCount++;

            if (reason == BlockReason.Previous)
            {
                RejectedByPrevious++;
                searchPrevious++;
            }
            else if (reason == BlockReason.Older)
            {
                RejectedByOlder++;
                searchOlder++;
            }
            else
            {
                RejectedByClearance++;
                searchClearance++;
            }

            rejectedBoxes.Clear();
            rejectedBoxes.AddRange(candidate.boxes);

            return;
        }

        candidates.Add(candidate);
    }

    private static Candidate MakeCandidate(RoomEntry entry, RoomCategory category, RoomBlueprint blueprint, bool mirror, float weight, Vector3 position, Quaternion rotation)
    {
        Candidate candidate = new Candidate();
        candidate.entry = entry;
        candidate.category = category;
        candidate.blueprint = blueprint;
        candidate.mirror = mirror;
        candidate.weight = weight;

        RoomBlueprint.ComputePlacement(
            blueprint.entryPosition, blueprint.entryRotation, position, rotation, mirror,
            out candidate.rootPosition, out candidate.rootRotation, out candidate.flip);

        candidate.matrix = Matrix4x4.TRS(candidate.rootPosition, candidate.rootRotation, candidate.flip);
        candidate.boundsCenter = candidate.matrix.MultiplyPoint3x4(blueprint.boundsCenter);

        candidate.boxes = new List<OrientedBox>(blueprint.boxes.Count);

        foreach (OrientedBox box in blueprint.boxes)
            candidate.boxes.Add(box.Transformed(candidate.matrix));

        return candidate;
    }

    private RoomBlueprint GetBlueprint(Room prefab)
    {
        if (!blueprints.TryGetValue(prefab, out RoomBlueprint blueprint))
        {
            blueprint = RoomBlueprint.Build(prefab);

            // stored even when null, so a broken room only complains once
            blueprints[prefab] = blueprint;
        }

        return blueprint;
    }

    // why the candidate can't go here: its footprint, or the clear space in front of its Exit, runs into a room that already exists
    // BlockReason.None means it fits
    private BlockReason GetBlockReason(Candidate candidate, Quaternion targetRotation)
    {
        OrientedBox clearance = candidate.blueprint.ExitClearanceBox(settings.exitClearanceSize).Transformed(candidate.matrix);
        float clearanceRadius = clearance.Radius;

        // the room we connect to has its end walls and gate frame right on the join, and so does this one
        // so before testing against it, this room's boxes are pushed forward (the way the player travels) by the gate tolerance
        // the join stops counting, but a room that runs alongside or back over the one before it still does
        Vector3 joinShift = targetRotation * Vector3.forward * settings.gateTolerance;

        for (int r = 0; r < records.Count; r++)
        {
            Record record = records[r];

            if (record.boxes.Count == 0)
                continue;

            bool isPrevious = r == records.Count - 1;
            Vector3 shift = isPrevious ? joinShift : Vector3.zero;

            bool nearFootprint = Vector3.Distance(candidate.boundsCenter + shift, record.boundsCenter) <= candidate.blueprint.boundsRadius + record.boundsRadius;
            bool nearClearance = Vector3.Distance(clearance.center, record.boundsCenter) <= clearanceRadius + record.boundsRadius;

            if (!nearFootprint && !nearClearance)
                continue;

            foreach (OrientedBox box in record.boxes)
            {
                OrientedBox existing = box.Shrunk(settings.overlapTolerance);

                if (nearClearance && existing.Overlaps(clearance))
                    return BlockReason.Clearance;

                if (!nearFootprint)
                    continue;

                foreach (OrientedBox own in candidate.boxes)
                {
                    OrientedBox moved = own;
                    moved.center += shift;

                    if (existing.Overlaps(moved))
                        return isPrevious ? BlockReason.Previous : BlockReason.Older;
                }
            }
        }

        return BlockReason.None;
    }

    // adjacent is true for two rooms that connect to each other, which get the same join allowance as in GetBlockReason
    private bool RecordsOverlap(Record older, Record newer, bool adjacent)
    {
        Vector3 shift = adjacent ? older.exitRotation * Vector3.forward * settings.gateTolerance : Vector3.zero;

        if (Vector3.Distance(older.boundsCenter, newer.boundsCenter + shift) > older.boundsRadius + newer.boundsRadius)
            return false;

        foreach (OrientedBox box in older.boxes)
        {
            OrientedBox shrunk = box.Shrunk(settings.overlapTolerance);

            foreach (OrientedBox other in newer.boxes)
            {
                OrientedBox moved = other;
                moved.center += shift;

                if (shrunk.Overlaps(moved))
                    return true;
            }
        }

        return false;
    }

    // one weighted pick from the candidates that fit
    private Candidate Draw()
    {
        float total = 0f;

        foreach (Candidate candidate in candidates)
            total += candidate.weight;

        double roll = random.NextDouble() * total;

        foreach (Candidate candidate in candidates)
        {
            roll -= candidate.weight;

            if (roll <= 0.0)
                return candidate;
        }

        return candidates[candidates.Count - 1];
    }

    // makes the candidate a real room in the list, and works out the state after it
    private PlannedRoom Commit(Candidate candidate, ChainState state, bool forced)
    {
        Matrix4x4 exitWorld = candidate.matrix * candidate.blueprint.exitMatrix;

        Record record = new Record();
        record.prefab = candidate.entry.room;
        record.category = candidate.category;
        record.mirror = candidate.mirror;
        record.forced = forced;
        record.boxes = candidate.boxes;
        record.boundsCenter = candidate.boundsCenter;
        record.boundsRadius = candidate.blueprint.boundsRadius;
        record.exitPosition = exitWorld.MultiplyPoint3x4(Vector3.zero);

        // the same direction RoomGate.WorldRotation gives for a spawned gate
        record.exitRotation = Quaternion.LookRotation(
            exitWorld.MultiplyVector(Vector3.forward).normalized,
            exitWorld.MultiplyVector(Vector3.up).normalized);

        record.stateAfter = NextState(state, candidate.category);

        records.Add(record);
        CategoryCounts[(int)candidate.category]++;

        PlannedRoom planned = new PlannedRoom();
        planned.prefab = record.prefab;
        planned.category = candidate.category;
        planned.mirror = candidate.mirror;
        planned.rootPosition = candidate.rootPosition;
        planned.rootRotation = candidate.rootRotation;
        planned.flip = candidate.flip;
        planned.forced = forced;

        return planned;
    }

    // the last resort: place the most likely room for this state, even though it overlaps something
    private bool TryForce(out PlannedRoom planned)
    {
        planned = default;

        GetTarget(out Vector3 position, out Quaternion rotation);
        ChainState state = CurrentState;

        RoomEntry best = null;
        RoomCategory bestCategory = RoomCategory.Forward;

        foreach (RoomCategory category in PoolFor(state))
        {
            RoomEntry[] list = settings.lists[(int)category];

            if (list == null)
                continue;

            foreach (RoomEntry entry in list)
            {
                if (IsUsable(entry) && (best == null || entry.weight > best.weight) && GetBlueprint(entry.room) != null)
                {
                    best = entry;
                    bestCategory = category;
                }
            }
        }

        if (best == null)
            return false;

        Candidate candidate = MakeCandidate(best, bestCategory, GetBlueprint(best.room), false, best.weight, position, rotation);

        ForcedCount++;

        // only the first few, so a layout nothing fits can't flood the Console
        if (ForcedCount <= 5)
        {
            Debug.LogWarning(
                $"No room fit, so '{best.room.name}' was placed anyway and overlaps something. " +
                $"This search threw away {searchPrevious} picks that ran into the room before, {searchOlder} that ran into older rooms, and {searchClearance} whose exit clearance was blocked. " +
                "Mostly 'the room before' means the footprints stick out past the gates (raise Gate Tolerance, or shrink the footprints near the gates). " +
                "Mostly 'older rooms' or 'clearance' means the lists need more variety (turns that go the other way, more forward rooms).",
                best.room);
        }

        planned = Commit(candidate, state, true);
        return true;
    }
}
