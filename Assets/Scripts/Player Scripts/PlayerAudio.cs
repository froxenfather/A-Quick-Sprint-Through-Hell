using UnityEngine;

// all of the player's sounds.
// put this on the Player, next to CustomPlayerController

//   - Footsteps: one every strideLength meters while running on the ground (so faster running means faster steps)
//   - One-shots: jump, dash, wall jump, and touching lava or an electric wall
//   - Loops: the wall run scrape and the wall slide hiss, faded in while they are happening and out when they stop
//   - Wind: gets louder the faster the player goes. it uses the player's whole speed (falling counts), so dropping down the chute is loud.
//     it loops with a crossfade, so a clip that doesn't loop cleanly still sounds seamless (see AudioKit.CrossfadeLoop).
//     the clip has to be longer than a few seconds

// every slot is optional: an empty one is just silent (the lobby has no wind clip, so no wind)
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

    [Header("Wind")]
    // leave empty for no wind
    [SerializeField] private AudioClip windClip;

    // below this speed (m/s) there is no wind at all
    [SerializeField] private float windStartSpeed = 30f;

    // at this speed (m/s) the wind is at Wind Max Volume.
    // in between it rises in a straight line
    [SerializeField] private float windFullVolumeSpeed = 70f;

    [Range(0f, 1f)]
    [SerializeField] private float windMaxVolume = 0.8f;

    // how long (seconds) the end of the clip fades into its start.
    // longer hides a rougher loop.
    // it is cut to half the clip's length
    [Min(0.1f)]
    [SerializeField] private float windCrossfadeSeconds = 8.5f;

    // how quickly the volume follows the speed.
    // higher = snappier, 0 = the volume never changes
    [Min(0f)]
    [SerializeField] private float windVolumeSmoothing = 6f;

    private AudioSource effectsSource;
    private AudioSource wallRunSource;
    private AudioSource wallSlideSource;
    private AudioSource slideSource;

    // the wind, looping with a crossfade (see AudioKit), or null if there is no wind clip
    private AudioKit.CrossfadeLoop wind;
    private float smoothedWindVolume;

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

    private void Start()
    {
        if (windClip == null)
            return;

        wind = new AudioKit.CrossfadeLoop(gameObject, windClip);

        // start somewhere random in the clip, so it isn't the same stretch of wind every run
        float fade = Mathf.Min(windCrossfadeSeconds, windClip.length * 0.5f);

        wind.Play(Random.Range(0f, Mathf.Max(0f, windClip.length - fade) * 0.9f));
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

        UpdateWind();
    }

    // ============================================================
    // WIND
    // ============================================================

    private void UpdateWind()
    {
        if (wind == null)
            return;

        // the whole speed, falling included
        float target = windMaxVolume * Mathf.InverseLerp(windStartSpeed, windFullVolumeSpeed, player.Velocity.magnitude);

        smoothedWindVolume = Mathf.Lerp(smoothedWindVolume, target, windVolumeSmoothing * Time.unscaledDeltaTime);

        wind.Update(smoothedWindVolume, windCrossfadeSeconds);
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

        return AudioKit.Create2DSource(sourceObject, null, loop, loop ? 0f : 1f);
    }
}
