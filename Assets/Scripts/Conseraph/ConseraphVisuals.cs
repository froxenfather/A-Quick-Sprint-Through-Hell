using UnityEngine;

// you feel a terrible chill crawling down your back...
public class ConseraphVisuals : MonoBehaviour
{
    // found at the start if left empty
    [SerializeField] private CustomPlayerController player;

    [Header("Parts")]
    [SerializeField] private Transform ring;
    [SerializeField] private Transform face;
    [SerializeField] private Transform mouth;
    [Header("Ring")]
    [Min(0.1f)]
    [SerializeField] private float ringFlipsPerSecond = 3f;
    [Min(0f)]
    [SerializeField] private float ringMaxTilt = 10f;
    [Header("Mouth")]
    [Min(0.1f)]
    [SerializeField] private float mouthChangesPerSecond = 2f;
    [Min(0f)]
    [SerializeField] private float mouthMaxTilt = 10f;
    [Min(0.01f)]
    [SerializeField] private float mouthMinSize = 0.9f;
    [Min(0.01f)]
    [SerializeField] private float mouthMaxSize = 1.1f;
    [Header("Face")]
    [Min(0.1f)]
    [SerializeField] private float faceLoopSeconds = 1f;

    // how far the face leans from upright.
    // positive leans counter-clockwise as the player sees it, negative leans the other way
    [SerializeField] private float faceTilt = 10f;

    // how much bigger the face gets in its second and third pose (0.1 is 10% bigger)
    [Min(0f)]
    [SerializeField] private float faceSizeGrowth = 0.1f;

    // one part of the picture.
    // it remembers the scale it has in the scene, so the pose is applied on top of it
    private class Part
    {
        private readonly Transform transform;
        private readonly Vector3 baseScale;

        public Part(Transform transform)
        {
            this.transform = transform;

            if (transform != null)
                baseScale = transform.localScale;
        }

        // flip is 1 or -1: -1 mirrors the part over the vertical axis.
        // the tilt is negated with the mirror, so a mirrored part leans the opposite way, like a real reflection
        public void Set(float flip, float tilt, float size)
        {
            if (transform == null)
                return;

            transform.localScale = new Vector3(baseScale.x * flip * size, baseScale.y * size, baseScale.z);
            transform.localRotation = Quaternion.Euler(0f, 0f, flip * tilt);
        }
    }

    private Part ringPart;
    private Part facePart;
    private Part mouthPart;

    private CapsuleCollider playerCapsule;
    private float startTime;

    // the step each part last changed on, and which way the ring and mouth are mirrored now.
    // they start on -1 so the first change at time zero lands on the unmirrored pose
    private int ringStep = -1;
    private int mouthStep = -1;
    private float ringFlip = -1f;
    private float mouthFlip = -1f;

    private void Awake()
    {
        ringPart = new Part(ring);
        facePart = new Part(face);
        mouthPart = new Part(mouth);

        if (player == null)
            player = FindAnyObjectByType<CustomPlayerController>();

        if (player != null)
            playerCapsule = player.GetComponent<CapsuleCollider>();
        else
            Debug.LogWarning("Conseraph can't find a CustomPlayerController, so he will not turn to face the player.", this);
    }

    private void OnEnable()
    {
        startTime = Time.time;
        ringStep = -1;
        mouthStep = -1;
        ringFlip = -1f;
        mouthFlip = -1f;
    }

    private void Update()
    {
        float time = Time.time - startTime;

        UpdateRing(time);
        UpdateMouth(time);
        UpdateFace(time);
    }

    private void UpdateRing(float time)
    {
        // a new step number means it is time for the next flip
        int step = Mathf.FloorToInt(time * ringFlipsPerSecond);

        if (step == ringStep)
            return;

        ringStep = step;
        ringFlip = -ringFlip;
        ringPart.Set(ringFlip, Random.Range(0f, ringMaxTilt), 1f);
    }

    private void UpdateMouth(float time)
    {
        int step = Mathf.FloorToInt(time * mouthChangesPerSecond);

        if (step == mouthStep)
            return;

        mouthStep = step;
        mouthFlip = -mouthFlip;
        mouthPart.Set(mouthFlip, Random.Range(0f, mouthMaxTilt), Random.Range(mouthMinSize, mouthMaxSize));
    }

    // four equal poses in a loop.
    // pose 0 is the start.
    // pose 1 is bigger.
    // pose 2 is flipped and still bigger.
    // pose 3 is smaller again and still flipped.
    // the flip back to the start happens when the loop begins again
    private void UpdateFace(float time)
    {
        int pose = Mathf.FloorToInt(time / faceLoopSeconds * 4f) % 4;

        bool flipped = pose >= 2;
        bool bigger = pose == 1 || pose == 2;

        facePart.Set(flipped ? -1f : 1f, faceTilt, bigger ? 1f + faceSizeGrowth : 1f);
    }

    // after everything else has moved this frame, so he is never a frame behind the player
    private void LateUpdate()
    {
        if (player == null)
            return;

        // the middle of the player's capsule, not their feet or their eyes.
        // it uses the transform so it follows the player smoothly, and the capsule's center so it follows a slide
        Vector3 target = playerCapsule != null ? player.transform.TransformPoint(playerCapsule.center) : player.transform.position;

        // a sprite is seen from its -Z side, so its forward points away from the player.
        // keeping world up as up means he never rolls, so a tilt is always measured from true vertical
        Vector3 away = transform.position - target;

        if (away.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }
}
