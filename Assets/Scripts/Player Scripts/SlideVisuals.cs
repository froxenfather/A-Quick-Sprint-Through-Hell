using UnityEngine;

// lowers the camera and tilts the body model while sliding. rough/placeholder - doesn't need to look good, just needs to read as "the character is sliding" from third person
// put this on the Player object, next to CustomPlayerController
public class SlideVisuals : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform model;

    [Header("Camera")]
    [SerializeField] private float slideCameraHeight = 0.7f;
    [SerializeField] private float crouchCameraHeight = 1f;
    [SerializeField] private float slideCameraSpeed = 8f;

    [Header("Body tilt")]
    [SerializeField] private float slideTiltAngle = 75f;
    [SerializeField] private float slideModelPivotHeight = 1.1f;
    [SerializeField] private float tiltSpeed = 540f; // degrees per second

    // CameraPivot's normal height, read once so we know what to ease back to
    private float standCameraHeight;

    private void Start()
    {
        standCameraHeight = cameraPivot.localPosition.y;
    }

    // camera/model work runs per frame, not in FixedUpdate
    private void Update()
    {
        UpdateCameraHeight();
        UpdateModelTilt();
    }

    private void UpdateCameraHeight()
    {
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
}
