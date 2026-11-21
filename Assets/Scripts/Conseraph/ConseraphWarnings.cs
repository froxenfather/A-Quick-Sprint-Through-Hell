using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// the two big messages Conseraph puts in the middle of the screen.
// put this on the Conseraph object. each message builds its own overlay, so there is nothing else to set up.
//
//   Chase  "here it comes..." types itself out, letter by letter, every time he starts to chase (the run starts: the player leaves a safe room).
//          the words shake while they type, then stay a moment and fade away.
//   Manic  "DIE!!!!!" fades in from nothing once he goes manic (Mania Mode, see Conseraph), and fades away when the enrage ends.
//          the words shake for as long as they show, and an optional sound gets louder as they get stronger.
//
// the shake is the same as the countdown timer's: the words jump to a random spot a few pixels from their place, a few times a second
[RequireComponent(typeof(Conseraph))]
public class ConseraphWarnings : MonoBehaviour
{
    private enum ChasePhase
    {
        Idle,
        Typing,
        Holding,
        Fading
    }

    // found at the start if left empty.
    // the chase warning needs it to know when the run starts
    [SerializeField] private RoomChain roomChain;

    // ============================================================
    // CHASE: "here it comes..."
    // ============================================================

    [Header("Chase: text")]
    [SerializeField] private string chaseMessage = "here it comes...";

    // the font (filled in for you when this component is added in the Inspector)
    [SerializeField] private TMP_FontAsset chaseFont;

    [Min(1f)]
    [SerializeField] private float chaseFontSize = 70f;

    [SerializeField] private Color chaseTextColor = new Color(1f, 0.04f, 0.04f);
    [SerializeField] private Color chaseOutlineColor = Color.black;

    // 0 is no outline, 1 is the thickest the font allows
    [Range(0f, 1f)]
    [SerializeField] private float chaseOutlineThickness = 0.2f;

    // extra space between the letters, so the message reads like h e r e  i t  c o m e s
    [SerializeField] private float chaseLetterSpacing = 40f;

    [Header("Chase: timing")]
    // how many letters appear each second (slow on purpose)
    [Min(0.5f)]
    [SerializeField] private float chaseCharactersPerSecond = 3f;

    // how long the finished message stays on the screen before it fades (seconds)
    [Min(0f)]
    [SerializeField] private float chaseHoldSeconds = 2.5f;

    [Min(0.01f)]
    [SerializeField] private float chaseFadeOutSeconds = 1.5f;

    // while the message types, it jumps to a random spot up to this far (in pixels) from its place, again and again.
    // when the typing stops it snaps back to its place.
    // 0 turns the jitter off.
    // it is read every frame, so you can drag the slider during Play mode and see the change straight away
    [Header("Chase: jitter")]
    [Range(0f, 40f)]
    [SerializeField] private float chaseJitterPixels = 6f;

    // how many times a second it jumps to a new spot.
    // the timer's clock changes picture 16 times a second while it animates, so this matches it
    [Min(1f)]
    [SerializeField] private float chaseJitterChangesPerSecond = 16f;

    // ============================================================
    // MANIC: "DIE!!!!!"
    // ============================================================

    [Header("Manic: text")]
    // shown in capital letters, whatever is typed here
    [FormerlySerializedAs("message")]
    [SerializeField] private string manicMessage = "DIE!!!!!";

    // the font (Midnight Spree, the scary one, filled in for you when this component is added in the Inspector)
    [FormerlySerializedAs("font")]
    [SerializeField] private TMP_FontAsset manicFont;

    [FormerlySerializedAs("fontSize")]
    [Min(1f)]
    [SerializeField] private float manicFontSize = 300f;

    [FormerlySerializedAs("textColor")]
    [SerializeField] private Color manicTextColor = new Color(0.9f, 0.05f, 0.05f);

    [FormerlySerializedAs("outlineColor")]
    [SerializeField] private Color manicOutlineColor = Color.black;

    // 0 is no outline, 1 is the thickest the font allows
    [FormerlySerializedAs("outlineThickness")]
    [Range(0f, 1f)]
    [SerializeField] private float manicOutlineThickness = 0.2f;

    [Header("Manic: timing")]
    // how long "DIE!!!!!" takes to go from nothing to full once he goes manic (seconds)
    [FormerlySerializedAs("fadeInSeconds")]
    [Min(0.01f)]
    [SerializeField] private float manicFadeInSeconds = 1f;

    // how long it takes to fade away when he stops being manic (seconds)
    [FormerlySerializedAs("clearSeconds")]
    [Min(0.01f)]
    [SerializeField] private float manicClearSeconds = 0.4f;

    [Header("Manic: sound")]
    // plays while "DIE!!!!!" is on the screen and gets louder as it gets stronger.
    // leave empty for no sound
    [FormerlySerializedAs("warningSound")]
    [SerializeField] private AudioClip manicSound;

    [FormerlySerializedAs("maxVolume")]
    [Range(0f, 1f)]
    [SerializeField] private float manicMaxVolume = 1f;

    // ticked: the sound loops for as long as the words are showing
    [FormerlySerializedAs("loopSound")]
    [SerializeField] private bool manicLoopSound = true;

    // the words jump to a random spot up to this far (in pixels) from their place, again and again, for as long as they are showing.
    // 0 turns the jitter off.
    // it is read every frame, so you can drag the slider during Play mode and see the change straight away
    [Header("Manic: jitter")]
    [FormerlySerializedAs("jitterPixels")]
    [Range(0f, 40f)]
    [SerializeField] private float manicJitterPixels = 10f;

    // how many times a second they jump to a new spot
    [FormerlySerializedAs("jitterChangesPerSecond")]
    [Min(1f)]
    [SerializeField] private float manicJitterChangesPerSecond = 16f;

    private Conseraph conseraph;

    // the chase warning
    private CanvasGroup chaseGroup;
    private TextMeshProUGUI chaseLabel;
    private ChasePhase chasePhase = ChasePhase.Idle;
    private readonly UiKit.Jitter chaseJitter = new UiKit.Jitter();

    // how many letters the chase message has, how far the typing has got (a fraction of a letter counts, so the speed is smooth), and how long it has been held
    private int chaseTotalCharacters;
    private float chaseTyped;
    private float chaseHeld;

    // the manic warning
    private CanvasGroup manicGroup;
    private RectTransform manicRect;
    private AudioKit.LevelSound manicSoundPlayer;
    private readonly UiKit.Jitter manicJitter = new UiKit.Jitter();

    private void Awake()
    {
        conseraph = GetComponent<Conseraph>();

        if (roomChain == null)
            roomChain = FindAnyObjectByType<RoomChain>();

        // without a RoomChain there is no run to start, so only the chase warning is lost
        if (roomChain == null)
            Debug.LogWarning("ConseraphWarnings can't find a RoomChain in this scene, so there is no chase warning.", this);

        BuildChaseUI();
        BuildManicUI();

        manicSoundPlayer = new AudioKit.LevelSound(gameObject, manicSound, manicLoopSound);
    }

    private void OnEnable()
    {
        if (roomChain != null)
            roomChain.RunStarted += OnRunStarted;
    }

    private void OnDisable()
    {
        if (roomChain != null)
            roomChain.RunStarted -= OnRunStarted;

        // nothing should be left on the screen or playing
        chasePhase = ChasePhase.Idle;

        if (chaseGroup != null)
            chaseGroup.alpha = 0f;

        if (manicGroup != null)
            manicGroup.alpha = 0f;

        if (manicSoundPlayer != null)
            manicSoundPlayer.Stop();
    }

    // real time, so the freeze frame of a parry doesn't stall the fades
    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        UpdateChase(deltaTime);
        UpdateManic(deltaTime);
    }

    // ============================================================
    // CHASE
    // ============================================================

    // the run starts, at the first safe room or a later one: the message types itself out from the first letter
    private void OnRunStarted()
    {
        // the text is laid out whole, so the letters don't shift while it types.
        // none of them are shown yet
        chaseLabel.text = chaseMessage;
        chaseLabel.maxVisibleCharacters = 0;
        chaseLabel.ForceMeshUpdate();

        chaseTotalCharacters = chaseLabel.textInfo.characterCount;
        chaseTyped = 0f;
        chaseHeld = 0f;

        chaseGroup.alpha = 1f;
        chasePhase = ChasePhase.Typing;
    }

    private void UpdateChase(float deltaTime)
    {
        switch (chasePhase)
        {
            case ChasePhase.Typing:
                chaseTyped += chaseCharactersPerSecond * deltaTime;
                chaseLabel.maxVisibleCharacters = Mathf.Min(chaseTotalCharacters, Mathf.FloorToInt(chaseTyped));

                // finished typing: it stops shaking and rests in its place, like the timer
                if (chaseTyped >= chaseTotalCharacters)
                    chasePhase = ChasePhase.Holding;

                break;

            case ChasePhase.Holding:
                chaseHeld += deltaTime;

                if (chaseHeld >= chaseHoldSeconds)
                    chasePhase = ChasePhase.Fading;

                break;

            case ChasePhase.Fading:
                chaseGroup.alpha = Mathf.MoveTowards(chaseGroup.alpha, 0f, deltaTime / chaseFadeOutSeconds);

                if (chaseGroup.alpha <= 0f)
                    chasePhase = ChasePhase.Idle;

                break;
        }

        // it shakes while it types, and sits still the rest of the time
        chaseJitter.Update(chaseLabel.rectTransform, chasePhase == ChasePhase.Typing, chaseJitterPixels, chaseJitterChangesPerSecond, deltaTime);
    }

    // a full-screen overlay with one text in the middle, invisible until the run starts
    private void BuildChaseUI()
    {
        chaseGroup = UiKit.BuildOverlay(transform, "Chase Warning Canvas", 60);

        chaseLabel = UiKit.CreateText(chaseGroup.transform, "Message", new Vector2(0.5f, 0.5f), new Vector2(1700f, 300f), chaseFont, chaseFontSize, chaseTextColor, chaseOutlineColor, chaseOutlineThickness);
        chaseLabel.characterSpacing = chaseLetterSpacing;
    }

    // ============================================================
    // MANIC
    // ============================================================

    private void UpdateManic(float deltaTime)
    {
        float alpha = conseraph.Manic
            ? Mathf.MoveTowards(manicGroup.alpha, 1f, deltaTime / manicFadeInSeconds)
            : Mathf.MoveTowards(manicGroup.alpha, 0f, deltaTime / manicClearSeconds);

        manicGroup.alpha = alpha;

        // the sound gets louder with the words, and the words shake for as long as they show
        manicSoundPlayer.Set(alpha, manicMaxVolume);
        manicJitter.Update(manicRect, alpha > 0f, manicJitterPixels, manicJitterChangesPerSecond, deltaTime);
    }

    // a full-screen overlay with one text in the middle, invisible until he is manic
    private void BuildManicUI()
    {
        manicGroup = UiKit.BuildOverlay(transform, "Manic Warning Canvas", 70);

        TextMeshProUGUI label = UiKit.CreateText(manicGroup.transform, "Message", new Vector2(0.5f, 0.5f), new Vector2(1800f, 400f), manicFont, manicFontSize, manicTextColor, manicOutlineColor, manicOutlineThickness);
        label.text = manicMessage.ToUpperInvariant();

        manicRect = label.rectTransform;
    }
}
