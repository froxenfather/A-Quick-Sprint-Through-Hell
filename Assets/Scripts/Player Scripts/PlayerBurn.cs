using System;
using UnityEngine;

// burn damage from hazards (lava, electric walls).
// put this on the Player, next to CustomPlayerController

// the player has singe levels, from 0 (fine) up to a maximum:
//   - every touch of a SpeedCapSurface adds one singe level and lowers the max sprint speed by sprintSpeedLostPerTouch
//   - the maximum is reached when the sprint speed is down to the walk speed, and that is the most damage there is.
//     it starts from whatever Sprint Speed is set to in the Inspector (25 and a walk speed of 12 is 13 levels)
//   - touching a hazard at the maximum still hurts (the hazard's speed cap and bounce still happen) but adds no more singe
//   - clearing a room takes one singe level away (RoomChain.RoomCleared), and a health pickup takes them all away
// Burn (0 to 1) is singe / maximum, which BurnOverlay shows on screen
public class PlayerBurn : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;

    // max sprint speed (m/s) lost per singe level.
    // the number of levels is (sprint speed - walk speed) / this
    [Min(0.1f)]
    [SerializeField] private float sprintSpeedLostPerTouch = 1f;

    // touches closer together than this (seconds) count once, so one landing on lava can't burn several times
    [Min(0f)]
    [SerializeField] private float touchCooldown = 0.4f;

    private RoomChain roomChain;
    private int singe;
    private float lastTouchTime = -999f;

    // the most singe levels there are: the sprint speed has run out of room to drop
    public int MaxSinge => Mathf.CeilToInt((player.StartSprintSpeed - player.WalkSpeed) / sprintSpeedLostPerTouch);

    public int Singe => singe;

    // 0 (fine) to 1 (the most burnt there is)
    public float Burn => MaxSinge > 0 ? (float)singe / MaxSinge : 0f;

    // fired for every touch that counted, even at the maximum (it still hurts), with the surface that did it
    public event Action<SpeedCapSurface> Burned;

    // fired when a touch added a singe level
    public event Action Singed;

    // fired when one singe level was taken away but some are left
    public event Action Soothed;

    // fired when the last of the singe is gone (a pickup, or the last level cleared by a room)
    public event Action Healed;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<CustomPlayerController>();
    }

    private void OnEnable()
    {
        player.HazardTouched += OnHazardTouched;

        // the lobby has no RoomChain, so there are no rooms to clear there
        roomChain = FindFirstObjectByType<RoomChain>();

        if (roomChain != null)
            roomChain.RoomCleared += OnRoomCleared;
    }

    private void OnDisable()
    {
        player.HazardTouched -= OnHazardTouched;

        if (roomChain != null)
            roomChain.RoomCleared -= OnRoomCleared;
    }

    private void OnHazardTouched(SpeedCapSurface surface)
    {
        if (Time.time - lastTouchTime < touchCooldown)
            return;

        lastTouchTime = Time.time;

        Burned?.Invoke(surface);

        // at the maximum it still hurts, but there is no more singe to add
        if (singe >= MaxSinge)
            return;

        singe++;
        UpdateSprintSpeed();

        Singed?.Invoke();
    }

    // each room cleared takes one singe level away. at 0 there is nothing to take, so rooms do nothing
    private void OnRoomCleared()
    {
        if (singe == 0)
            return;

        singe--;
        UpdateSprintSpeed();

        if (singe == 0)
            Healed?.Invoke();
        else
            Soothed?.Invoke();
    }

    // back to full health: no singe, and the sprint speed we started the game with
    public void Heal()
    {
        singe = 0;
        UpdateSprintSpeed();

        Healed?.Invoke();
    }

    // the sprint speed is the starting one minus what the singe levels have taken (SetSprintSpeed keeps it from dropping below the walk speed)
    private void UpdateSprintSpeed()
    {
        player.SetSprintSpeed(player.StartSprintSpeed - singe * sprintSpeedLostPerTouch);
    }
}
