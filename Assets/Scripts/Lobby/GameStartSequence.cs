using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// runs the "change scene behind a video" sequence.
// made by the Portal (start the game) and by PlayerDeath (go back to the lobby), and kept alive across the scene change
//   1. every other sound stops (the video's own sound is the only one that keeps playing)
//   2. the next scene starts loading in the background, but is held back
//   3. the video plays full screen.
//      it can't be skipped
//   4. if there is a stats page, it shows now on black, one line at a time, until the player presses a key or clicks
//   5. the screen is black, the next scene starts, and the black fades away
// it builds its own full-screen UI, so nothing needs setting up in a scene
public class GameStartSequence : MonoBehaviour
{
    // how long to wait for the video's first picture before giving up and carrying on
    private const float VideoStartTimeout = 5f;

    // the stats rows: how wide the column is, how tall each row is, and how big the text is (on the 1920 x 1080 reference screen)
    private const float StatsWidth = 1000f;
    private const float StatsRowHeight = 70f;
    private const float StatsFontSize = 46f;

    // how far (on the 1920 x 1080 screen) the stats column is moved down when there is a header above it
    private const float StatsDropForHeader = 140f;

    // after the last stat lands, keys pressed in this time (seconds) are ignored, so a key still being mashed doesn't skip it
    private const float AnyKeyGrace = 0.5f;

    // what the stats page shows and how it looks
    public class StatsPage
    {
        // one label and its value per line, in the order they appear
        public List<(string label, string value)> Lines;

        public TMP_FontAsset Font;

        // played as each line lands (the video's sound source ignores the pause, so it is heard)
        public AudioClip Thud;
        public float ThudVolume = 1f;

        // the pause before each line appears
        public float LineDelay = 0.8f;

        public Color LabelColor = Color.white;
        public Color ValueColor = Color.red;

        // an optional header at the very top of the screen, shown before the stats: a large title and a smaller tip under it.
        // leave the title empty for no header
        public string HeaderTitle;
        public Color HeaderTitleColor = Color.white;
        public float HeaderTitleSize = 110f;
        public string HeaderTip;
        public Color HeaderTipColor = Color.white;
        public float HeaderTipSize = 42f;
        public TMP_FontAsset HeaderFont;
    }

    private Canvas canvas;
    private Image black;
    private RawImage videoImage;
    private AudioSource videoAudio;
    private VideoPlayer videoPlayer;
    private RenderTexture videoTexture;

    // stats is optional: pass one to show the stats page after the video
    public static void Begin(string sceneName, VideoClip clip, float fadeInSeconds, StatsPage stats = null)
    {
        GameObject sequence = new GameObject("GameStartSequence");
        DontDestroyOnLoad(sequence);

        sequence.AddComponent<GameStartSequence>().Run(sceneName, clip, fadeInSeconds, stats);
    }

    private void Run(string sceneName, VideoClip clip, float fadeInSeconds, StatsPage stats)
    {
        BuildUI();
        StartCoroutine(Sequence(sceneName, clip, fadeInSeconds, stats));
    }

    private IEnumerator Sequence(string sceneName, VideoClip clip, float fadeInSeconds, StatsPage stats)
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

        // 4. black, and the stats if there are any
        videoImage.gameObject.SetActive(false);
        canvas.enabled = true;

        if (stats != null)
            yield return ShowStats(stats);

        // 5. the scene.
        //    a scene held back stops loading at 0.9
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

    // a column of stats on the black screen, one line at a time with a thud.
    // it ends when the player presses a key or clicks, once every line is showing
    private IEnumerator ShowStats(StatsPage page)
    {
        GameObject container = new GameObject("Stats", typeof(RectTransform));
        container.transform.SetParent(black.transform, false);

        // the header (how the player died) at the very top, if there is one
        GameObject header = string.IsNullOrEmpty(page.HeaderTitle) ? null : BuildHeader(page);

        // centered, tall enough for every row.
        // with a header it sits lower, so the two don't overlap
        RectTransform containerRect = (RectTransform)container.transform;
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(StatsWidth, page.Lines.Count * StatsRowHeight);
        containerRect.anchoredPosition = header != null ? new Vector2(0f, -StatsDropForHeader) : Vector2.zero;

        // every row starts hidden
        GameObject[] rows = new GameObject[page.Lines.Count];

        for (int i = 0; i < rows.Length; i++)
            rows[i] = BuildRow(containerRect, i, page.Lines[i].label, page.Lines[i].value, page);

        // the header lands first, then each row, one thud at a time.
        // the first key or click while they are still arriving shows everything that is left at once (one thud for the lot).
        // the next one leaves, as it always did.
        // keys pressed in the first moment of the screen are ignored, so a key still being mashed from the death doesn't skip it
        float skipLockedUntil = Time.unscaledTime + AnyKeyGrace;
        bool skipped = false;

        for (int i = header != null ? -1 : 0; i < rows.Length; i++)
        {
            // the item the skip happens on still gets its thud, the ones after it are silent
            if (!skipped)
            {
                float waited = 0f;

                while (waited < page.LineDelay)
                {
                    if (Time.unscaledTime >= skipLockedUntil && AnyKeyOrClick())
                    {
                        skipped = true;
                        break;
                    }

                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (page.Thud != null)
                    videoAudio.PlayOneShot(page.Thud, page.ThudVolume);
            }

            // -1 is the header
            if (i < 0)
                header.SetActive(true);
            else
                rows[i].SetActive(true);
        }

        // after a skip this also makes sure the click that skipped can't leave straight away
        yield return new WaitForSecondsRealtime(AnyKeyGrace);

        while (!AnyKeyOrClick())
            yield return null;

        container.SetActive(false);

        if (header != null)
            header.SetActive(false);
    }

    // the title and the tip at the top of the screen, hidden until it is their turn.
    // the title is large, and the tip is smaller and wraps onto a second line if it is long
    private GameObject BuildHeader(StatsPage page)
    {
        GameObject header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(black.transform, false);
        Stretch((RectTransform)header.transform);

        BuildHeaderText(header.transform, "Title", page.HeaderTitle, page.HeaderTitleSize, page.HeaderTitleColor, page.HeaderFont, -30f, 150f, 1700f);

        if (!string.IsNullOrEmpty(page.HeaderTip))
            BuildHeaderText(header.transform, "Tip", page.HeaderTip, page.HeaderTipSize, page.HeaderTipColor, page.HeaderFont, -190f, 120f, 1500f);

        header.SetActive(false);
        return header;
    }

    private static void BuildHeaderText(Transform parent, string objectName, string text, float size, Color color, TMP_FontAsset font, float top, float height, float width)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        // centred along the top edge of the screen
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, top);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Top;
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;

        // a very long title or tip (the manic death) shrinks to fit the box. the others fit already, so they stay at their size
        label.enableAutoSizing = true;
        label.fontSizeMax = size;
        label.fontSizeMin = 12f;

        if (font != null)
            label.font = font;
    }

    // one row: the label on the left and the value on the right.
    // rows go from the top of the column downward
    private static GameObject BuildRow(RectTransform parent, int index, string label, string value, StatsPage page)
    {
        GameObject row = new GameObject("Row " + index, typeof(RectTransform));
        row.transform.SetParent(parent, false);

        RectTransform rowRect = (RectTransform)row.transform;
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, StatsRowHeight);
        rowRect.anchoredPosition = new Vector2(0f, -index * StatsRowHeight);

        BuildText(rowRect, "Label", label, TextAlignmentOptions.MidlineLeft, page.LabelColor, page.Font);
        BuildText(rowRect, "Value", value, TextAlignmentOptions.MidlineRight, page.ValueColor, page.Font);

        row.SetActive(false);
        return row;
    }

    private static void BuildText(RectTransform parent, string objectName, string text, TextAlignmentOptions alignment, Color color, TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        Stretch((RectTransform)textObject.transform);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = alignment;
        label.color = color;
        label.fontSize = StatsFontSize;
        label.raycastTarget = false;

        if (font != null)
            label.font = font;
    }

    // any key on the keyboard, or the left or right mouse button, pressed this frame.
    // the old Input class is turned off in this project, so this uses the Input System
    private static bool AnyKeyOrClick()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        bool key = keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        bool click = mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);

        return key || click;
    }

    // a full-screen overlay: black, with the video fitted inside it
    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvas.enabled = false;

        // scaled so the stats look the same on any screen size (the black and the video stretch to fill, so they don't care)
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

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
