using UnityEngine;

// wind that gets louder the faster the player goes.
// silent until windStartSpeed (m/s), full volume at fullVolumeSpeed
// uses the player's whole speed (falling counts), so dropping down the chute is loud
//
// the wind loops with a crossfade, so a clip that doesn't loop cleanly still sounds seamless: two copies of the clip play, and near the end of one the other starts at its beginning while the first fades out (equal power, so there is no dip in loudness).
// the clip has to be longer than a few seconds.
// put this on the Player and give it a wind clip
public class WindAudio : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;
    [SerializeField] private AudioClip windClip;

    [Header("Speed")]
    // below this speed (m/s) there is no wind at all
    [SerializeField] private float windStartSpeed = 30f;

    // at this speed (m/s) the wind is at maxVolume.
    // in between it rises in a straight line
    [SerializeField] private float fullVolumeSpeed = 70f;

    [Header("Sound")]
    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 0.8f;

    // how long (seconds) the end of the clip fades into its start.
    // longer hides a rougher loop.
    // it is cut to half the clip's length
    [Min(0.1f)]
    [SerializeField] private float crossfadeSeconds = 2f;

    // how quickly the volume follows the speed.
    // higher = snappier, 0 = no smoothing
    [Min(0f)]
    [SerializeField] private float volumeSmoothing = 6f;

    private AudioSource current;
    private AudioSource other;
    private float smoothedVolume;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<CustomPlayerController>();
    }

    private void Start()
    {
        if (windClip == null)
        {
            Debug.LogWarning("WindAudio has no wind clip, so there is no wind sound.", this);
            return;
        }

        current = CreateSource();
        other = CreateSource();

        // start somewhere random in the clip, so it isn't the same stretch of wind every run
        float fade = Mathf.Min(crossfadeSeconds, windClip.length * 0.5f);

        current.volume = 0f;
        current.Play();
        current.time = Random.Range(0f, Mathf.Max(0f, windClip.length - fade) * 0.9f);
    }

    private void Update()
    {
        if (windClip == null || player == null || current == null)
            return;

        float target = maxVolume * Mathf.InverseLerp(windStartSpeed, fullVolumeSpeed, player.Velocity.magnitude);

        smoothedVolume = volumeSmoothing <= 0f
            ? target
            : Mathf.Lerp(smoothedVolume, target, 1f - Mathf.Exp(-volumeSmoothing * Time.unscaledDeltaTime));

        // the copy we were fading out has finished: the other one is now the main one
        if (!current.isPlaying)
        {
            if (other.isPlaying)
            {
                AudioSource finished = current;
                current = other;
                other = finished;
            }
            else
            {
                current.Play();
            }
        }

        float length = windClip.length;
        float fade = Mathf.Min(crossfadeSeconds, length * 0.5f);
        float fadeStart = length - fade;

        float currentGain = 1f;
        float otherGain = 0f;

        // in the last part of the clip: bring in the start of the clip on the other source, fading between them
        if (current.time >= fadeStart)
        {
            if (!other.isPlaying)
            {
                other.Play();
                other.time = 0f;
            }

            float progress = Mathf.Clamp01((current.time - fadeStart) / fade);

            currentGain = Mathf.Cos(progress * Mathf.PI * 0.5f);
            otherGain = Mathf.Sin(progress * Mathf.PI * 0.5f);
        }

        current.volume = smoothedVolume * currentGain;
        other.volume = smoothedVolume * otherGain;
    }

    // 2D (not placed in the world), plays once, we do the looping
    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = windClip;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;

        return source;
    }
}
