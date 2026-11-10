using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Ground Movement")]
    [SerializeField] private float walkSpeed = 5.5f; //soft walk speed cap
    [SerializeField] private float sprintSpeed = 7.5f; //soft sprint speed cap
    [SerializeField] private float groundAccel = 55f;  // ramp toward target when under it
    [SerializeField] private float groundOverspeedDecel = 12f; // gentle skid toward target when over it (arrived hot)
    [SerializeField] private float groundFriction = 120f; // no input -> decelerate VERY quickly, near-instant stop
    [SerializeField] private float landingGrace = 0.15f; // after touching ground, suppress friction this long so momentum carries (bhop window)

    [Header("Movement")]
    [SerializeField] private float jumpVelocity = 5f;
    [SerializeField] private float extraGravity = 9.81f;
    [SerializeField] private float airControl = 2f;
    [SerializeField] private float maxAirSpeed = 8f;

    private Rigidbody rb; 
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool sprinting;
    private bool grounded;
    private bool wasGrounded;
    private float landTime = -999f;

    private Rigidbody supportBody;
    private Vector3 supportVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        Move();
        ApplyExtraGravity();
        Jump();
    }

    private void ReadInput()
    {
        moveInput = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            moveInput.y += 1f;

        if (Keyboard.current.sKey.isPressed)
            moveInput.y -= 1f;

        if (Keyboard.current.dKey.isPressed)
            moveInput.x += 1f;

        if (Keyboard.current.aKey.isPressed)
            moveInput.x -= 1f;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpPressed = true;

        sprinting = Keyboard.current.leftShiftKey.isPressed;

        moveInput = moveInput.normalized;
    }

    private void Move()
    {
        Vector3 moveDirection =
            transform.forward * moveInput.y +
            transform.right * moveInput.x;

        if (grounded)
        {
            if (!wasGrounded)
                landTime = Time.time;

            bool inLandingGrace = Time.time - landTime < landingGrace;

            // work in support-relative space so moving platforms still carry us
            Vector3 relVel = rb.linearVelocity - supportVelocity;
            Vector3 horiz = new Vector3(relVel.x, 0f, relVel.z);

            float targetSpeed = sprinting ? sprintSpeed : walkSpeed;
            bool hasInput = moveInput.sqrMagnitude > 0.0001f;

            if (hasInput)
            {
                Vector3 targetVel = moveDirection * targetSpeed;

                // gentler rate when we're already over target (came in hot from a boost / air landing) than when accelerating up to it
                float rate = horiz.magnitude > targetSpeed
                    ? groundOverspeedDecel
                    : groundAccel;

                horiz = Vector3.MoveTowards(
                    horiz,
                    targetVel,
                    rate * Time.fixedDeltaTime
                );
            }
            else if (!inLandingGrace)
            {
                // high groundFriction -> decelerates very quickly to a stop
                // (near-instant, but still frame-rate independent). suppressed during the landing grace window so jump momentum carries
                horiz = Vector3.MoveTowards(
                    horiz,
                    Vector3.zero,
                    groundFriction * Time.fixedDeltaTime
                );
            }

            Vector3 finalVelocity =
                new Vector3(horiz.x, relVel.y, horiz.z) +
                supportVelocity;

            rb.linearVelocity = new Vector3(
                finalVelocity.x,
                rb.linearVelocity.y,
                finalVelocity.z
            );
        }
        else
        {
            rb.AddForce(
                moveDirection * airControl,
                ForceMode.Acceleration
            );

            LimitAirSpeed();
        }
        wasGrounded = grounded;
    }
    
    private void LimitAirSpeed()
    {
        Vector3 horizontalVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        if (horizontalVelocity.magnitude > maxAirSpeed)
        {
            horizontalVelocity =
                horizontalVelocity.normalized * maxAirSpeed;

            rb.linearVelocity = new Vector3(
                horizontalVelocity.x,
                rb.linearVelocity.y,
                horizontalVelocity.z
            );
        }
    }

    private void Jump()
    {
        if (jumpPressed && grounded)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                jumpVelocity,
                rb.linearVelocity.z
            );

            grounded = false;
            supportBody = null;
            supportVelocity = Vector3.zero;
        }

        jumpPressed = false;
    }

    private void ApplyExtraGravity()
    {
        if (!grounded)
        {
            rb.AddForce(
                Vector3.down * extraGravity,
                ForceMode.Acceleration
            );
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        grounded = false;
        supportBody = null;
        supportVelocity = Vector3.zero;

        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                grounded = true;

                supportBody = collision.rigidbody;

                if (supportBody != null)
                {
                    supportVelocity = supportBody.GetPointVelocity(contact.point);
                }

                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.rigidbody == supportBody)
        {
            supportBody = null;
            supportVelocity = Vector3.zero;
            grounded = false;
        }
    }
}