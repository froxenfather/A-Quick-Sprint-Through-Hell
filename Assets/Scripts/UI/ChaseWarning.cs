using TMPro;
using UnityEngine;
using UnityEngine.UI;

// bright red letters in the middle of the screen, typed out slowly, every time Conseraph starts to chase.
// it shows when the run starts (the player leaves a safe room), which is the moment he begins to hunt.
// it builds its own overlay canvas, so it only needs adding to an object in the scene (anywhere, for example the RoomChain object)
public class ChaseWarning : MonoBehaviour
{
    private enum Phase
    {
        Idle,
        Typing,
        Holding,
        Fading
    }

    // found at the start if left empty
    [SerializeField] private RoomChain roomChain;

    [Header("Text")]
    [SerializeField] private string message = "here he comes...";

    // the font (Swamp Witch, filled in for you)
    [SerializeField] private TMP_FontAsset font;

    [Min(1f)]
    [SerializeField] private float fontSize = 130f;

    [SerializeField] private Color textColor = new Color(1f, 0.04f, 0.04f);
    [SerializeField] private Color outlineColor = Color.black;

    // 0 is no outline, 1 is the thickest the font allows
    [Range(0f, 1f)]
    [SerializeField] private float outlineThickness = 0.2f;

    // extra space between the letters, so the message reads like h e r e  h e  c o m e s
    [SerializeField] private float letterSpacing = 30f;

    [Header("Timing")]
    // how many letters appear each second (slow on purpose)
    [Min(0.5f)]
    [SerializeField] private float charactersPerSecond = 5f;

    // how long the finished message stays on the screen before it fades (seconds)
    [Min(0f)]
    [SerializeField] private float holdSeconds = 2.5f;

    [Min(0.01f)]
    [SerializeField] private float fadeOutSeconds = 1.5f;

    // the same jitter as the countdown timer: while the message types, it jumps to a random spot up to this far (in pixels) from its place, again and again.
    // when the typing stops it snaps back to its place.
    // 0 turns the jitter off.
    // it is read every frame, so you can drag the slider during Play mode and see the change straight away
    [Header("Jitter")]
    [Range(0f, 40f)]
    [SerializeField] private float jitterPixels = 6f;

    // how many times a second it jumps to a new spot.
    // the timer's clock changes picture 16 times a second while it animates, so this matches it
    [Min(1f)]
    [SerializeField] private float jitterChangesPerSecond = 16f;

    private CanvasGroup group;
    private TextMeshProUGUI label;
    private RectTransform labelRect;

    private Phase phase = Phase.Idle;

    // how many letters the message has, how far the typing has got (a fraction of a letter counts, so the speed is smooth), and how long it has been held
    private int totalCharacters;
    private float typed;
    private float held;

    // time since the last jump to a new spot
    private float jitterTimer;

    private void Awake()
    {
        if (roomChain == null)
            roomChain = FindFirstObjectByType<RoomChain>();

        if (roomChain == null)
        {
            Debug.LogWarning("ChaseWarning can't find a RoomChain in this scene, so there is no chase warning.", this);
            enabled = false;
            return;
        }

        BuildUI();
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
    }

    // the run starts, at the first safe room or a later one: the message types itself out from the first letter
    private void OnRunStarted()
    {
        // the text is laid out whole, so the letters don't shift while it types.
        // none of them are shown yet
        label.text = message;
        label.maxVisibleCharacters = 0;
        label.ForceMeshUpdate();

        totalCharacters = label.textInfo.characterCount;
        typed = 0f;
        held = 0f;

        // already due, so the first jump happens straight away, as the timer's does
        jitterTimer = 1f / jitterChangesPerSecond;

        group.alpha = 1f;
        phase = Phase.Typing;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        switch (phase)
        {
            case Phase.Typing:
                typed += charactersPerSecond * deltaTime;
                label.maxVisibleCharacters = Mathf.Min(totalCharacters, Mathf.FloorToInt(typed));

                // a new random push each time it is due
                jitterTimer += deltaTime;

                if (jitterTimer >= 1f / jitterChangesPerSecond)
                {
                    jitterTimer = 0f;
                    labelRect.anchoredPosition = Random.insideUnitCircle * jitterPixels;
                }

                if (typed >= totalCharacters)
                {
                    // finished typing: snap back to its place, like the timer resting
                    labelRect.anchoredPosition = Vector2.zero;
                    phase = Phase.Holding;
                }

                break;

            case Phase.Holding:
                held += deltaTime;

                if (held >= holdSeconds)
                    phase = Phase.Fading;

                break;

            case Phase.Fading:
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, deltaTime / fadeOutSeconds);

                if (group.alpha <= 0f)
                    phase = Phase.Idle;

                break;
        }
    }

    // a full-screen overlay with one text in the middle, scaled so it looks the same on any screen size
    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("Chase Warning Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // invisible until the run starts, and it never catches clicks
        group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1700f, 300f);
        rect.anchoredPosition = Vector2.zero;

        labelRect = rect;

        label = textObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = textColor;
        label.characterSpacing = letterSpacing;
        label.raycastTarget = false;

        if (font != null)
            label.font = font;

        // the outline is drawn by the font's material, so it is switched on here (fontMaterial is this text's own copy)
        label.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
        label.outlineColor = outlineColor;
        label.outlineWidth = outlineThickness;
    }
}
