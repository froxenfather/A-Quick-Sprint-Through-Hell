using UnityEngine;

// everything that makes the player's body look right.
// put this on the Player, next to CustomPlayerController.
//
//   Animator   feeds the stick figure's Animator from the player controller.
//              the Animator (Player_Animator.controller in Assets/Animation) decides which animation plays from these parameters:
//                Speed (float)  flat speed, blends idle / walk / run / run fast
//                Grounded, Sliding, Crouching, WallRunning (bool)
//                Jump (trigger) fired by a jump, a wall jump and a dash, so all three play the jump animation
//                PlaybackSpeed (float) how fast the running cycle plays, so quicker running looks quicker
//   Slide      lowers the camera and tilts the body model while sliding (and lowers the camera for a crouch).
//              rough/placeholder: it just needs to read as "the character is sliding" from third person
//   Head       lets the first-person camera sit inside the stick figure's head without seeing the inside of it:
//              the head (and everything on it) is shrunk to nothing each frame, so the arms, torso and legs stay visible
//              when you look down, and the animations still play on the rest of the body
public class PlayerVisuals : MonoBehaviour
{
    // found on this object if left empty
    [SerializeField] private CustomPlayerController player;

    // ============================================================
    // ANIMATOR
    // ============================================================

    [Header("Animator")]
    // found on the stick figure (a child of the Player) if left empty
    [SerializeField] private Animator animator;

    // the controller to use.
    // left empty, whatever the Animator already has is kept
    [SerializeField] private RuntimeAnimatorController controller;

    // at or below this flat speed (m/s) the running cycle plays at Slowest Playback, at or above Top Speed it plays at Fastest Playback
    [SerializeField] private float topSpeed = 30f;

    [Min(0.1f)]
    [SerializeField] private float slowestPlayback = 0.9f;

    [Min(0.1f)]
    [SerializeField] private float fastestPlayback = 1.4f;

    // how smoothly the Speed parameter follows the real speed (seconds).
    // higher = lazier blends
    [Min(0f)]
    [SerializeField] private float speedSmoothing = 0.08f;

    // ============================================================
    // SLIDE
    // ============================================================

    [Header("Slide: camera")]
    // the object the camera sits on, found by Camera Pivot Name under the Player if left empty
    [SerializeField] private Transform cameraPivot;

    [SerializeField] private string cameraPivotName = "CameraPivot";

    [SerializeField] private float slideCameraHeight = 0.7f;
    [SerializeField] private float crouchCameraHeight = 1f;
    [SerializeField] private float slideCameraSpeed = 8f;

    [Header("Slide: body tilt")]
    // the body model that tilts: the stick figure itself, which is the object the Animator is on, if left empty
    [SerializeField] private Transform model;

    [SerializeField] private float slideTiltAngle = 75f;
    [SerializeField] private float slideModelPivotHeight = 1.1f;

    // degrees per second
    [SerializeField] private float tiltSpeed = 540f;

    // ============================================================
    // HEAD
    // ============================================================

    [Header("Head (first person)")]
    // found by Head Bone Name under the Player if left empty
    [SerializeField] private Transform headBone;

    [SerializeField] private string headBoneName = "Neck";

    // turn this off to see the head again (for a third-person test)
    [SerializeField] private bool hideHead = true;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int SlidingHash = Animator.StringToHash("Sliding");
    private static readonly int CrouchingHash = Animator.StringToHash("Crouching");
    private static readonly int WallRunningHash = Animator.StringToHash("WallRunning");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int PlaybackSpeedHash = Animator.StringToHash("PlaybackSpeed");

    // false if there is no Animator, so the slide and the head still work without it
    private bool hasAnimator;

    // the camera pivot's normal height, read once so we know what to ease back to
    private float standCameraHeight;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<CustomPlayerController>();

        SetUpAnimator();

        // the model that tilts is the stick figure itself
        if (model == null && hasAnimator)
            model = animator.transform;

        if (cameraPivot == null)
            cameraPivot = FindChild(cameraPivotName);

        if (cameraPivot == null)
            Debug.LogWarning($"PlayerVisuals could not find a child called '{cameraPivotName}' under the Player, so the camera won't lower for slides and crouches.", this);

        if (headBone == null)
            headBone = FindChild(headBoneName);

        if (headBone == null)
            Debug.LogWarning($"PlayerVisuals could not find a bone called '{headBoneName}' under the Player, so the head is not hidden.", this);
    }

    private void Start()
    {
        if (cameraPivot != null)
            standCameraHeight = cameraPivot.localPosition.y;
    }

    private void OnEnable()
    {
        if (!hasAnimator)
            return;

        player.Jumped += PlayJump;
        player.WallJumped += PlayJump;
        player.Dashed += PlayJump;
    }

    private void OnDisable()
    {
        if (!hasAnimator)
            return;

        player.Jumped -= PlayJump;
        player.WallJumped -= PlayJump;
        player.Dashed -= PlayJump;
    }

    // camera/model work runs per frame, not in FixedUpdate
    private void Update()
    {
        if (hasAnimator)
            UpdateAnimator();

        UpdateCameraHeight();
        UpdateModelTilt();
    }

    // after the Animator has posed the body, so our scale wins this frame
    private void LateUpdate()
    {
        if (headBone == null)
            return;

        headBone.localScale = hideHead ? Vector3.zero : Vector3.one;
    }

    // ============================================================
    // ANIMATOR
    // ============================================================

    private void SetUpAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        hasAnimator = animator != null;

        if (!hasAnimator)
        {
            Debug.LogError("PlayerVisuals could not find an Animator on the Player or its children, so there is no animation.", this);
            return;
        }

        if (controller != null && animator.runtimeAnimatorController != controller)
            animator.runtimeAnimatorController = controller;

        // root motion would let the animations move the character, fighting the player controller, which owns all movement
        animator.applyRootMotion = false;
    }

    private void UpdateAnimator()
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

    // ============================================================
    // SLIDE
    // ============================================================

    private void UpdateCameraHeight()
    {
        if (cameraPivot == null)
            return;

        float targetHeight = standCameraHeight;

        if (player.IsSliding)
            targetHeight = slideCameraHeight;
        else if (player.IsCrouching)
            targetHeight = crouchCameraHeight;

        Vector3 localPosition = cameraPivot.localPosition;
        localPosition.y = Mathf.MoveTowards(localPosition.y, targetHeight, slideCameraSpeed * Time.deltaTime);
        cameraPivot.localPosition = localPosition;
    }

    private void UpdateModelTilt()
    {
        if (model == null)
            return;

        float targetAngle = player.IsSliding ? slideTiltAngle : 0f;

        // localEulerAngles wraps to 0-360, so convert to a -180..180 range before easing toward a target that might be "behind" 0 (otherwise MoveTowardsAngle can spin the long way around)
        float currentAngle = model.localEulerAngles.x;
        if (currentAngle > 180f)
            currentAngle -= 360f;

        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, tiltSpeed * Time.deltaTime);

        // lean back around the HIPS, not the feet - that's why we don't just rotate the model in place
        // flip the sign on newAngle below if this leans the wrong way for your model's forward axis
        Vector3 pivot = new Vector3(0f, slideModelPivotHeight, 0f);
        Quaternion tilt = Quaternion.Euler(-newAngle, 0f, 0f);

        model.localRotation = tilt;
        model.localPosition = pivot - tilt * pivot;
    }

    // ============================================================
    // FINDING THINGS UNDER THE PLAYER
    // ============================================================

    // the first object under the Player (even a switched-off one) with this name, or null
    private Transform FindChild(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
