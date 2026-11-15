using UnityEngine;

// the toll: a bell that loops from Conseraph himself, louder the closer the player is.
// the volume is worked out here from the distance, so Unity's own distance fade is switched off.
// it fades in and out with him, so it is silent while he is invisible
[RequireComponent(typeof(Conseraph))]
public class ConseraphAudio : MonoBehaviour
{
    [SerializeField] private AudioClip tollClip;

    [Header("Volume")]
    // the volume when he is right on top of the player
    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 1f;

    // the volume at the very edge of the hearing range, where the player first begins to hear him
    [Range(0f, 1f)]
    [SerializeField] private float minVolume = 0.05f;

    // further from the player than this (meters) he can't be heard at all.
    // between here and the player the volume rises in a straight line from minVolume to maxVolume
    [Min(0.1f)]
    [SerializeField] private float hearingRange = 80f;

    private Conseraph conseraph;
    private AudioSource source;

    private void Awake()
    {
        if (tollClip == null)
        {
            Debug.LogWarning("ConseraphAudio has no toll clip, so there is no toll sound.", this);
            enabled = false;
            return;
        }

        conseraph = GetComponent<Conseraph>();

        // a 3D source on Conseraph, so the sound comes from him and pans toward him
        source = gameObject.AddComponent<AudioSource>();
        source.clip = tollClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.volume = 0f;

        // a flat curve, so Unity doesn't also fade the sound by distance.
        // the doppler effect is off so his speed doesn't bend the pitch
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
        source.dopplerLevel = 0f;
    }

    private void Start()
    {
        // Conseraph switches himself off when there is no player, and then there is no distance to go by
        if (!conseraph.enabled)
        {
            enabled = false;
            return;
        }

        source.Play();
    }

    private void Update()
    {
        float distance = conseraph.DistanceToPlayer;

        float rangeVolume = distance >= hearingRange
            ? 0f
            : Mathf.Lerp(minVolume, maxVolume, 1f - distance / hearingRange);

        source.volume = rangeVolume * conseraph.Visibility;
    }
}
