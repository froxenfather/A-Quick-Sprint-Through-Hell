using UnityEngine;
using UnityEngine.UI;

// a beating heart on the HUD, shown only while Conseraph is hunting.
// the three hearts in heart_sheet play in order in a quarter of a second, then it rests on the first for the rest of the second.
// when Conseraph is hunting and closer than his catch-up distance, the beat speeds up in a straight line, up to 3.5 times as fast at his close distance.
// put this on a Raw Image under the HUD canvas whose Texture is heart_sheet (it finds the Raw Image on its own object if the slot is empty)
public class HeartbeatHUD : MonoBehaviour
{
    [SerializeField] private RawImage heart;

    // how many hearts are in the sheet, side by side
    [Min(1)]
    [SerializeField] private int frames = 3;

    [Header("Beat (at normal speed)")]
    // the hearts play during this part of each beat, then it rests on the first one for the rest.
    // 0.25 and 0.75 make a beat a second long
    [Min(0.01f)]
    [SerializeField] private float animationSeconds = 0.25f;

    [Min(0f)]
    [SerializeField] private float restSeconds = 0.75f;

    [Header("Speeding up")]
    // how many times faster the beat is at Conseraph's close distance.
    // it grows in a straight line from 1 at his catch-up distance
    [Min(1f)]
    [SerializeField] private float maxSpeedMultiplier = 3.5f;

    private Conseraph conseraph;
    private bool hasConseraph;

    // how far through the beat we are, from 0 to 1.
    // adding to it each frame (rather than working it out from the time) means a change of speed never makes the heart jump
    private float beat;

    private void Awake()
    {
        if (heart == null)
            heart = GetComponent<RawImage>();

        if (heart == null)
        {
            Debug.LogWarning("HeartbeatHUD needs a Raw Image (put this script on one, or drag one into Heart), so there is no heartbeat.", this);
            enabled = false;
            return;
        }

        conseraph = FindFirstObjectByType<Conseraph>();
        hasConseraph = conseraph != null;
    }

    private void Update()
    {
        // the heart only shows while he is hunting.
        // it starts again from the first heart each time he begins
        bool visible = !hasConseraph || conseraph.Hunting;

        if (heart.enabled != visible)
        {
            heart.enabled = visible;
            beat = 0f;
        }

        if (!visible)
            return;

        beat = (beat + Time.deltaTime * SpeedMultiplier() / (animationSeconds + restSeconds)) % 1f;

        // the first part of the beat plays the hearts in order, the rest rests on the first
        float position = beat * (animationSeconds + restSeconds);
        int frame = 0;

        if (position < animationSeconds)
            frame = Mathf.Min(frames - 1, Mathf.FloorToInt(position / animationSeconds * frames));

        heart.uvRect = new Rect(frame / (float)frames, 0f, 1f / frames, 1f);
    }

    // normal while he isn't hunting or is far away, then faster the closer he gets.
    // 1 at his catch-up distance, maxSpeedMultiplier at his close distance
    private float SpeedMultiplier()
    {
        if (!hasConseraph || !conseraph.Hunting)
            return 1f;

        float closeness = Mathf.InverseLerp(conseraph.CatchUpDistance, conseraph.CloseDistance, conseraph.DistanceToPlayer);

        return Mathf.Lerp(1f, maxSpeedMultiplier, closeness);
    }
}
