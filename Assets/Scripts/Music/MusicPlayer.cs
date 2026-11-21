using UnityEngine;

// all of the game's background music, in one place.
// put this on the RoomChainMAINGAME object, next to RoomChain.
//
// the rules that keep this simple:
//   - the music player decides nothing about the game. RoomChain tells it when the run music starts and stops, and the enrage song just follows Conseraph's enraged state.
//   - nothing in the game depends on what is playing: Conseraph goes enraged when the countdown timer says so, and goes manic on his own clock (see Conseraph)
//
//   Run music    starts when RoomChain calls PlayRunMusic (the run starts: the player leaves a safe room) and plays once, it does not loop.
//                starting it again restarts it from the beginning. it fades in.
//                RoomChain calls StopRunMusic when the player goes into a safe room: it fades out and stops.
//                debug mode (on RoomChain) keeps it quiet.
//   Enrage song  plays while Conseraph is enraged: it fades in when he becomes enraged and fades out when the enrage ends.
//                it starts from its very beginning and loops with a crossfade (see AudioKit.CrossfadeLoop), so a clip that doesn't loop cleanly still sounds seamless.
//                with Mania Mode (on Conseraph) the song plays through once, and then only its final seconds loop.
//                the clip has to be longer than a few seconds.
//
// every clip is optional: leave it empty for no music.
//
// to add another song: give it a clip and settings in a new section below, add a Play/Stop method for whoever starts it (or follow a game state, like the enrage song does),
// and add one line to Update. a song that just starts and stops can use AudioKit.FadedSong, like the run music does
public class MusicPlayer : MonoBehaviour
{
    // found at the start if left empty.
    // only needed for the enrage song: with no Conseraph in the scene, it never plays
    [SerializeField] private Conseraph conseraph;

    // ============================================================
    // RUN MUSIC
    // ============================================================

    [Header("Run music")]
    // the song that starts each time the run starts (when the player leaves a safe room) and plays once (it does not loop).
    // starting it again restarts it from the beginning.
    // leave empty for no music.
    // (filled in for you when this component is added in the Inspector)
    [SerializeField] private AudioClip runMusic;

    // the volume the song fades up to.
    // it can be changed while playing
    [Range(0f, 1f)]
    [SerializeField] private float runVolume = 0.578f;

    // seconds the song takes to fade in from silence up to Run Volume.
    // 0 starts it at full volume straight away
    [Range(0f, 30f)]
    [SerializeField] private float runFadeInSeconds = 10f;

    // seconds the song takes to fade out to silence when the player goes into a safe room, and then it stops.
    // 0 stops it at once
    [Range(0f, 30f)]
    [SerializeField] private float runFadeOutSeconds = 2f;

    // ============================================================
    // ENRAGE SONG
    // ============================================================

    [Header("Enrage song")]
    // the enrage mode music.
    // leave empty for no music.
    // (filled in for you when this component is added in the Inspector)
    [SerializeField] private AudioClip enrageSong;

    [Range(0f, 1f)]
    [SerializeField] private float enrageVolume = 0.7f;

    // how long (seconds) the end of the song fades into its start each time it loops.
    // longer hides a rougher loop.
    // it is cut to half the part of the song that loops
    [Min(0.1f)]
    [SerializeField] private float enrageLoopCrossfadeSeconds = 0.1f;

    // Mania Mode (on Conseraph) only.
    // the song plays through once, and when it gets to its final this many seconds, those seconds loop forever instead of the whole song.
    // for a 2:50 song, 10 means the loop is 2:40 to 2:50.
    // when Conseraph goes manic is set separately, by Mania Start Seconds on him
    [Min(1f)]
    [SerializeField] private float enrageFinalLoopSeconds = 10f;

    // how long the song takes to fade in from silence once he is enraged (seconds).
    // 0 starts it at full volume straight away
    [Min(0f)]
    [SerializeField] private float enrageFadeInSeconds = 1f;

    // how long the song takes to fade out when the enrage ends (seconds).
    // 0 cuts it off
    [Min(0f)]
    [SerializeField] private float enrageFadeOutSeconds = 0f;

    // the run music: plays once with a fade-in and a fade-out (see AudioKit)
    private AudioKit.FadedSong runSong;

    // the enrage song: looping with a crossfade (see AudioKit), or null if there is no song
    private AudioKit.CrossfadeLoop enrageLoop;

    // true from the moment the enrage song starts until it has faded out and stopped
    private bool enrageSongPlaying;

    // the enrage song's fade-in and fade-out, 0 is silence and 1 is full volume
    private float enrageFade;

    private void Awake()
    {
        if (conseraph == null)
            conseraph = FindAnyObjectByType<Conseraph>();

        runSong = new AudioKit.FadedSong(gameObject);

        if (enrageSong == null)
            return;

        if (conseraph == null)
            Debug.LogWarning("MusicPlayer can't find Conseraph in this scene, so the enrage song never plays.", this);

        enrageLoop = new AudioKit.CrossfadeLoop(gameObject, enrageSong);
    }

    private void OnDisable()
    {
        // the sources are separate components and would keep playing at their last volume with nothing to loop or fade them
        if (enrageSongPlaying)
            StopEnrageSong();
    }

    // everything here runs on real time, so the freeze frame of a parry doesn't stall the fades (audio itself isn't stopped by it)
    private void Update()
    {
        runSong.Update(runVolume, runFadeInSeconds, runFadeOutSeconds);

        UpdateEnrageSong();
    }

    // ============================================================
    // RUN MUSIC
    // ============================================================

    // RoomChain calls this each time the run starts
    public void PlayRunMusic()
    {
        runSong.Play(runMusic, runVolume, runFadeInSeconds);
    }

    // RoomChain calls this when the player goes into a safe room
    public void StopRunMusic()
    {
        runSong.Stop(runFadeOutSeconds);
    }

    // ============================================================
    // ENRAGE SONG
    // ============================================================

    private void UpdateEnrageSong()
    {
        if (enrageLoop == null || conseraph == null)
            return;

        float deltaTime = Time.unscaledDeltaTime;

        if (conseraph.Enraged)
        {
            if (!enrageSongPlaying)
                StartEnrageSong();

            enrageFade = enrageFadeInSeconds > 0f ? Mathf.MoveTowards(enrageFade, 1f, deltaTime / enrageFadeInSeconds) : 1f;
        }
        else if (enrageSongPlaying)
        {
            enrageFade = enrageFadeOutSeconds > 0f ? Mathf.MoveTowards(enrageFade, 0f, deltaTime / enrageFadeOutSeconds) : 0f;

            if (enrageFade <= 0f)
            {
                StopEnrageSong();
                return;
            }
        }

        if (!enrageSongPlaying)
            return;

        // with Mania Mode the song loops back to the start of its final seconds, not to its very beginning
        float loopStart = conseraph.ManiaMode ? Mathf.Max(0f, enrageSong.length - enrageFinalLoopSeconds) : 0f;

        enrageLoop.Update(enrageVolume * enrageFade, enrageLoopCrossfadeSeconds, loopStart);
    }

    // from the very beginning of the clip, at full gain.
    // only the fade-in makes it quieter
    private void StartEnrageSong()
    {
        enrageSongPlaying = true;
        enrageFade = 0f;

        enrageLoop.Play();
    }

    private void StopEnrageSong()
    {
        enrageSongPlaying = false;

        enrageLoop.Stop();
    }
}
