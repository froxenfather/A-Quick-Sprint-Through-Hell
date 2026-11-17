using System.Collections.Generic;
using UnityEngine;

// counts what the player does during a run, for the stats screen when they die.
// it starts counting when the run starts (the first time the player leaves a safe room, the same moment as the timer), so the chute and the tutorial don't count.
// it only reads the game, so it can be removed without breaking anything.
// it is added for you when you add PlayerDeath to the Player
[RequireComponent(typeof(CustomPlayerController))]
public class RunStats : MonoBehaviour
{
    private CustomPlayerController player;
    private PlayerBurn playerBurn;
    private RoomChain roomChain;

    private bool running;
    private bool hasRoomChain;
    private Vector3 lastPosition;

    // the seconds spent being hunted: only counted while the run is active, never in the chute, the tutorial or a safe room
    private float huntedSeconds;

    private int jumps;
    private int wallJumps;
    private int dashes;
    private int timesBurned;

    private float topSpeed;
    private float distance;
    private float wallRunSeconds;
    private float slideSeconds;

    private void Awake()
    {
        player = GetComponent<CustomPlayerController>();
        playerBurn = GetComponent<PlayerBurn>();
        roomChain = FindFirstObjectByType<RoomChain>();
        hasRoomChain = roomChain != null;
    }

    private void OnEnable()
    {
        player.Jumped += OnJumped;
        player.WallJumped += OnWallJumped;
        player.Dashed += OnDashed;

        if (playerBurn != null)
            playerBurn.Burned += OnBurned;

        // with no rooms in the scene there is no run to wait for, so it counts from the start
        if (roomChain != null)
            roomChain.RunStarted += StartRun;
        else
            StartRun();
    }

    private void OnDisable()
    {
        player.Jumped -= OnJumped;
        player.WallJumped -= OnWallJumped;
        player.Dashed -= OnDashed;

        if (playerBurn != null)
            playerBurn.Burned -= OnBurned;

        if (roomChain != null)
            roomChain.RunStarted -= StartRun;
    }

    // the first time the run starts.
    // starting again after a safe room changes nothing, so the stats keep adding up across them
    private void StartRun()
    {
        if (running)
            return;

        running = true;
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (!running)
            return;

        // the survived clock only goes up while the player is actively being hunted, so it stops in a safe room.
        // with no rooms in the scene there is nothing to pause it
        if (!hasRoomChain || roomChain.RunActive)
            huntedSeconds += Time.deltaTime;

        // the same number the speed HUD shows
        topSpeed = Mathf.Max(topSpeed, player.HorizontalSpeed);

        // sideways distance only, so falling and climbing don't add to it
        Vector3 position = transform.position;
        Vector3 step = position - lastPosition;
        step.y = 0f;

        distance += step.magnitude;
        lastPosition = position;

        if (player.IsWallRunning)
            wallRunSeconds += Time.deltaTime;

        if (player.IsSliding)
            slideSeconds += Time.deltaTime;
    }

    private void OnJumped()
    {
        if (running)
            jumps++;
    }

    private void OnWallJumped()
    {
        if (running)
            wallJumps++;
    }

    private void OnDashed()
    {
        if (running)
            dashes++;
    }

    private void OnBurned(SpeedCapSurface surface)
    {
        if (running)
            timesBurned++;
    }

    // the stats screen's lines, in the order they appear: a label and its value.
    // wall jumps count as jumps too, and they are listed again on their own
    public List<(string label, string value)> BuildLines()
    {
        int rooms = roomChain != null ? roomChain.RoomsPassed : 0;
        int survivedSeconds = Mathf.FloorToInt(huntedSeconds);

        return new List<(string label, string value)>
        {
            ("Rooms cleared", rooms.ToString()),
            ("Top speed", $"{Mathf.RoundToInt(topSpeed)} m/s"),
            ("Jumps", (jumps + wallJumps).ToString()),
            ("Dashes", dashes.ToString()),
            ("Time survived", $"{survivedSeconds / 60:00}:{survivedSeconds % 60:00}"),
            ("Distance run", $"{Mathf.RoundToInt(distance)} m"),
            ("Wall jumps", wallJumps.ToString()),
            ("Time wall running", $"{wallRunSeconds:0.0} s"),
            ("Time sliding", $"{slideSeconds:0.0} s"),
            ("Times burned", timesBurned.ToString()),
        };
    }
}
