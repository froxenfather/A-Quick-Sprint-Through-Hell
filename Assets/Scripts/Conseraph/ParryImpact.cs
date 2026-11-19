using UnityEngine;
using UnityEngine.UI;

// the freeze frame when the player parries Conseraph.
// the moment the parry lands a hit sound plays, the screen flashes and time stops for a moment.
// when it ends, his flight and the player's throw begin (both were already set going, they just wait for time to run again).
// his own Parry Sound then plays as he starts to fly.
// put this on the Conseraph object
[RequireComponent(typeof(Conseraph))]
public class ParryImpact : MonoBehaviour
{
    [Header("Freeze")]
    // how long time stands still (seconds, in real time).
    // 0 turns the freeze off, the sound and the flash still happen
    [Min(0f)]
    [SerializeField] private float freezeSeconds = 0.15f;

    [Header("Sound")]
    // plays the moment the parry lands, before the freeze.
    // leave empty for no sound
    [SerializeField] private AudioClip hitSound;

    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 1f;

    [Header("Flash")]
    // the colour of the flash (its own opacity is ignored, the strength below decides)
    [SerializeField] private Color flashColor = Color.white;

    // how bright the flash is at its brightest, 0 is nothing and 1 is a solid screen
    [Range(0f, 1f)]
    [SerializeField] private float flashStrength = 0.85f;

    // how long the flash takes to fade out once the freeze is over (seconds)
    [Min(0.01f)]
    [SerializeField] private float flashFadeSeconds = 0.25f;

    private Conseraph conseraph;

    // a flat 2D source for the hit sound, made at the start if there is a sound
    private AudioSource hitSource;

    private GameObject flashCanvas;
    private CanvasGroup flashGroup;

    // true while time is stopped
    private bool frozen;
    private float freezeTimeLeft;

    // the frame the parry landed in has already run, so the countdown starts on the next one
    private bool freezeJustStarted;

    // the game's speed before the freeze, put back when it ends
    private float previousTimeScale = 1f;

    private void Awake()
    {
        conseraph = GetComponent<Conseraph>();

        if (hitSound != null)
        {
            hitSource = gameObject.AddComponent<AudioSource>();
            hitSource.playOnAwake = false;
            hitSource.spatialBlend = 0f;
        }

        BuildFlash();
    }

    private void OnEnable()
    {
        conseraph.Parried += OnParried;
    }

    private void OnDisable()
    {
        conseraph.Parried -= OnParried;

        // never leave the game stopped, whatever happens to this object during a freeze
        EndFreeze();
    }

    private void OnDestroy()
    {
        if (flashCanvas != null)
            Destroy(flashCanvas);
    }

    private void OnParried()
    {
        if (hitSource != null)
            hitSource.PlayOneShot(hitSound, hitVolume);

        flashGroup.alpha = flashStrength;

        if (freezeSeconds <= 0f || frozen)
            return;

        frozen = true;
        freezeJustStarted = true;
        freezeTimeLeft = freezeSeconds;

        // a timeScale of 0 would be a paused game if it were already 0, so that is never the speed to come back to
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
    }

    // everything here runs on real time, the game's own time is stopped during the freeze
    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        if (frozen)
        {
            if (freezeJustStarted)
            {
                freezeJustStarted = false;
                return;
            }

            freezeTimeLeft -= deltaTime;

            if (freezeTimeLeft <= 0f)
                EndFreeze();

            // the flash holds at full while time is stopped and starts to fade once it runs again
            return;
        }

        if (flashGroup.alpha > 0f)
            flashGroup.alpha = Mathf.MoveTowards(flashGroup.alpha, 0f, flashStrength / flashFadeSeconds * deltaTime);
    }

    private void EndFreeze()
    {
        if (!frozen)
            return;

        frozen = false;
        Time.timeScale = previousTimeScale;
    }

    // a full-screen overlay of one flat colour, invisible until a parry.
    // it is its own object, not a child of Conseraph, so his position and size can't touch it
    private void BuildFlash()
    {
        flashCanvas = new GameObject("Parry Flash Canvas", typeof(Canvas), typeof(CanvasGroup), typeof(Image));

        Canvas canvas = flashCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // above the HUD, the burn overlay and the messages, below only the death sequence
        canvas.sortingOrder = 110;

        flashGroup = flashCanvas.GetComponent<CanvasGroup>();
        flashGroup.alpha = 0f;
        flashGroup.interactable = false;
        flashGroup.blocksRaycasts = false;

        Color color = flashColor;
        color.a = 1f;

        Image image = flashCanvas.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
