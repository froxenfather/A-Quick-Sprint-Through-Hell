using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 2f;

    private float verticalRotation = 0f;

    // The player's Rigidbody uses interpolation, which rewrites the transform's rotation every frame
    // from the physics copy. Rotating only the transform gets snapped back (rubberbanding),
    // so yaw is applied to the Rigidbody too, keeping both copies in agreement.
    private Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("PlayerLook running");
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        if (rb != null)
        {
            // rb.rotation is the physics truth (already includes any platform spin), so turn from there
            // and write the result to both the Rigidbody and the transform.
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
