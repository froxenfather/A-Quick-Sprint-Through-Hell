using TMPro;
using UnityEngine;

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

    // the sound that gets louder with the words, and the words' jitter (see AudioKit and UiKit)
    private AudioKit.LevelSound sound;
    private readonly UiKit.Jitter jitter = new UiKit.Jitter();

    // false until the player has gone into their first room
    private bool active;

    // how long the player has been outside the room, in seconds
    private float outsideTime;

    private void Awake()
    {
        if (roomChain == null)
            roomChain = FindAnyObjectByType<RoomChain>();

        if (playerDeath == null)
            playerDeath = FindAnyObjectByType<PlayerDeath>();

        player = FindAnyObjectByType<CustomPlayerController>();

        if (roomChain == null || playerDeath == null || player == null)
        {
            Debug.LogWarning("VoidWatch needs a RoomChain, a PlayerDeath and the player in the scene, so it is switched off.", this);
            enabled = false;
            return;
        }

        // CustomPlayerController requires a capsule collider, so it is always there
        playerCapsule = player.GetComponent<CapsuleCollider>();

        BuildUI();

        sound = new AudioKit.LevelSound(gameObject, consumeSound, loopSound);
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
            sound.Stop();

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

        // the sound gets louder with the words, and the words jitter for as long as they show
        sound.Set(alpha, maxVolume);
        jitter.Update(labelRect, alpha > 0f, jitterPixels, jitterChangesPerSecond, deltaTime);

        if (!inside && alpha >= 1f)
            playerDeath.Die(DeathMessages.Void);
    }

    // a full-screen overlay with one text in the middle, invisible until the player is outside a room
    private void BuildUI()
    {
        group = UiKit.BuildOverlay(transform, "Void Watch Canvas", 70);

        TextMeshProUGUI label = UiKit.CreateText(group.transform, "Message", new Vector2(0.5f, 0.5f), new Vector2(1700f, 300f), font, fontSize, textColor, outlineColor, outlineThickness);
        label.text = message;

        labelRect = label.rectTransform;
    }
}
