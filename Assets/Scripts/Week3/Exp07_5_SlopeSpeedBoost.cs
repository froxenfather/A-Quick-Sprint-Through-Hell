using UnityEngine;
using UnityEngine.InputSystem;

// learning Objective: convert the velocity that would be lost hitting a ramp (the part driving INTO the surface) into extra flat speed down the ramp
// steeper ramp -> bigger boost. head-on hit -> bigger boost than a glancing one
// requires: a ramp Collider below (no Rigidbody on this object). drop it straight down, then try again from an angle (tilt startVelocity)
public class Exp07_5_SlopeSpeedBoost : MonoBehaviour
{
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private Vector3 startVelocity = Vector3.zero;
    [SerializeField] private float boostMultiplier = 1f; // "k" in the math
    [SerializeField] private AnimationCurve slopeBoostCurve = AnimationCurve.Linear(0f, 0f, 90f, 1f); // x = slope degrees, y = multiplier
    [SerializeField] private float skinWidth = 0.05f;
    [SerializeField] private float groundCheckDistance = 0.2f;

    private Vector3 velocity;
    private Vector3 startPosition;
    private bool grounded;

    private void Awake()
    {
        startPosition = transform.position;
        velocity = startVelocity;
    }

    private void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            transform.position = startPosition;
            velocity = startVelocity;
            grounded = false;
        }

        velocity += Vector3.down * gravity * Time.deltaTime;
        Vector3 nextPosition = transform.position + velocity * Time.deltaTime;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance + skinWidth))
        {
            float impactSpeed = -Vector3.Dot(velocity, hit.normal);

            if (impactSpeed > 0f)
            {
                Vector3 tangential = velocity - (Vector3.Dot(velocity, hit.normal) * hit.normal);
                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                float steepnessFactor = slopeBoostCurve.Evaluate(slopeAngle);

                Vector3 flatDownhill = Vector3.ProjectOnPlane(Vector3.down, hit.normal);
                flatDownhill.y = 0f;
                flatDownhill = flatDownhill.normalized;

                float bonus = impactSpeed * steepnessFactor * boostMultiplier;

                Vector3 tangentialFlat = tangential;
                tangentialFlat.y = 0f;
                velocity = tangentialFlat + flatDownhill * bonus;

                Debug.Log($"impact: {impactSpeed:F2}  slope: {slopeAngle:F1} deg  bonus: {bonus:F2}");
            }

            nextPosition.y = hit.point.y + skinWidth;
            grounded = true;
        }
        else
        {
            grounded = false;
        }

        transform.position = nextPosition;

        Debug.DrawRay(transform.position, velocity, grounded ? Color.cyan : Color.red);
    }
}
