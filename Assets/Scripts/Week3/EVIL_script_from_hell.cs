using UnityEngine;
using UnityEngine.InputSystem;

// STRETCH EXPERIMENT (do only after the core experiments are done)
// learning Objective: on a ramp, gravity's pull ALONG the surface is g * sin(slope)
// steeper ramp -> faster acceleration down; sliding up a ramp slows you down
// requires: a ramp Collider below (no Rigidbody on this object).
// place it on Ramp_15 / Ramp_30 / Ramp_45 and compare.
// this is NOT the final slide system

// this fucker took me an entire night.
// ramps werent even equal length
// gravity got added twice.
// seam ate velocity.y on steep ramps.
// and i was measuring distance from the wrong point the whole time.
// pool balls fixed me
// those who know
public class EVIL_script_from_hell : MonoBehaviour
{
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float skinWidth = 0.05f;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private float flatFriction = 8f; // = gravity for the cos+sin symmetry, dont ask

    private Vector3 velocity;
    private Vector3 startPosition;
    private float halfHeight;
    private Vector3? previousNormal;
    private bool wasOnSlope;
    private bool hasMoved;
    private bool hasLoggedFinal;
    private Vector3? rampExitPosition; // NOT startPosition, learn from my pain

    private void Awake()
    {
        startPosition = transform.position; // remember home

        Renderer rend = GetComponentInChildren<Renderer>(); // grab the mesh
        halfHeight = rend != null ? rend.bounds.extents.y : 0f; // so we know where feet are
    }

    private void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame) // r = start over
        {
            transform.position = startPosition;
            velocity = Vector3.zero; // kill all momentum
            previousNormal = null; // forget what we were standing on
            wasOnSlope = false;
            hasMoved = false; // reset the "did it ever move" flag
            hasLoggedFinal = false; // allow the stop-log to fire again
            rampExitPosition = null;
        }

        // ONLY place gravity gets added.
        // do not add a second sin(theta) kick below, the projection already handles that.
        // doubled it once, never again
        velocity += Vector3.down * gravity * Time.deltaTime;

        Vector3 feetPosition = transform.position + Vector3.down * halfHeight; // actual bottom, not the pivot
        float fallDistanceThisFrame = Mathf.Max(-velocity.y * Time.deltaTime, 0f); // how far we're about to drop
        float castDistance = Mathf.Max(groundCheckDistance, fallDistanceThisFrame) + skinWidth; // look at least that far

        if (!Physics.Raycast(feetPosition, Vector3.down, out RaycastHit hit, castDistance)) // nothing under us
        {
            transform.position += velocity * Time.deltaTime; // just keep falling
            Debug.DrawRay(transform.position, velocity, Color.red); // red = airborne
            previousNormal = null;
            return;
        }

        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up); // 0 = flat, 90 = wall

        if (slopeAngle <= 0.5f) // basically flat
        {
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, flatFriction * Time.deltaTime); // bleed speed off
        }

        // only preserve speed on a REAL normal change, not every frame
        bool isTransition = previousNormal == null || Vector3.Angle(previousNormal.Value, hit.normal) > 1f;

        if (isTransition)
        {
            float speedBeforeProjection = velocity.magnitude; // save it before it gets mangled

            if (wasOnSlope && slopeAngle <= 0.5f) // specifically leaving a ramp
            {
                // this is the ramp->flat handoff.
                // distance gets measured from HERE, not from wherever this thing started
                Debug.Log($"<color=orange>[{name}] EXIT RAMP - speed: {speedBeforeProjection:F2}</color>");
                rampExitPosition = transform.position; // the ONE true reference point
            }

            Vector3 projected = Vector3.ProjectOnPlane(velocity, hit.normal); // flatten onto new surface
            velocity = projected.normalized * speedBeforeProjection; // but keep the speed we had
        }
        else
        {
            velocity = Vector3.ProjectOnPlane(velocity, hit.normal); // same surface, just stay on it
        }

        previousNormal = hit.normal; // for next frame's comparison
        wasOnSlope = slopeAngle > 0.5f;

        Vector3 nextPosition = transform.position + velocity * Time.deltaTime; // where we'd end up
        nextPosition.y = hit.point.y + halfHeight + skinWidth; // snap feet to the surface
        transform.position = nextPosition;

        if (velocity.magnitude > 0.05f) // still moving
        {
            hasMoved = true;
            Debug.Log($"[{name}] slope: {slopeAngle:F1} deg  speed: {velocity.magnitude:F2}");
        }
        else if (hasMoved && !hasLoggedFinal) // just came to rest, log it ONCE
        {
            hasLoggedFinal = true;

            // y zeroed out, measured from ramp exit.
            // this took 40 messages to get right
            Vector3 reference = rampExitPosition ?? startPosition; // fallback if it never left a ramp
            Vector3 referenceFlat = new Vector3(reference.x, 0f, reference.z); // kill height
            Vector3 endFlat = new Vector3(transform.position.x, 0f, transform.position.z); // kill height here too
            float distanceFromRampExit = Vector3.Distance(referenceFlat, endFlat); // ground distance only

            Debug.Log($"<color=yellow>[{name}] STOPPED - distance from ramp exit: {distanceFromRampExit:F2}</color>");
        }

        Debug.DrawRay(transform.position, velocity, Color.cyan); // cyan = grounded
    }
}

// lets be PERCFECTLY CLEAR HERE this is FINE games should NOT be realistic they should FEEL GOOD
// GOODNIGHT

// SLOPE SLIDE FORMULA, for the record
// 1. ramp accel: a = g sin(theta)
// 2. exit speed after distance L from rest: v^2 = 2aL
// 3. sub in accel: v^2 = 2gL sin(theta)
// 4. flat stopping distance at constant decel k: d = v^2 / (2k)
// 5. sub in v^2: d = (2gL sin(theta)) / (2k)
// 6. simplify: d = (gL / k) sin(theta)
// 7. if k = g: d = L sin(theta)
// 8. equal-length ramps:
//    30 deg -> d = 0.500L
//    45 deg -> d = 0.707L
//    60 deg -> d = 0.866L
// 9. so: 60 > 45 > 30.
//    checks out