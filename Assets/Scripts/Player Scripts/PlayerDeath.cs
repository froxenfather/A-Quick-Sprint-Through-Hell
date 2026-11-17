using TMPro;
using UnityEngine;
using UnityEngine.Video;

// what happens when Conseraph touches the player, which is the only way to die.
// it freezes the player and sends them back to the lobby.
// the "got you" video plays while the lobby loads, then a black screen shows the stats of the run (see GameStartSequence).
// put this on the Player in TheFactory
[RequireComponent(typeof(RunStats))]
public class PlayerDeath : MonoBehaviour
{
    // found at the start if left empty
    [SerializeField] private Conseraph conseraph;

    [Header("Going back")]
    // the scene to go back to.
    // it has to be in Build Settings (menu: A Quick Sprint Through Hell > Set Up Build Scenes)
    [SerializeField] private string lobbySceneName = "CustomPlay";

    // played full screen, with sound, while the lobby loads.
    // leave empty to skip the video
    [SerializeField] private VideoClip gotYouVideo;

    // seconds for the screen to fade from black to clear once the lobby has started
    [Min(0f)]
    [SerializeField] private float fadeInSeconds = 1f;

    [Header("Stats screen")]
    // the font for the stats (the scary one)
    [SerializeField] private TMP_FontAsset statsFont;

    // played as each stat lands.
    // leave empty for no sound
    [SerializeField] private AudioClip thudSound;

    [Range(0f, 1f)]
    [SerializeField] private float thudVolume = 1f;

    // the pause before each stat appears, so the screen builds up one thud at a time
    [Min(0.05f)]
    [SerializeField] private float lineDelaySeconds = 0.8f;

    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private Color valueColor = Color.red;

    private CustomPlayerController player;
    private PlayerLook look;
    private RunStats runStats;
    private bool dead;

    private void Awake()
    {
        player = GetComponent<CustomPlayerController>();
        look = GetComponent<PlayerLook>();
        runStats = GetComponent<RunStats>();

        if (conseraph == null)
            conseraph = FindFirstObjectByType<Conseraph>();

        if (conseraph == null)
            Debug.LogWarning("PlayerDeath can't find Conseraph in this scene, so nothing can kill the player.", this);
    }

    private void OnEnable()
    {
        if (conseraph != null)
            conseraph.TouchedPlayer += Die;
    }

    private void OnDisable()
    {
        if (conseraph != null)
            conseraph.TouchedPlayer -= Die;
    }

    private void Die()
    {
        if (dead)
            return;

        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogError($"PlayerDeath can't load scene '{lobbySceneName}'. Add it to Build Settings (menu: A Quick Sprint Through Hell > Set Up Build Scenes) and check the name.", this);
            return;
        }

        dead = true;

        GameStartSequence.StatsPage page = new GameStartSequence.StatsPage
        {
            Lines = runStats.BuildLines(),
            Font = statsFont,
            Thud = thudSound,
            ThudVolume = thudVolume,
            LineDelay = lineDelaySeconds,
            LabelColor = labelColor,
            ValueColor = valueColor
        };

        // freeze the player where they are, they are about to be replaced by the lobby's player
        player.enabled = false;

        if (look != null)
            look.enabled = false;

        // this object is in the factory, which gets unloaded, so the rest runs on an object that survives the scene change
        GameStartSequence.Begin(lobbySceneName, gotYouVideo, fadeInSeconds, page);
    }
}
