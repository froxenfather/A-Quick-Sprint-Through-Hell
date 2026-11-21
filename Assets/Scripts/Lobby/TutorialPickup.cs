using UnityEngine;

// the tutorial orb in the lobby.
// when the player walks into it, the next factory they load runs the tutorial (see TutorialChoice and RoomChain).
// needs a Collider set to Is Trigger.
// the player's own movement ignores triggers, so it can walk straight through
public class TutorialPickup : MonoBehaviour
{
    [SerializeField] private AudioClip pickupSound;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    [Header("Look")]
    // degrees per second it spins.
    // 0 keeps it still
    [SerializeField] private float spinSpeed = 90f;

    // how far it moves up and down, and how fast.
    // 0 keeps it still
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 2f;

    private Vector3 startPosition;
    private bool taken;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (taken)
            return;

        if (other.GetComponentInParent<CustomPlayerController>() == null)
            return;

        taken = true;
        TutorialChoice.Pick();

        Debug.Log("Tutorial orb taken: the next factory load will run the tutorial.", this);

        // PlayClipAtPoint makes its own temporary sound, so it keeps playing after the orb is gone
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, volume);

        Destroy(gameObject);
    }
}

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
