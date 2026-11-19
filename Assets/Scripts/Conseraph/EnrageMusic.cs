using UnityEngine;

// the song that plays while Conseraph is enraged.
// it fades in when the timer reaches 00:00, which is the moment he becomes enraged, and fades out when the enrage ends.
// the song starts from its very beginning, and the start has no crossfade of its own, only the fade-in below.
// it loops with a crossfade, like the wind, so a clip that doesn't loop cleanly still sounds seamless: two copies of the clip play, and near the end of one the other starts at its beginning while the first fades out (equal power, so there is no dip in loudness).
// the clip has to be longer than a few seconds.
// put this on the Conseraph object and drag the song into Enrage Music
[RequireComponent(typeof(Conseraph))]
public class EnrageMusic : MonoBehaviour
{
    // the enrage mode music.
    // leave empty for no music
    [SerializeField] private AudioClip enrageMusic;

    [Header("Sound")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f;

    // how long (seconds) the end of the song fades into its start each time it loops.
    // longer hides a rougher loop.
    // it is cut to half the clip's length
    [Min(0.1f)]
    [SerializeField] private float loopCrossfadeSeconds = 2f;

    [Header("Fade")]
    // how long the song takes to fade in from silence once he is enraged (seconds).
    // 0 starts it at full volume straight away
    [Min(0f)]
    [SerializeField] private float fadeInSeconds = 1f;

    // how long the song takes to fade out when the enrage ends (seconds).
    // 0 cuts it off
    [Min(0f)]
    [SerializeField] private float fadeOutSeconds = 1f;

    private Conseraph conseraph;

    // the two copies of the clip.
    // current is the one that is further into the song
    private AudioSource current;
    private AudioSource other;

    private bool hasMusic;

    // true from the moment the song starts until it has faded out and stopped
    private bool playing;

    // the fade-in and fade-out, 0 is silence and 1 is full volume
    private float fade;

    private void Awake()
    {
        conseraph = GetComponent<Conseraph>();

        hasMusic = enrageMusic != null;

        if (!hasMusic)
            return;

        current = CreateSource();
        other = CreateSource();
    }

    private void OnDisable()
    {
        // the sources are separate components and would keep playing at their last volume with nothing to loop or fade them
        if (playing)
            StopSong();
    }

    // everything here runs on real time, so the freeze frame of a parry doesn't stall the fades (audio itself isn't stopped by it)
    private void Update()
    {
        if (!hasMusic)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        bool startedNow = false;

        if (conseraph.Enraged)
        {
            if (!playing)
            {
                StartSong();
                startedNow = true;
            }

            fade = fadeInSeconds > 0f ? Mathf.MoveTowards(fade, 1f, deltaTime / fadeInSeconds) : 1f;
        }
        else if (playing)
        {
            fade = fadeOutSeconds > 0f ? Mathf.MoveTowards(fade, 0f, deltaTime / fadeOutSeconds) : 0f;

            if (fade <= 0f)
            {
                StopSong();
                return;
            }
        }

        if (!playing)
            return;

        float currentGain = 1f;
        float otherGain = 0f;

        // the song was only just started this frame, so there is nothing to loop yet
        if (!startedNow)
            LoopGains(out currentGain, out otherGain);

        current.volume = volume * fade * currentGain;
        other.volume = volume * fade * otherGain;
    }

    // from the very beginning of the clip, at full gain.
    // only the fade-in above makes it quieter
    private void StartSong()
    {
        playing = true;
        fade = 0f;

        current.Play();
    }

    private void StopSong()
    {
        playing = false;

        current.Stop();
        other.Stop();

        current.volume = 0f;
        other.volume = 0f;
    }

    // the loop: how loud each copy is right now, with a crossfade at the end of the song.
    // the same method as WindAudio
    private void LoopGains(out float currentGain, out float otherGain)
    {
        currentGain = 1f;
        otherGain = 0f;

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

        float length = enrageMusic.length;
        float crossfade = Mathf.Min(loopCrossfadeSeconds, length * 0.5f);
        float crossfadeStart = length - crossfade;

        // in the last part of the song: bring in the start of the song on the other copy, fading between them
        if (current.time >= crossfadeStart)
        {
            if (!other.isPlaying)
            {
                other.Play();
                other.time = 0f;
            }

            float progress = Mathf.Clamp01((current.time - crossfadeStart) / crossfade);

            currentGain = Mathf.Cos(progress * Mathf.PI * 0.5f);
            otherGain = Mathf.Sin(progress * Mathf.PI * 0.5f);
        }
    }

    // 2D (not placed in the world), plays once, we do the looping
    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = enrageMusic;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;

        return source;
    }
}
