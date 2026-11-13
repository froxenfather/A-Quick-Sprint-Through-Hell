using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// runs the "start the game" sequence.
// made by the Portal and kept alive across the scene change
//   1. every other sound stops (the video's own sound is the only one that keeps playing)
//   2. the next scene starts loading in the background, but is held back
//   3. the intro video plays full screen.
//      it can't be skipped
//   4. when it ends the screen is black, the next scene starts, and the black fades away
// it builds its own full-screen UI, so nothing needs setting up in a scene
public class GameStartSequence : MonoBehaviour
{
    // how long to wait for the video's first picture before giving up and carrying on
    private const float VideoStartTimeout = 5f;

    private Canvas canvas;
    private Image black;
    private RawImage videoImage;
    private AudioSource videoAudio;
    private VideoPlayer videoPlayer;
    private RenderTexture videoTexture;

    public static void Begin(string sceneName, VideoClip clip, float fadeInSeconds)
    {
        GameObject sequence = new GameObject("GameStartSequence");
        DontDestroyOnLoad(sequence);

        sequence.AddComponent<GameStartSequence>().Run(sceneName, clip, fadeInSeconds);
    }

    private void Run(string sceneName, VideoClip clip, float fadeInSeconds)
    {
        BuildUI();
        StartCoroutine(Sequence(sceneName, clip, fadeInSeconds));
    }

    private IEnumerator Sequence(string sceneName, VideoClip clip, float fadeInSeconds)
    {
        // 1. silence everything.
        //    the video's AudioSource ignores this pause
        AudioListener.pause = true;

        // 2. load the next scene behind the video, but don't start it yet
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        load.allowSceneActivation = false;

        // 3. the video
        if (clip != null)
            yield return PlayVideo(clip);

        // 4. black, then the scene
        videoImage.gameObject.SetActive(false);
        canvas.enabled = true;

        // a scene held back stops loading at 0.9
        while (load.progress < 0.9f)
            yield return null;

        load.allowSceneActivation = true;

        while (!load.isDone)
            yield return null;

        // one more frame, so the new scene's Start methods have run (the chute is built and the player is placed)
        yield return null;

        AudioListener.pause = false;

        float time = 0f;

        while (time < fadeInSeconds)
        {
            time += Time.unscaledDeltaTime;
            SetBlackAlpha(1f - Mathf.Clamp01(time / fadeInSeconds));
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator PlayVideo(VideoClip clip)
    {
        videoTexture = new RenderTexture((int)clip.width, (int)clip.height, 0);
        videoImage.texture = videoTexture;
        videoImage.GetComponent<AspectRatioFitter>().aspectRatio = (float)clip.width / clip.height;

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, videoAudio);

        bool finished = false;
        bool failed = false;

        videoPlayer.loopPointReached += _ => finished = true;
        videoPlayer.errorReceived += (_, message) =>
        {
            Debug.LogError("Intro video failed: " + message, this);
            failed = true;
        };

        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared && !failed)
            yield return null;

        if (failed)
            yield break;

        videoPlayer.Play();

        // show the video once it has a picture, so the lobby doesn't turn black before the first frame is ready
        float waited = 0f;

        while (videoPlayer.frame < 1 && !failed && !finished && waited < VideoStartTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        canvas.enabled = true;

        while (!finished && !failed)
            yield return null;
    }

    // a full-screen overlay: black, with the video fitted inside it
    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvas.enabled = false;

        GameObject blackObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
        blackObject.transform.SetParent(canvasObject.transform, false);

        black = blackObject.GetComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;
        Stretch((RectTransform)blackObject.transform);

        GameObject videoObject = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        videoObject.transform.SetParent(blackObject.transform, false);

        videoImage = videoObject.GetComponent<RawImage>();
        videoImage.raycastTarget = false;

        // fits inside the screen without stretching, black bars if the shape differs
        RectTransform videoRect = (RectTransform)videoObject.transform;
        videoRect.anchorMin = new Vector2(0.5f, 0.5f);
        videoRect.anchorMax = new Vector2(0.5f, 0.5f);
        videoRect.pivot = new Vector2(0.5f, 0.5f);

        AspectRatioFitter fitter = videoObject.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        // 2D, and it keeps playing while AudioListener.pause silences everything else
        videoAudio = gameObject.AddComponent<AudioSource>();
        videoAudio.playOnAwake = false;
        videoAudio.spatialBlend = 0f;
        videoAudio.ignoreListenerPause = true;
    }

    private void SetBlackAlpha(float alpha)
    {
        Color color = black.color;
        color.a = alpha;
        black.color = color;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        // never leave the game muted if this is destroyed early
        AudioListener.pause = false;

        if (videoTexture != null)
            videoTexture.Release();
    }
}
