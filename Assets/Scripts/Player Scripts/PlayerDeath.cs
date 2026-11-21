using TMPro;
using UnityEngine;
using UnityEngine.Video;

// the ways to die: Conseraph touching the player (normal, enraged, after parrying him, or manic), and being lost to the void (see VoidWatch).
// it freezes the player and sends them back to the lobby.
// a video plays while the lobby loads, then a black screen shows how they died and the stats of the run (see GameStartSequence).
// this works out WHICH death happened (by name, see PickConseraphDeath) and DeathMessages says what the screen shows for it.
// the titles, tips, colors and manic phrases are in DeathMessages.cs, not in the Inspector.
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

    // played full screen, with sound, while the lobby loads, when Conseraph kills the player.
    // leave empty to skip the video
    [SerializeField] private VideoClip gotYouVideo;

    // the same, for dying to the void
    [SerializeField] private VideoClip voidVideo;

    // seconds for the screen to fade from black to clear once the lobby has started
    [Min(0f)]
    [SerializeField] private float fadeInSeconds = 1f;

    [Header("Stats screen")]
    // the font for the stats and for the Conseraph headers (the scary one)
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

    [Header("Header sizes")]
    [Min(1f)]
    [SerializeField] private float titleSize = 110f;

    [Min(1f)]
    [SerializeField] private float tipSize = 42f;

    [SerializeField] private Color tipColor = Color.white;

    // the block font for the void title and tip.
    // filled in with Anton in the editor if it is left empty, so it is not shown in the Inspector
    [SerializeField, HideInInspector] private TMP_FontAsset voidHeaderFont;

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
        Die(PickConseraphDeath());
    }

    // called by VoidWatch when "Let Go" reaches full opacity
    public void DieToVoid()
    {
        Die(DeathMessages.Void);
    }

    // which death it was when Conseraph killed the player: the name of the death, see DeathMessages for what each one says.
    // the rules are checked from the top and the first one that matches wins, so put the most specific ones first.
    // to add a death, add a name and a case in DeathMessages, then add an `if` here that returns that name.
    // what you can check: manic, parries (how many times he was parried this run), enraged, singed
    private string PickConseraphDeath()
    {
        bool manic = conseraph != null && conseraph.Manic;
        int parries = conseraph != null ? conseraph.ParryCount : 0;
        bool enraged = conseraph != null && conseraph.Enraged;
        bool singed = playerBurn != null && playerBurn.Singe > 0;

        if (manic)
            return DeathMessages.Mania;

        if (parries >= 10)
            return DeathMessages.Parried10;

        if (parries >= 5)
            return DeathMessages.Parried5;

        if (parries >= 1)
            return DeathMessages.Parried1;

        if (enraged)
            return DeathMessages.Enraged;

        if (singed)
            return DeathMessages.Singed;

        return DeathMessages.Normal;
    }

    private void Die(string deathName)
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

        // the Mania death: every stat is a phrase instead of a number
        if (deathName == DeathMessages.Mania)
            OverwriteWithPhrases(page.Lines);

        FillHeader(page, deathName);

        // freeze the player where they are, they are about to be replaced by the lobby's player
        player.enabled = false;

        if (look != null)
            look.enabled = false;

        VideoClip video = deathName == DeathMessages.Void ? voidVideo : gotYouVideo;

        // this object is in the factory, which gets unloaded, so the rest runs on an object that survives the scene change
        GameStartSequence.Begin(lobbySceneName, video, fadeInSeconds, page);
    }

    // each stat keeps its label and gets a random phrase for its value
    private void OverwriteWithPhrases(System.Collections.Generic.List<(string label, string value)> lines)
    {
        if (DeathMessages.ManiaPhrases.Length == 0)
            return;

        for (int i = 0; i < lines.Count; i++)
            lines[i] = (lines[i].label, DeathMessages.ManiaPhrases[Random.Range(0, DeathMessages.ManiaPhrases.Length)]);
    }

    // the title and tip at the very top of the stats screen: how the player died
    private void FillHeader(GameStartSequence.StatsPage page, string deathName)
    {
        DeathMessage message = DeathMessages.Get(deathName);

        page.HeaderTitleSize = titleSize;
        page.HeaderTipSize = tipSize;
        page.HeaderTipColor = tipColor;

        page.HeaderTitle = message.Title;
        page.HeaderTitleColor = message.Color;
        page.HeaderTip = message.Tip;
        page.HeaderFont = message.UseVoidFont ? voidHeaderFont : statsFont;
    }
}
