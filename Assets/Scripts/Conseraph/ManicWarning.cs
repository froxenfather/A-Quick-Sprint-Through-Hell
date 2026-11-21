using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the big "DIE!!!!!" in the middle of the screen while Conseraph is manic (Mania Mode: the enrage song has reached its final loop).
// it works like VoidWatch's "Let Go": it fades in from nothing to full over the middle of the screen, the words constantly jitter like the timer's, and an optional sound fades up with it.
// it fades away when he stops being manic (the enrage ends).
// Conseraph adds this itself if it is missing, but add it to the Conseraph object in the Inspector to get the scary font filled in
[RequireComponent(typeof(Conseraph))]
public class ManicWarning : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private string message = "DIE!!!!!";

    // the font (Midnight Spree, the scary one, filled in for you when this is added in the Inspector)
    [SerializeField] private TMP_FontAsset font;

    [Min(1f)]
    [SerializeField] private float fontSize = 300f;

    [SerializeField] private Color textColor = new Color(0.9f, 0.05f, 0.05f);
    [SerializeField] private Color outlineColor = Color.black;

    // 0 is no outline, 1 is the thickest the font allows
    [Range(0f, 1f)]
    [SerializeField] private float outlineThickness = 0.2f;

    [Header("Timing")]
    // how long "DIE!!!!!" takes to go from nothing to full once he goes manic (seconds)
    [Min(0.01f)]
    [SerializeField] private float fadeInSeconds = 1f;

    // how long it takes to fade away when he stops being manic (seconds)
    [Min(0.01f)]
    [SerializeField] private float clearSeconds = 0.4f;

    [Header("Sound")]
    // plays while "DIE!!!!!" is on the screen and gets louder as it gets stronger.
    // leave empty for no sound
    [SerializeField] private AudioClip warningSound;

    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 1f;

    // ticked: the sound loops for as long as the words are showing
    [SerializeField] private bool loopSound = true;

    // the same jitter as the countdown timer and "Let Go": the words jump to a random spot up to this far (in pixels) from their place, again and again, for as long as they are showing.
    // 0 turns the jitter off.
    // it is read every frame, so you can drag the slider during Play mode and see the change straight away
    [Header("Jitter")]
    [Range(0f, 40f)]
    [SerializeField] private float jitterPixels = 10f;

    // how many times a second they jump to a new spot
    [Min(1f)]
    [SerializeField] private float jitterChangesPerSecond = 16f;

    private Conseraph conseraph;
    private CanvasGroup group;
    private RectTransform labelRect;
    private AudioSource warningSource;

    // time since the words last jumped to a new spot
    private float jitterTimer;

    private void Awake()
    {
        conseraph = GetComponent<Conseraph>();

        BuildUI();

        if (warningSound != null)
        {
            warningSource = gameObject.AddComponent<AudioSource>();
            warningSource.clip = warningSound;
            warningSource.loop = loopSound;
            warningSource.playOnAwake = false;
            warningSource.spatialBlend = 0f;
            warningSource.volume = 0f;
        }
    }

    private void OnDisable()
    {
        // nothing should be left on the screen or playing
        if (group != null)
            group.alpha = 0f;

        if (warningSource != null)
            warningSource.Stop();
    }

    // real time, so the freeze frame of a parry doesn't stall the fades
    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        float alpha = conseraph.Manic
            ? Mathf.MoveTowards(group.alpha, 1f, deltaTime / fadeInSeconds)
            : Mathf.MoveTowards(group.alpha, 0f, deltaTime / clearSeconds);

        group.alpha = alpha;

        UpdateSound(alpha);
        UpdateJitter(alpha, deltaTime);
    }

    // the sound plays while the words show, and its volume follows their opacity
    private void UpdateSound(float alpha)
    {
        if (warningSource == null)
            return;

        if (alpha <= 0f)
        {
            if (warningSource.isPlaying)
                warningSource.Stop();

            return;
        }

        if (!warningSource.isPlaying)
            warningSource.Play();

        warningSource.volume = alpha * maxVolume;
    }

    // a new random spot up to jitterPixels away, each time it is due, for as long as the words show.
    // when they are gone the text goes back to its place
    private void UpdateJitter(float alpha, float deltaTime)
    {
        if (alpha <= 0f)
        {
            labelRect.anchoredPosition = Vector2.zero;
            jitterTimer = 1f / jitterChangesPerSecond;
            return;
        }

        jitterTimer += deltaTime;

        if (jitterTimer >= 1f / jitterChangesPerSecond)
        {
            jitterTimer = 0f;
            labelRect.anchoredPosition = Random.insideUnitCircle * jitterPixels;
        }
    }

    // a full-screen overlay with one text in the middle, scaled so it looks the same on any screen size
    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("Manic Warning Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // invisible until he is manic, and it never catches clicks
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
        rect.sizeDelta = new Vector2(1800f, 400f);
        rect.anchoredPosition = Vector2.zero;

        labelRect = rect;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = message.ToUpperInvariant();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = textColor;
        label.raycastTarget = false;

        if (font != null)
            label.font = font;

        // the outline is drawn by the font's material, so it is switched on here (fontMaterial is this text's own copy)
        label.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
        label.outlineColor = outlineColor;
        label.outlineWidth = outlineThickness;
    }
}
