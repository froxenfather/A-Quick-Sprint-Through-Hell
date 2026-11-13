using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 2f;

    private float verticalRotation = 0f;
    private Rigidbody rb;

    // start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("PlayerLook running");
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rb = GetComponent<Rigidbody>();
    }

    // turns the player to face along forward (flat) and tilts the camera to pitchDegrees (positive = looking down, 89 = straight down)
    public void SetLook(Vector3 forward, float pitchDegrees)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        Vector3 flat = new Vector3(forward.x, 0f, forward.z);

        if (flat.sqrMagnitude > 0.0001f)
        {
            Quaternion yaw = Quaternion.LookRotation(flat.normalized, Vector3.up);

            // both copies, like the mouse look does, or the Rigidbody snaps the transform back
            if (rb != null)
                rb.rotation = yaw;

            transform.rotation = yaw;
        }

        verticalRotation = Mathf.Clamp(pitchDegrees, -89f, 89f);
        cameraPivot.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    // update is called once per frame
    void Update()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        if (rb != null)
        {
            // rb.rotation is the physics truth (already includes any platform spin), so turn from there and write the result to both the Rigidbody and the transform
            Quaternion newRotation = rb.rotation * Quaternion.Euler(0f, mouseX, 0f);
            rb.rotation = newRotation;
            transform.rotation = newRotation;
        }
        else
        {
            transform.Rotate(Vector3.up * mouseX);
        }

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -89f, 89f);

        cameraPivot.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }
}
