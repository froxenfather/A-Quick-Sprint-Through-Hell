using TMPro;
using UnityEngine;
using UnityEngine.UI;

// watches whether the player is still inside the room they are in, and loses them to the void if they aren't.
// the room is the one the player is currently in (and the ones either side of it, so going through a gate never counts as leaving).
// outside it, after a short grace period, "Let Go" fades in over the middle of the screen from nothing to full.
// if it reaches full the player dies to the void.
// while it is on the screen a sound fades up with it (the louder the words, the louder the void), and the words constantly jitter like the timer's.
// getting back inside at any time clears it and starts over.
// it does nothing until the player has gone into their first room, so the fall down the chute never counts.
// put this on the RoomChain object (or any object in TheFactory)
public class VoidWatch : MonoBehaviour
{
    // found at the start if left empty
    [SerializeField] private RoomChain roomChain;
    [SerializeField] private PlayerDeath playerDeath;

    [Header("Text")]
    [SerializeField] private string message = "Let Go";

    // the font (Midnight Spree, the scary one, filled in for you)
    [SerializeField] private TMP_FontAsset font;

    [Min(1f)]
    [SerializeField] private float fontSize = 160f;

    [SerializeField] private Color textColor = new Color(0.6f, 0.2f, 1f);
    [SerializeField] private Color outlineColor = Color.black;

    // 0 is no outline, 1 is the thickest the font allows
    [Range(0f, 1f)]
    [SerializeField] private float outlineThickness = 0.2f;

    [Header("Timing")]
    // how long the player can be outside the room before "Let Go" starts to appear (seconds)
    [Min(0f)]
    [SerializeField] private float graceSeconds = 0.5f;

    // how long "Let Go" takes to go from nothing to full, after the grace period.
    // at full the player dies to the void
    [Min(0.1f)]
    [SerializeField] private float fadeInSeconds = 5f;

    // how long it takes to fade away when the player gets back inside
    [Min(0.01f)]
    [SerializeField] private float clearSeconds = 0.4f;

    [Header("Sound")]
    // plays while "Let Go" is on the screen, and gets louder as it gets stronger: silent when it starts, at Max Volume when it is full.
    // it fades back down when the player gets back inside.
    // leave empty for no sound
    [SerializeField] private AudioClip consumeSound;

    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 1f;

    // ticked: the sound loops for as long as the words are showing.
    // unticked: it plays once, so use a sound that is at least as long as the fade
    [SerializeField] private bool loopSound = true;

    // the same jitter as the countdown timer: the words jump to a random spot up to this far (in pixels) from their place, again and again, for as long as they are showing.
    // 0 turns the jitter off.
    // it is read every frame, so you can drag the slider during Play mode and see the change straight away
    [Header("Jitter")]
    [Range(0f, 40f)]
    [SerializeField] private float jitterPixels = 6f;

    // how many times a second they jump to a new spot (the timer's clock changes picture 16 times a second while it animates)
    [Min(1f)]
    [SerializeField] private float jitterChangesPerSecond = 16f;

    [Header("Room bounds")]
    // how far (meters) outside a room's bounds still counts as inside.
    // the gap between two rooms is 1 m, so keep this above that
    [Min(0f)]
    [SerializeField] private float boundsMargin = 2f;

    private CustomPlayerController player;
    private CapsuleCollider playerCapsule;
    private CanvasGroup group;
    private RectTransform labelRect;

    // a 2D source for the sound, made at the start if there is a sound
    private AudioSource consumeSource;

    // time since the words last jumped to a new spot
    private float jitterTimer;

    // false until the player has gone into their first room
    private bool active;

    // how long the player has been outside the room, in seconds
    private float outsideTime;

    private void Awake()
    {
        if (roomChain == null)
            roomChain = FindFirstObjectByType<RoomChain>();

        if (playerDeath == null)
            playerDeath = FindFirstObjectByType<PlayerDeath>();

        player = FindFirstObjectByType<CustomPlayerController>();

        if (roomChain == null || playerDeath == null || player == null)
        {
            Debug.LogWarning("VoidWatch needs a RoomChain, a PlayerDeath and the player in the scene, so it is switched off.", this);
            enabled = false;
            return;
        }

        // CustomPlayerController requires a capsule collider, so it is always there
        playerCapsule = player.GetComponent<CapsuleCollider>();

        BuildUI();

        if (consumeSound != null)
        {
            consumeSource = gameObject.AddComponent<AudioSource>();
            consumeSource.clip = consumeSound;
            consumeSource.loop = loopSound;
            consumeSource.playOnAwake = false;
            consumeSource.spatialBlend = 0f;
            consumeSource.volume = 0f;
        }
    }

    private void OnEnable()
    {
        if (roomChain != null)
            roomChain.RoomEntered += OnRoomEntered;
    }

    private void OnDisable()
    {
        if (roomChain != null)
            roomChain.RoomEntered -= OnRoomEntered;
    }

    // the player went into a room: from now on they have to stay in it.
    // the RoomChain always knows which room they are in, so there is nothing to track here
    private void OnRoomEntered(Room room)
    {
        active = true;
    }

    private void Update()
    {
        if (!active)
            return;

        // already dying (Conseraph got them first, or the void did): nothing more to watch
        if (playerDeath.IsDead)
        {
            active = false;

            if (consumeSource != null)
                consumeSource.Stop();

            return;
        }

        float deltaTime = Time.deltaTime;

        // the middle of the player's capsule, the same point Conseraph chases
        Vector3 point = player.transform.TransformPoint(playerCapsule.center);
        bool inside = roomChain.IsInsideBounds(point, boundsMargin);

        float alpha;

        if (inside)
        {
            // back inside: the timer starts over and the words fade away
            outsideTime = 0f;
            alpha = Mathf.MoveTowards(group.alpha, 0f, deltaTime / clearSeconds);
        }
        else
        {
            // nothing during the grace period, then a straight line from 0 to 1
            outsideTime += deltaTime;
            alpha = Mathf.Clamp01((outsideTime - graceSeconds) / fadeInSeconds);
        }

        group.alpha = alpha;

        UpdateSound(alpha);
        UpdateJitter(alpha, deltaTime);

        if (!inside && alpha >= 1f)
            playerDeath.DieToVoid();
    }

    // the sound plays while the words show, and its volume follows their opacity
    private void UpdateSound(float alpha)
    {
        if (consumeSource == null)
            return;

        if (alpha <= 0f)
        {
            if (consumeSource.isPlaying)
                consumeSource.Stop();

            return;
        }

        if (!consumeSource.isPlaying)
            consumeSource.Play();

        consumeSource.volume = alpha * maxVolume;
    }

    // the same push as the timer's jitter: a new random spot up to jitterPixels away, each time it is due, for as long as the words show.
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
        GameObject canvasObject = new GameObject("Void Watch Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // invisible until the player is outside a room, and it never catches clicks
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

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = message;
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
