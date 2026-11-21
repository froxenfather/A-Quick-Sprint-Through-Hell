using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the pieces every full-screen overlay is made of, so each script doesn't carry its own copy.
// nothing to attach: these are plain helpers the other scripts call.
//   BuildOverlay   a screen overlay canvas that scales to any screen size, invisible and never catches clicks
//   CreateText     centered text on it, with the font outline switched on
//   ApplyOutline   the outline on any text (the HUD texts use this too)
//   Stretch        makes a UI object fill its parent
//   Jitter         the "jump to a random spot a few times a second" shake of the warnings and the timer
public static class UiKit
{
    // the screen the overlays are laid out on, scaled to fit the real screen
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // a full-screen overlay: a canvas on top of everything with a CanvasGroup to fade it.
    // it starts invisible (alpha 0) and never catches clicks.
    // sortingOrder decides which overlay is in front of which (the bigger one wins).
    // parent can be null for an overlay that is its own object, not a child of anything.
    // scaleWithScreen: the overlay is laid out on a 1920 x 1080 screen and scaled to fit the real one (right for text).
    // turn it off for overlays that work in real screen pixels (the burn marks) or just fill the screen with a colour (the parry flash)
    public static CanvasGroup BuildOverlay(Transform parent, string name, int sortingOrder, bool scaleWithScreen = true)
    {
        GameObject canvasObject = scaleWithScreen
            ? new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup))
            : new GameObject(name, typeof(Canvas), typeof(CanvasGroup));

        if (parent != null)
            canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        if (scaleWithScreen)
        {
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
        }

        CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        return group;
    }

    // centered text on an overlay.
    // anchor is where on the screen it sits, from (0, 0) bottom left to (1, 1) top right, so (0.5, 0.5) is the middle.
    // size is the box the text is laid out in, on the 1920 x 1080 screen.
    // font can be null for TextMesh Pro's default
    public static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchor, Vector2 size, TMP_FontAsset font, float fontSize, Color color, Color outlineColor, float outlineThickness)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = color;
        label.raycastTarget = false;

        if (font != null)
            label.font = font;

        ApplyOutline(label, outlineColor, outlineThickness);

        return label;
    }

    // the outline is drawn by the font's material, so it is switched on here.
    // fontMaterial is this text's own copy of it, so other texts that use the same font don't get the outline.
    // thickness: 0 is no outline, 1 is the thickest the font allows
    public static void ApplyOutline(TMP_Text label, Color color, float thickness)
    {
        label.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
        label.outlineColor = color;
        label.outlineWidth = thickness;
    }

    // makes a UI object fill the whole of its parent
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // the shake of the warnings and the timer: a new random spot up to a few pixels from the middle, a few times a second.
    // call Update every frame. pixels and changesPerSecond are passed in each time, so a slider dragged during Play mode takes effect straight away
    public class Jitter
    {
        private float timer;

        // active: the shake is on. off, the text goes back to its place (the first shake after that comes straight away)
        public void Update(RectTransform rect, bool active, float pixels, float changesPerSecond, float deltaTime)
        {
            float interval = 1f / Mathf.Max(1f, changesPerSecond);

            if (!active)
            {
                rect.anchoredPosition = Vector2.zero;
                timer = interval;
                return;
            }

            timer += deltaTime;

            if (timer >= interval)
            {
                timer = 0f;
                rect.anchoredPosition = Random.insideUnitCircle * pixels;
            }
        }
    }
}
