using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Big Clock Hours...
public class CountdownTimerHUD : MonoBehaviour
{
    [SerializeField] private RoomChain roomChain;
    [SerializeField] private Image clockImage;

    // the clock pictures in order.
    [SerializeField] private Sprite[] clockFrames = new Sprite[8];

    [SerializeField] private TMP_Text timeText;

    // how long the countdown starts from, in seconds.
    [Min(1f)]
    [SerializeField] private float durationSeconds = 310f;

    [Header("Time text")]
    // the digits are black with a thick outline.
    // the outline is drawn by the font's material, which is set up when the game starts
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private Color outlineColor = Color.red;

    // 0 is no outline, 1 is the thickest the font allows.
    // if the outline looks cut off at the edges, make the font asset with a bigger padding
    [Range(0f, 1f)]
    [SerializeField] private float outlineThickness = 0.15f;

    // shown instead of the time while the clock is not counting down (before the player goes through the first door of the chute)
    [SerializeField] private string idleMessage = "UR GOOD";

    [Header("Clock animation")]
    // the clock does one full loop of its pictures every this many seconds
    [Min(0.1f)]
    [SerializeField] private float cycleSeconds = 1f;

    // the pictures play during this part of each cycle, then it rests on the first picture for the rest
    [Min(0.05f)]
    [SerializeField] private float animationSeconds = 0.5f;

    [Header("Tick sound")]
    // played once per second while the countdown shows this many seconds or fewer, down to 00:00.
    // leave empty for no tick
    [SerializeField] private AudioClip tickSound;

    [Min(0)]
    [SerializeField] private int tickFromSeconds = 5;

    [Range(0f, 1f)]
    [SerializeField] private float tickVolume = 1f;

    // the most (in pixels) the group moves away from its place on each picture change.
    // 0 turns the jitter off
    // it is read every frame, so you can drag the slider during Play mode and see the change straight away
    [Header("Jitter")]
    [Range(0f, 40f)]
    [SerializeField] private float jitterPixels = 6f;

    private RectTransform rect;
    private Vector2 basePosition;

    // the text box's width in the scene, and the gap between the clock and the text, which the clock keeps when the words get longer
    private float textMinWidth;
    private float clockGap;

    private bool started;
    private float startTime;

    // for the tick sound: the whole seconds shown last frame, and the source that plays the tick
    private int lastShownSeconds = -1;
    private AudioSource tickSource;

    // the animation frame we last jittered for. -1 while resting, so the next animation starts with a fresh jitter
    private int lastJitterFrame = -1;

    private void Awake()
    {
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;

        if (roomChain == null)
            roomChain = FindFirstObjectByType<RoomChain>();

        // remember how the clock sits next to the text in the scene: the text box's width, and the gap between the clock's right edge and the box's left edge
        if (clockImage != null && timeText != null)
        {
            RectTransform textRect = timeText.rectTransform;
            RectTransform clockRect = clockImage.rectTransform;

            textMinWidth = textRect.rect.width;

            float textLeft = textRect.anchoredPosition.x - textMinWidth * textRect.pivot.x;
            float clockRight = clockRect.anchoredPosition.x + clockRect.rect.width * (1f - clockRect.pivot.x);

            clockGap = textLeft - clockRight;
        }

        // a 2D source for the tick, made here so nothing needs setting up
        tickSource = gameObject.AddComponent<AudioSource>();
        tickSource.playOnAwake = false;
        tickSource.spatialBlend = 0f;

        if (timeText != null)
        {
            timeText.color = textColor;

            // fontMaterial is this text's own copy of the font material, so the outline doesn't change other texts that use the same font
            Material material = timeText.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);

            timeText.outlineColor = outlineColor;
            timeText.outlineWidth = outlineThickness;
        }
    }

    // the text is centered in its box and spills out of both sides when the words are long, so the clock has to move out of the way.
    // the clock keeps the same gap from the left end of the text that it has in the scene
    private void PlaceClock()
    {
        if (clockImage == null || timeText == null)
            return;

        RectTransform textRect = timeText.rectTransform;
        RectTransform clockRect = clockImage.rectTransform;

        // how wide the words are, never less than the box in the scene
        float textWidth = Mathf.Max(textMinWidth, timeText.preferredWidth);

        float textLeft = textRect.anchoredPosition.x - textWidth * textRect.pivot.x;
        float clockRight = textLeft - clockGap;

        clockRect.anchoredPosition = new Vector2(clockRight - clockRect.rect.width * (1f - clockRect.pivot.x), clockRect.anchoredPosition.y);
    }

    private void LateUpdate()
    {
        PlaceClock();
    }

    private void OnEnable()
    {
        if (roomChain != null)
            roomChain.StarterExited += OnStarterExited;
    }

    private void OnDisable()
    {
        if (roomChain != null)
            roomChain.StarterExited -= OnStarterExited;

        rect.anchoredPosition = basePosition;
    }

    private void OnStarterExited()
    {
        started = true;
        startTime = Time.time;
    }

    private void Update()
    {
        float elapsed = started ? Time.time - startTime : 0f;
        bool finished = started && elapsed >= durationSeconds;

        float remaining = Mathf.Max(0f, Mathf.Ceil(durationSeconds - elapsed));

        if (started)
            ShowTime(remaining);
        else
            ShowIdleMessage();

        // tick each time the shown second changes, from tickFromSeconds down to 0
        int shownSeconds = Mathf.RoundToInt(remaining);

        if (shownSeconds != lastShownSeconds)
        {
            lastShownSeconds = shownSeconds;

            if (started && shownSeconds <= tickFromSeconds && tickSound != null)
                tickSource.PlayOneShot(tickSound, tickVolume);
        }

        // which picture to show, and whether the clock is in the animated half of the second
        int frame = 0;
        bool animating = false;

        if (started && !finished && clockFrames.Length > 0)
        {
            float phase = elapsed % cycleSeconds;

            if (phase < animationSeconds)
            {
                animating = true;
                frame = Mathf.Min(clockFrames.Length - 1, Mathf.FloorToInt(phase / animationSeconds * clockFrames.Length));
            }
        }

        if (animating)
        {
            // a new random push on every picture change
            if (frame != lastJitterFrame)
            {
                lastJitterFrame = frame;
                rect.anchoredPosition = basePosition + Random.insideUnitCircle * jitterPixels;
            }
        }
        else
        {
            // resting: snap back
            lastJitterFrame = -1;
            rect.anchoredPosition = basePosition;
        }

        if (clockImage != null && frame < clockFrames.Length && clockFrames[frame] != null)
            clockImage.sprite = clockFrames[frame];
    }

    // mm:ss, always two digits each: 05:10, 00:59
    private void ShowTime(float totalSeconds)
    {
        if (timeText == null)
            return;

        int seconds = Mathf.RoundToInt(totalSeconds);
        timeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }

    // the clock isn't counting down, so the text says so
    private void ShowIdleMessage()
    {
        if (timeText != null)
            timeText.text = idleMessage;
    }
}
