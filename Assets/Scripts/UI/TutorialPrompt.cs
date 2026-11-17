using TMPro;
using UnityEngine;
using UnityEngine.UI;

// what shows on the screen when the player goes into a room.
// a room can have a Tutorial UI prefab, which you design yourself in Unity's UI editor (text, images, animations), or just a Tutorial Message, which types itself out letter by letter.
// the prefab is made when the player goes into the room and goes away when they leave, so it can be anything.
// it stays while the player is in that room, and fades out when they go into a room with nothing.
// it builds its own overlay canvas, so it only needs adding to an object in the scene (anywhere, for example the RoomChain object)
public class TutorialPrompt : MonoBehaviour
{
    // found at the start if left empty
    [SerializeField] private RoomChain roomChain;

    [Header("Text (for rooms with a Tutorial Message and no prefab)")]
    // leave empty for TextMesh Pro's default font
    [SerializeField] private TMP_FontAsset font;

    [Min(1f)]
    [SerializeField] private float fontSize = 54f;

    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color outlineColor = Color.black;

    // 0 is no outline, 1 is the thickest the font allows
    [Range(0f, 1f)]
    [SerializeField] private float outlineThickness = 0.25f;

    // how many letters appear each second
    [Min(1f)]
    [SerializeField] private float charactersPerSecond = 30f;

    [Header("Place (read when the game starts)")]
    // how far up the screen the message sits: 0 is the bottom edge, 1 is the top
    [Range(0f, 1f)]
    [SerializeField] private float heightOnScreen = 0.25f;

    // how wide the message can get before it wraps onto another line (on a 1920 x 1080 screen)
    [Min(100f)]
    [SerializeField] private float width = 1400f;

    [Header("Fade")]
    // how long a Tutorial UI prefab takes to fade in when the player goes into its room (a typed message doesn't fade in, it types)
    [Min(0.01f)]
    [SerializeField] private float fadeInSeconds = 1f;

    // how long the old message or prefab takes to fade away when the next room has none
    [Min(0.01f)]
    [SerializeField] private float fadeOutSeconds = 0.4f;

    private CanvasGroup group;
    private Transform canvasTransform;
    private TextMeshProUGUI label;

    // the prefab that is showing now, if any
    private GameObject currentUI;

    // how many letters the text message has, and how far the typing has got (a fraction of a letter counts, so the speed is smooth)
    private int totalCharacters;
    private float typed;

    // 1 while something should be showing, 0 when it should be gone
    private float goal;

    private void Awake()
    {
        if (roomChain == null)
            roomChain = FindFirstObjectByType<RoomChain>();

        if (roomChain == null)
        {
            Debug.LogWarning("TutorialPrompt can't find a RoomChain in this scene, so there are no room messages.", this);
            enabled = false;
            return;
        }

        BuildUI();
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

    // a room with a prefab shows it, and a room with only a message types the message out.
    // either one replaces whatever was up before, straight away.
    // a room with neither fades the old one away
    private void OnRoomEntered(Room room)
    {
        GameObject prefab = room.TutorialUI;
        string message = room.TutorialMessage;
        bool hasMessage = !string.IsNullOrWhiteSpace(message);

        if (prefab == null && !hasMessage)
        {
            goal = 0f;
            return;
        }

        ClearUI();

        if (prefab != null)
        {
            label.gameObject.SetActive(false);
            totalCharacters = 0;
            typed = 0f;

            ShowUI(prefab);

            // a prefab fades in from nothing
            group.alpha = 0f;
            goal = 1f;
            return;
        }
        else
        {
            label.gameObject.SetActive(true);

            // the text is laid out whole, so the lines don't jump around while it types.
            // none of the letters are shown yet
            label.text = message;
            label.maxVisibleCharacters = 0;
            label.ForceMeshUpdate();

            totalCharacters = label.textInfo.characterCount;
            typed = 0f;
        }

        group.alpha = 1f;
        goal = 1f;
    }

    private void Update()
    {
        if (typed < totalCharacters)
        {
            typed += charactersPerSecond * Time.unscaledDeltaTime;
            label.maxVisibleCharacters = Mathf.Min(totalCharacters, Mathf.FloorToInt(typed));
        }

        if (!Mathf.Approximately(group.alpha, goal))
        {
            float seconds = goal > group.alpha ? fadeInSeconds : fadeOutSeconds;
            group.alpha = Mathf.MoveTowards(group.alpha, goal, Time.unscaledDeltaTime / seconds);
        }

        // faded all the way out: the prefab isn't needed any more
        if (goal == 0f && group.alpha <= 0f && currentUI != null)
            ClearUI();
    }

    // makes the prefab on the overlay canvas.
    // a prefab that is a whole Canvas (made by right-clicking in the Hierarchy and choosing UI) is stretched over the screen, since a Canvas has no place of its own.
    // any other prefab keeps the place it was designed in
    private void ShowUI(GameObject prefab)
    {
        currentUI = Instantiate(prefab, canvasTransform, false);

        if (currentUI.GetComponent<Canvas>() != null)
            Stretch((RectTransform)currentUI.transform);
    }

    private void ClearUI()
    {
        if (currentUI == null)
            return;

        Destroy(currentUI);
        currentUI = null;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // a full-screen overlay with one text on it, scaled so it looks the same on any screen size
    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("Tutorial Prompt Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        canvasTransform = canvasObject.transform;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // invisible until the first message, and it never catches clicks
        group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, heightOnScreen);
        rect.anchorMax = new Vector2(0.5f, heightOnScreen);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 300f);
        rect.anchoredPosition = Vector2.zero;

        label = textObject.GetComponent<TextMeshProUGUI>();
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
