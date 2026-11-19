using TMPro;
using UnityEngine;
using UnityEngine.Video;

// the two ways to die: Conseraph touching the player, and being lost to the void (see VoidWatch).
// it freezes the player and sends them back to the lobby.
// a video plays while the lobby loads, then a black screen shows how they died and the stats of the run (see GameStartSequence).
// put this on the Player in TheFactory
[RequireComponent(typeof(RunStats))]
public class PlayerDeath : MonoBehaviour
{
    private enum DeathCause
    {
        Conseraph,
        Void
    }

    // found at the start if left empty
    [SerializeField] private Conseraph conseraph;

    [Header("Going back")]
    // the scene to go back to.
    // it has to be in Build Settings (menu: A Quick Sprint Through Hell > Set Up Build Scenes)
    [SerializeField] private string lobbySceneName = "CustomPlay";

    // played full screen, with sound, while the lobby loads, when Conseraph kills the player.
    // leave empty to skip the video
    [SerializeField] private VideoClip gotYouVideo;

    // the same, for dying to the void
    [SerializeField] private VideoClip voidVideo;

    // seconds for the screen to fade from black to clear once the lobby has started
    [Min(0f)]
    [SerializeField] private float fadeInSeconds = 1f;

    [Header("Stats screen")]
    // the font for the stats and for the red Conseraph header (the scary one)
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

    [Header("How they died: Conseraph")]
    [SerializeField] private string conseraphTitle = "You succumbed to Conseraph";
    [SerializeField] private Color conseraphTitleColor = Color.red;

    // the tips under the title.
    // the enraged one wins over the singed one, and the normal one is for everything else
    [TextArea(1, 3)]
    [SerializeField] private string normalTip = "Try taking your time to ensure you do not need to backtrack or slow down!";

    [TextArea(1, 3)]
    [SerializeField] private string enragedTip = "Try checking the saferoom for clues on how to counter him when all seems lost!";

    [TextArea(1, 3)]
    [SerializeField] private string singedTip = "When singed, slide bouncing and dashing is the best way to keep speed!";

    [Header("How they died: the void")]
    [SerializeField] private string voidTitle = "The Void consumes you.";
    [SerializeField] private Color voidTitleColor = new Color(0.6f, 0.2f, 1f);

    [TextArea(1, 3)]
    [SerializeField] private string voidTip = "Make sure to stay inside the Eternal Factory and avoid holes!";

    // the block font for the void title and tip.
    // filled in with Anton in the editor if it is left empty
    [SerializeField] private TMP_FontAsset voidHeaderFont;

    [Header("Header sizes")]
    [Min(1f)]
    [SerializeField] private float titleSize = 110f;

    [Min(1f)]
    [SerializeField] private float tipSize = 42f;

    [SerializeField] private Color tipColor = Color.white;

    // the GUID of Anton SDF, one of the fonts that comes with TextMesh Pro
    private const string AntonFontGuid = "8a89fa14b10d46a99122fd4f73fca9a2";

    private CustomPlayerController player;
    private PlayerLook look;
    private PlayerBurn playerBurn;
    private RunStats runStats;
    private bool dead;

    // true from the moment the player starts dying, so nothing else tries to kill them as well
    public bool IsDead => dead;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // found by the font's GUID, so renaming or moving it is fine
        if (voidHeaderFont == null)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(AntonFontGuid);

            if (!string.IsNullOrEmpty(path))
                voidHeaderFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }
    }
#endif

    private void Awake()
    {
        player = GetComponent<CustomPlayerController>();
        look = GetComponent<PlayerLook>();
        playerBurn = GetComponent<PlayerBurn>();
        runStats = GetComponent<RunStats>();

        if (conseraph == null)
            conseraph = FindFirstObjectByType<Conseraph>();

        if (conseraph == null)
            Debug.LogWarning("PlayerDeath can't find Conseraph in this scene, so he can't kill the player.", this);
    }

    private void OnEnable()
    {
        if (conseraph != null)
            conseraph.TouchedPlayer += OnConseraphTouched;
    }

    private void OnDisable()
    {
        if (conseraph != null)
            conseraph.TouchedPlayer -= OnConseraphTouched;
    }

    private void OnConseraphTouched()
    {
        Die(DeathCause.Conseraph);
    }

    // called by VoidWatch when "Let Go" reaches full opacity
    public void DieToVoid()
    {
        Die(DeathCause.Void);
    }

    private void Die(DeathCause cause)
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

        FillHeader(page, cause);

        // freeze the player where they are, they are about to be replaced by the lobby's player
        player.enabled = false;

        if (look != null)
            look.enabled = false;

        VideoClip video = cause == DeathCause.Void ? voidVideo : gotYouVideo;

        // this object is in the factory, which gets unloaded, so the rest runs on an object that survives the scene change
        GameStartSequence.Begin(lobbySceneName, video, fadeInSeconds, page);
    }

    // the title and tip at the very top of the stats screen: how the player died
    private void FillHeader(GameStartSequence.StatsPage page, DeathCause cause)
    {
        page.HeaderTitleSize = titleSize;
        page.HeaderTipSize = tipSize;
        page.HeaderTipColor = tipColor;

        if (cause == DeathCause.Void)
        {
            page.HeaderTitle = voidTitle;
            page.HeaderTitleColor = voidTitleColor;
            page.HeaderTip = voidTip;
            page.HeaderFont = voidHeaderFont;
            return;
        }

        page.HeaderTitle = conseraphTitle;
        page.HeaderTitleColor = conseraphTitleColor;
        page.HeaderFont = statsFont;

        // enraged first, then singed (any singe level at all), then the normal tip
        if (conseraph != null && conseraph.Enraged)
            page.HeaderTip = enragedTip;
        else if (playerBurn != null && playerBurn.Singe > 0)
            page.HeaderTip = singedTip;
        else
            page.HeaderTip = normalTip;
    }
}
