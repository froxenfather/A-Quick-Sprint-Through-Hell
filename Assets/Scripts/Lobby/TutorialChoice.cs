using UnityEngine;

// remembers that the player took the tutorial orb in the lobby, so the factory can read it after the scene changes.
// a static value survives a scene change, and it is used up by the first factory load that reads it: the orb turns the tutorial on for one load only
public static class TutorialChoice
{
    private static bool picked;

    // the orb was taken
    public static void Pick()
    {
        picked = true;
    }

    // true if the orb was taken since the last time this was asked.
    // asking clears it, so it counts for one factory load only
    public static bool Consume()
    {
        bool result = picked;
        picked = false;

        return result;
    }

    // the editor can keep static values from one Play session to the next, so every Play session starts without the orb taken
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        picked = false;
    }
}
