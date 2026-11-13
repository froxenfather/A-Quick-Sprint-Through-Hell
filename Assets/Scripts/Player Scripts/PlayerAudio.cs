using UnityEngine;

// all of the player's sounds.
// put this on the Player, next to CustomPlayerController

//   - Footsteps: one every strideLength meters while running on the ground (so faster running means faster steps)
//   - One-shots: jump, dash, wall jump, and touching lava or an electric wall
//   - Loops: the wall run scrape and the wall slide hiss, faded in while they are happening and out when they stop

// every slot is optional: an empty one is just silent
public class PlayerAudio : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;

    // optional.
    // without it, the hazard sounds don't play
    [SerializeField] private PlayerBurn burn;

    [Header("Footsteps")]
    // several variations, picked at random (never the same one twice in a row)
    [SerializeField] private AudioClip[] footsteps;

    // meters travelled per step.
    // bigger = slower steps
    [Min(0.5f)]
    [SerializeField] private float strideLength = 3.5f;

    // no steps below this flat speed (m/s), so standing still or shuffling is silent
    [Min(0f)]
    [SerializeField] private float minStepSpeed = 1.5f;

    [Range(0f, 1f)]
    [SerializeField] private float footstepVolume = 0.45f;

    // pitch of the footsteps.
    // 1 = as recorded, lower = deeper and heavier, higher = lighter and sharper
    [Range(0.5f, 2f)]
    [SerializeField] private float footstepPitch = 1f;

    [Header("One-shots")]
    [SerializeField] private AudioClip jump;
    [SerializeField] private AudioClip dash;
    [SerializeField] private AudioClip wallJump;
    [SerializeField] private AudioClip lavaTouch;
    [SerializeField] private AudioClip electricTouch;

    [Range(0f, 1f)]
    [SerializeField] private float effectsVolume = 0.7f;

    // each sound is played slightly higher or lower by up to this much, so repeats don't sound identical
    [Range(0f, 0.5f)]
    [SerializeField] private float pitchVariation = 0.08f;

    [Header("Loops")]
    [SerializeField] private AudioClip wallRunLoop;
    [SerializeField] private AudioClip wallSlideLoop;

    // the ground slide: kept soft and smooth, quieter than the wall sounds
    [SerializeField] private AudioClip slideLoop;

    [Range(0f, 1f)]
    [SerializeField] private float loopVolume = 0.5f;

    // how fast a loop fades in and out (volume per second)
    [Min(0.1f)]
    [SerializeField] private float loopFadeSpeed = 6f;

    private AudioSource effectsSource;
    private AudioSource wallRunSource;
    private AudioSource wallSlideSource;
    private AudioSource slideSource;

    private float distanceSinceStep;
    private int lastFootstep = -1;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<CustomPlayerController>();

        if (burn == null)
            burn = GetComponent<PlayerBurn>();

        effectsSource = CreateSource("Effects", false);
        wallRunSource = CreateSource("Wall Run Loop", true);
        wallSlideSource = CreateSource("Wall Slide Loop", true);
        slideSource = CreateSource("Slide Loop", true);

        wallRunSource.clip = wallRunLoop;
        wallSlideSource.clip = wallSlideLoop;
        slideSource.clip = slideLoop;
    }

    private void OnEnable()
    {
        player.Jumped += OnJumped;
        player.WallJumped += OnWallJumped;
        player.Dashed += OnDashed;

        if (burn != null)
            burn.Burned += OnBurned;
    }

    private void OnDisable()
    {
        player.Jumped -= OnJumped;
        player.WallJumped -= OnWallJumped;
        player.Dashed -= OnDashed;

        if (burn != null)
            burn.Burned -= OnBurned;
    }

    private void Update()
    {
        UpdateFootsteps();

        FadeLoop(wallRunSource, player.IsWallRunning);
        FadeLoop(wallSlideSource, player.IsWallSliding);

        // the slide sound only plays on the ground, an air slide is silent
        FadeLoop(slideSource, player.IsSliding && player.IsGrounded);
    }

    // ============================================================
    // FOOTSTEPS
    // ============================================================

    private void UpdateFootsteps()
    {
        float speed = player.HorizontalSpeed;

        // steps only happen running on the ground.
        // sliding is silent here
        if (!player.IsGrounded || player.IsSliding || speed < minStepSpeed)
        {
            // start the next stride fresh, so the first step after stopping isn't instant
            distanceSinceStep = 0f;
            return;
        }

        distanceSinceStep += speed * Time.deltaTime;

        if (distanceSinceStep < strideLength)
            return;

        distanceSinceStep -= strideLength;
        PlayFootstep();
    }

    private void PlayFootstep()
    {
        if (footsteps == null || footsteps.Length == 0)
            return;

        // pick a different one than last time, when there is a choice
        int index = Random.Range(0, footsteps.Length);

        if (footsteps.Length > 1 && index == lastFootstep)
            index = (index + 1) % footsteps.Length;

        lastFootstep = index;

        PlayOneShot(footsteps[index], footstepVolume, footstepPitch);
    }

    // ============================================================
    // ONE-SHOTS
    // ============================================================

    private void OnJumped()
    {
        PlayOneShot(jump, effectsVolume);
    }

    private void OnWallJumped()
    {
        PlayOneShot(wallJump, effectsVolume);
    }

    private void OnDashed()
    {
        PlayOneShot(dash, effectsVolume);
    }

    private void OnBurned(SpeedCapSurface surface)
    {
        PlayOneShot(surface.Type == HazardType.Lava ? lavaTouch : electricTouch, effectsVolume);
    }

    // pitchScale multiplies the pitch (1 = as recorded), on top of the small random variation
    private void PlayOneShot(AudioClip clip, float volume, float pitchScale = 1f)
    {
        if (clip == null)
            return;

        effectsSource.pitch = pitchScale * (1f + Random.Range(-pitchVariation, pitchVariation));
        effectsSource.PlayOneShot(clip, volume);
    }

    // ============================================================
    // LOOPS
    // ============================================================

    // fades a loop in while it should be playing and out when it shouldn't, and stops it once it is silent
    private void FadeLoop(AudioSource source, bool active)
    {
        if (source.clip == null)
            return;

        float target = active ? loopVolume : 0f;

        if (active && !source.isPlaying)
            source.Play();

        source.volume = Mathf.MoveTowards(source.volume, target, loopFadeSpeed * Time.deltaTime);

        if (!active && source.isPlaying && source.volume <= 0f)
            source.Stop();
    }

    // the sources live on child objects, so every sound can have its own volume and pitch
    // 2D (spatialBlend 0): these are the player's own sounds, not something out in the world
    private AudioSource CreateSource(string sourceName, bool loop)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = loop ? 0f : 1f;

        return source;
    }
}
