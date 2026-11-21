using UnityEngine;
using UnityEngine.Serialization;

// the sounds that come from Conseraph himself. put this on the Conseraph object.
// (the background music, including the enrage song, is in MusicPlayer)
//
//   The toll: a bell that loops from Conseraph himself, louder the closer the player is.
//     the volume is worked out here from the distance, so Unity's own distance fade is switched off.
//     it fades in and out with him, so it is silent while he is invisible.
//     he can be heard from further away while he is enraged, and much further while he is manic.
//
// leave the clip empty for no toll
[RequireComponent(typeof(Conseraph))]
public class ConseraphSound : MonoBehaviour
{
    [Header("Toll")]
    [SerializeField] private AudioClip tollClip;

    // the volume when he is right on top of the player
    [FormerlySerializedAs("maxVolume")]
    [Range(0f, 1f)]
    [SerializeField] private float tollMaxVolume = 1f;

    // the volume at the very edge of the hearing range, where the player first begins to hear him
    [FormerlySerializedAs("minVolume")]
    [Range(0f, 1f)]
    [SerializeField] private float tollMinVolume = 0.05f;

    // further from the player than this (meters) he can't be heard at all.
    // between here and the player the volume rises in a straight line from Toll Min Volume to Toll Max Volume
    [FormerlySerializedAs("hearingRange")]
    [Min(0.1f)]
    [SerializeField] private float tollHearingRange = 120f;

    // ENRAGED: while he is enraged he can be heard from much further away, and you can set how loud he is.
    // the hearing distance (meters): the volume still rises in a straight line from Toll Min Volume at this distance to the volume below on top of the player
    [FormerlySerializedAs("enragedHearingRange")]
    [Range(0.1f, 5000f)]
    [SerializeField] private float tollEnragedHearingRange = 600f;

    // the volume when he is right on top of the player, while he is enraged (instead of Toll Max Volume)
    [Range(0f, 1f)]
    [SerializeField] private float tollEnragedMaxVolume = 1f;

    // MANIC (Mania Mode): the same two settings, while he is manic
    [FormerlySerializedAs("manicHearingRange")]
    [Range(0.1f, 20000f)]
    [SerializeField] private float tollManicHearingRange = 10000f;

    // the volume when he is right on top of the player, while he is manic (instead of Toll Max Volume)
    [Range(0f, 1f)]
    [SerializeField] private float tollManicMaxVolume = 1f;

    private Conseraph conseraph;

    // the toll: a 3D source, or null if there is no toll clip
    private AudioSource toll;

    private void Awake()
    {
        conseraph = GetComponent<Conseraph>();

        if (tollClip == null)
        {
            Debug.LogWarning("ConseraphSound has no toll clip, so there is no toll sound.", this);
            enabled = false;
            return;
        }

        toll = CreateTollSource();
    }

    private void Start()
    {
        // Conseraph switches himself off when there is no player, and then there is no distance to go by
        if (!conseraph.enabled)
        {
            enabled = false;
            return;
        }

        toll.Play();
    }

    private void Update()
    {
        float distance = conseraph.DistanceToPlayer;

        // manic first, then enraged, then normal: each has its own hearing distance and its own volume on top of the player
        float range = tollHearingRange;
        float maxVolume = tollMaxVolume;

        if (conseraph.Manic)
        {
            range = tollManicHearingRange;
            maxVolume = tollManicMaxVolume;
        }
        else if (conseraph.Enraged)
        {
            range = tollEnragedHearingRange;
            maxVolume = tollEnragedMaxVolume;
        }

        float rangeVolume = distance >= range
            ? 0f
            : Mathf.Lerp(tollMinVolume, maxVolume, 1f - distance / range);

        toll.volume = rangeVolume * conseraph.Visibility;
    }

    // a 3D source on Conseraph, so the sound comes from him and pans toward him
    private AudioSource CreateTollSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
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

        return source;
    }
}
