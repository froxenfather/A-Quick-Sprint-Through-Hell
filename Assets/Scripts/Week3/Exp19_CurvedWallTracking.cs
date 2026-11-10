using UnityEngine;
using UnityEngine.InputSystem;

// learning Objective: follow a curved wall (cylinder) using only the hit normal
// a non-Rigidbody object travels along the wall's tangent, re-casting toward the wall every frame. toggle the stick force to see why straight-line motion drifts off a curve, and toggle speed preservation to see reprojection slow you down
// requires: a vertical Cylinder with a Collider. start this object near it, at torso height, a bit away from the surface. no Rigidbody on this object
public class Exp19_CurvedWallTracking : MonoBehaviour
{
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float castRadius = 0.3f;
    [SerializeField] private float castDistance = 2f;
    [SerializeField] private float wallOffset = 0.5f; // how far from the surface to hover
    [SerializeField] private float stickStrength = 1f; // scales the inward pull
    [SerializeField] private bool useStickForce = true;
    [SerializeField] private bool preserveSpeed = true;

    private Vector3 velocity;
    private Vector3 lastNormal;
    private bool attached;
    private Vector3 startPosition;

    private void Awake()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            transform.position = startPosition;
            velocity = Vector3.zero;
            attached = false;
        }

        // attach: press Space to cast toward the wall. set your start facing so transform.forward points at the cylinder. detach: press E
        if (!attached && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (Physics.SphereCast(transform.position, castRadius, transform.forward, out RaycastHit attachHit, castDistance))
            {
                lastNormal = attachHit.normal;
                attached = true;

                Vector3 tangent = Vector3.Cross(Vector3.up, lastNormal);
                if (Vector3.Dot(tangent, transform.forward) < 0f)
                    tangent = -tangent;

                velocity = tangent.normalized * runSpeed;
            }
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            attached = false;
            velocity = Vector3.zero;
        }

        if (!attached) return;

        if (!Physics.SphereCast(transform.position, castRadius, -lastNormal, out RaycastHit hit, castDistance))
        {
            attached = false;
            return;
        }

        lastNormal = hit.normal;

        if (Vector3.Angle(lastNormal, Vector3.up) < 45f)
        {
            attached = false;
            return;
        }

        float speed = velocity.magnitude;
        Vector3 projected = Vector3.ProjectOnPlane(velocity, lastNormal);
        Vector3 direction = projected.normalized;
        velocity = preserveSpeed ? direction * speed : projected;

        if (useStickForce)
        {
            velocity += -lastNormal * (velocity.sqrMagnitude * stickStrength * 0.01f) * Time.deltaTime;
        }

        transform.position += velocity * Time.deltaTime;

        float distanceError = hit.distance - wallOffset;
        transform.position += -lastNormal * distanceError;

        Debug.DrawRay(transform.position, lastNormal * 2f, Color.green);   // wall normal
        Debug.DrawRay(transform.position, velocity, Color.red);            // velocity
        Debug.DrawRay(transform.position, Vector3.Cross(Vector3.up, lastNormal) * 2f, Color.cyan); // tangent
    }
}
