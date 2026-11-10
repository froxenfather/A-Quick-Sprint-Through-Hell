using System;
using UnityEngine;

// burn damage from hazards (lava, electric walls). put this on the Player, next to CustomPlayerController

// every touch of a SpeedCapSurface:
//   - adds burnPerTouch to the burn level (0 = fine, 1 = fully burnt), which BurnOverlay shows on screen
//   - lowers the player's max sprint speed by sprintSpeedLostPerTouch, but never below their walk speed
// Heal() puts both back: no burn, and the sprint speed the game started with
public class PlayerBurn : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;

    // how much one touch adds to the burn level. 0.1 = ten touches to be fully burnt
    [Range(0.01f, 1f)]
    [SerializeField] private float burnPerTouch = 0.1f;

    // max sprint speed (m/s) lost per touch. it never drops below the walk speed
    [Min(0f)]
    [SerializeField] private float sprintSpeedLostPerTouch = 1f;

    // touches closer together than this (seconds) count once, so one landing on lava can't burn several times
    [Min(0f)]
    [SerializeField] private float touchCooldown = 0.4f;

    private float burn;
    private float lastTouchTime = -999f;

    // 0 to 1
    public float Burn => burn;

    // fired for every touch that counted, with the surface that did it
    public event Action<SpeedCapSurface> Burned;

    // fired when a pickup heals us
    public event Action Healed;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<CustomPlayerController>();
    }

    private void OnEnable()
    {
        player.HazardTouched += OnHazardTouched;
    }

    private void OnDisable()
    {
        player.HazardTouched -= OnHazardTouched;
    }

    private void OnHazardTouched(SpeedCapSurface surface)
    {
        if (Time.time - lastTouchTime < touchCooldown)
            return;

        lastTouchTime = Time.time;

        burn = Mathf.Min(1f, burn + burnPerTouch);
        player.ReduceSprintSpeed(sprintSpeedLostPerTouch, player.WalkSpeed);

        Burned?.Invoke(surface);
    }

    // back to full health: no burn, and the sprint speed we started the game with
    public void Heal()
    {
        burn = 0f;
        player.ResetSprintSpeed();

        Healed?.Invoke();
    }
}
