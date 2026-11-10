using UnityEngine;

// a pickup that heals the player fully when they walk into it: no burn, and the sprint speed back to what it was at the start
// needs a Collider set to Is Trigger. the player's own movement ignores triggers, so it can walk straight through
public class HealthPickup : MonoBehaviour
{
    [SerializeField] private AudioClip pickupSound;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    // seconds until it comes back after being taken. 0 = gone for good
    [Min(0f)]
    [SerializeField] private float respawnTime = 0f;

    [Header("Look")]
    // degrees per second it spins
    [SerializeField] private float spinSpeed = 90f;

    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 2f;

    private Vector3 startPosition;
    private bool available = true;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        if (!available)
            return;

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!available)
            return;

        PlayerBurn playerBurn = other.GetComponentInParent<PlayerBurn>();

        if (playerBurn == null)
            return;

        playerBurn.Heal();

        // PlayClipAtPoint makes its own temporary sound, so it keeps playing after we hide ourselves
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, volume);

        if (respawnTime > 0f)
        {
            SetAvailable(false);
            Invoke(nameof(Respawn), respawnTime);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Respawn()
    {
        SetAvailable(true);
    }

    // hides or shows the whole pickup (its looks and its trigger) without turning the script itself off
    private void SetAvailable(bool value)
    {
        available = value;

        foreach (Renderer pickupRenderer in GetComponentsInChildren<Renderer>())
            pickupRenderer.enabled = value;

        foreach (Collider pickupCollider in GetComponentsInChildren<Collider>())
            pickupCollider.enabled = value;
    }
}
