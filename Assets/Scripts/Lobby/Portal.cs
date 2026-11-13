using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// the flat object at the bottom of the lobby's chute.
// when the player touches it the game starts:
// controls freeze, the intro video plays full screen while the next scene loads, then the next scene begins (see GameStartSequence)
// needs a Collider set to Is Trigger (one is set up for you when the script is first added)
[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    // the scene to go to.
    // it has to be in Build Settings (menu: A Quick Sprint Through Hell > Set Up Build Scenes)
    [SerializeField] private string nextSceneName = "TheFactory";

    // one of these is picked at random each time and played full screen, with sound, while the next scene loads
    // empty slots are ignored.
    // leave them all empty to skip the video
    [SerializeField] private VideoClip[] introVideos = new VideoClip[5];

    // the old single video slot.
    // a video in it is moved into the list the first time this is loaded in the editor
    [SerializeField, HideInInspector] private VideoClip introVideo;

    // seconds for the screen to fade from black to clear once the next scene has started
    [Min(0f)]
    [SerializeField] private float fadeInSeconds = 1f;

    private bool triggered;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnValidate()
    {
        if (introVideo == null)
            return;

        // move the old single video into the first empty slot
        if (introVideos == null || introVideos.Length == 0)
            introVideos = new VideoClip[5];

        for (int i = 0; i < introVideos.Length; i++)
        {
            if (introVideos[i] == null)
            {
                introVideos[i] = introVideo;
                break;
            }
        }

        introVideo = null;
    }

    // a random video from the filled slots, or none if they are all empty
    private VideoClip PickVideo()
    {
        List<VideoClip> filled = new List<VideoClip>();

        if (introVideos != null)
        {
            foreach (VideoClip clip in introVideos)
            {
                if (clip != null)
                    filled.Add(clip);
            }
        }

        return filled.Count > 0 ? filled[Random.Range(0, filled.Count)] : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        CustomPlayerController player = other.GetComponentInParent<CustomPlayerController>();

        if (player == null)
            return;

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"Portal can't load scene '{nextSceneName}'. Add it to Build Settings (menu: A Quick Sprint Through Hell > Set Up Build Scenes) and check the name.", this);
            return;
        }

        triggered = true;

        // freeze the player where they are, they are about to be replaced by the next scene's player
        player.enabled = false;

        PlayerLook look = player.GetComponent<PlayerLook>();

        if (look != null)
            look.enabled = false;

        // this object is in the lobby, which gets unloaded, so the rest runs on an object that survives the scene change
        GameStartSequence.Begin(nextSceneName, PickVideo(), fadeInSeconds);
    }
}
