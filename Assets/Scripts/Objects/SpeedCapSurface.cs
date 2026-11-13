using UnityEngine;

// what kind of hazard a SpeedCapSurface is, so the sounds and effects can tell lava from an electric wall
public enum HazardType
{
    Lava,
    Electric
}

// a hazard surface, like lava or an electric wall.
// put this on the same object as its Collider
// while the player touches it, their flat speed is capped at Max Speed.
// if they are airborne when they touch it, they can't build speed again until they land on something else
// the first touch also bounces them away along the surface's normal (up off a floor, out away from a wall)

// the look of the surface (the Lava or Electric Wall material) is separate: this is what makes it dangerous
// CustomPlayerController does all the work, this just holds the numbers
public class SpeedCapSurface : MonoBehaviour
{
    // picks the touch sound.
    // burning, the sprint speed penalty and the screen overlay are the same for both
    [SerializeField] private HazardType type = HazardType.Lava;

    // flat speed (m/s) the player is held to while touching this surface
    [Min(0f)]
    [SerializeField] private float maxSpeed = 5f;

    // speed (m/s) the player is thrown along the surface's normal on first touch.
    // 0 = no bounce
    [Min(0f)]
    [SerializeField] private float bounceSpeed = 10f;

    public HazardType Type => type;
    public float MaxSpeed => maxSpeed;
    public float BounceSpeed => bounceSpeed;
}
