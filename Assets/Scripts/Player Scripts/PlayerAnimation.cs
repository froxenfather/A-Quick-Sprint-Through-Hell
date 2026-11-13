using UnityEngine;

// feeds the stick figure's Animator from the player controller.
// put this on the Player, next to CustomPlayerController

// the Animator (made by A Quick Sprint Through Hell > Set Up Player Feedback) decides which animation plays from these parameters:
//   speed (float)  flat speed, blends idle / walk / run / run fast
//   grounded, Sliding, Crouching, WallRunning (bool)
//   jump (trigger) fired by a jump, a wall jump and a dash, so all three play the jump animation
//   PlaybackSpeed (float) how fast the running cycle plays, so quicker running looks quicker
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;

    // found on the stick figure (a child of the Player) if left empty
    [SerializeField] private Animator animator;

    // the controller to use.
    // set by the setup menu.
    // left empty, whatever the Animator already has is kept
    [SerializeField] private RuntimeAnimatorController controller;

    [Header("Running cycle speed")]
    // at or below this flat speed (m/s) the cycle plays at Slowest Playback, at or above Top Speed it plays at Fastest Playback
    [SerializeField] private float topSpeed = 30f;

    [Min(0.1f)]
    [SerializeField] private float slowestPlayback = 0.9f;

    [Min(0.1f)]
    [SerializeField] private float fastestPlayback = 1.4f;

    // how smoothly the Speed parameter follows the real speed (seconds).
    // higher = lazier blends
    [Min(0f)]
    [SerializeField] private float speedSmoothing = 0.08f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int SlidingHash = Animator.StringToHash("Sliding");
    private static readonly int CrouchingHash = Animator.StringToHash("Crouching");
    private static readonly int WallRunningHash = Animator.StringToHash("WallRunning");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int PlaybackSpeedHash = Animator.StringToHash("PlaybackSpeed");

    private void Awake()
    {
        if (player == null)
            player = GetComponent<CustomPlayerController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError("PlayerAnimation could not find an Animator on the Player or its children.", this);
            enabled = false;
            return;
        }

        if (controller != null && animator.runtimeAnimatorController != controller)
            animator.runtimeAnimatorController = controller;

        // root motion would let the animations move the character, fighting the player controller, which owns all movement
        animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        player.Jumped += PlayJump;
        player.WallJumped += PlayJump;
        player.Dashed += PlayJump;
    }

    private void OnDisable()
    {
        player.Jumped -= PlayJump;
        player.WallJumped -= PlayJump;
        player.Dashed -= PlayJump;
    }

    private void Update()
    {
        float speed = player.HorizontalSpeed;

        animator.SetFloat(SpeedHash, speed, speedSmoothing, Time.deltaTime);
        animator.SetBool(GroundedHash, player.IsGrounded);
        animator.SetBool(SlidingHash, player.IsSliding);
        animator.SetBool(CrouchingHash, player.IsCrouching);
        animator.SetBool(WallRunningHash, player.IsWallRunning);

        animator.SetFloat(PlaybackSpeedHash, Mathf.Lerp(slowestPlayback, fastestPlayback, Mathf.InverseLerp(0f, topSpeed, speed)));
    }

    // restarts the jump animation, even if it is already playing (a wall jump right after a jump)
    private void PlayJump()
    {
        animator.SetTrigger(JumpHash);
    }
}
