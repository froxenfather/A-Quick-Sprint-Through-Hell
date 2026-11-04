using UnityEngine;

// A hazard surface, like lava or an electric wall. Put this on the same object as its Collider.
// While the player touches it, their flat speed is capped at Max Speed. If they are airborne when they touch it, they can't build speed again until they land on something else.
// The first touch also bounces them away along the surface's normal (up off a floor, out away from a wall).
//
// The look of the surface (the Lava or Electric Wall material) is separate: this is what makes it dangerous.
// CustomPlayerController does all the work, this just holds the numbers.
public class SpeedCapSurface : MonoBehaviour
{
    // Flat speed (m/s) the player is held to while touching this surface.
    [Min(0f)]
    [SerializeField] private float maxSpeed = 5f;

    // Speed (m/s) the player is thrown along the surface's normal on first touch. 0 = no bounce.
    [Min(0f)]
    [SerializeField] private float bounceSpeed = 10f;

    public float MaxSpeed => maxSpeed;
    public float BounceSpeed => bounceSpeed;
}
