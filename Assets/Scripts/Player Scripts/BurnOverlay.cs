using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// black burn marks across the screen that get worse the more the player is burnt.
// put this on the Player, next to PlayerBurn

// it builds its own screen overlay when the game starts, so there is nothing to set up in the scene, and it draws its own
// textures from noise, so there are no image files either.
// every burn adds a mark, the marks grow as the burn level rises, and the edges of the screen darken.
// healing fades it all away
public class BurnOverlay : MonoBehaviour
{
    [SerializeField] private PlayerBurn burn;

    [Header("Burn marks")]
    // most marks on screen at once.
    // older ones go first
    [Min(1)]
    [SerializeField] private int maxMarks = 40;

    // mark size as a fraction of the screen height, at the first burn and at full burn
    [Range(0.05f, 1f)]
    [SerializeField] private float smallestMarkSize = 0.18f;

    [Range(0.05f, 2f)]
    [SerializeField] private float largestMarkSize = 0.7f;

    // how fast the marks grow to the size for the current burn level
    [Min(0.1f)]
    [SerializeField] private float growSpeed = 3f;

    [Header("Darkness")]
    // how dark the whole screen gets at full burn.
    // it rises in a straight line with the burn level
    [Range(0f, 1f)]
    [SerializeField] private float screenDarkness = 0.5f;

    // how dark the edges of the screen get at full burn
    [Range(0f, 1f)]
    [SerializeField] private float edgeDarkness = 0.85f;

    [Header("Healing")]
    // seconds the overlay takes to fade away when we heal
    [Min(0.05f)]
    [SerializeField] private float healFadeTime = 0.6f;

    private class Mark
    {
        public RectTransform rect;
        public float sizeAtBirth;
    }

    private const int MarkTextureSize = 128;
    private const int MarkVariants = 4;

    private readonly List<Mark> marks = new List<Mark>();
    private readonly Sprite[] markSprites = new Sprite[MarkVariants];

    private CanvasGroup group;
    private RectTransform marksRoot;
    private Image tint;
    private Image edges;

    private bool healing;

    private void Awake()
    {
        if (burn == null)
            burn = GetComponent<PlayerBurn>();

        BuildOverlay();
    }

    private void OnEnable()
    {
        burn.Singed += OnSinged;
        burn.Soothed += RemoveOldestMark;
        burn.Healed += OnHealed;
    }

    private void OnDisable()
    {
        burn.Singed -= OnSinged;
        burn.Soothed -= RemoveOldestMark;
        burn.Healed -= OnHealed;
    }

    private void OnDestroy()
    {
        // the overlay lives on its own object (so it can be a screen overlay), clean it up with us
        if (group != null)
            Destroy(group.gameObject);
    }

    private void Update()
    {
        // the whole screen and its edges both get darker as the burn level rises, a little more with every singe level
        tint.color = new Color(0f, 0f, 0f, burn.Burn * screenDarkness);
        edges.color = new Color(0f, 0f, 0f, burn.Burn * edgeDarkness);

        // the marks grow toward the size that matches the current burn level
        float targetSize = Mathf.Lerp(smallestMarkSize, largestMarkSize, burn.Burn) * Screen.height;

        foreach (Mark mark in marks)
        {
            float size = Mathf.MoveTowards(mark.rect.sizeDelta.x, Mathf.Max(mark.sizeAtBirth, targetSize), growSpeed * Screen.height * 0.1f * Time.deltaTime);
            mark.rect.sizeDelta = new Vector2(size, size);
        }

        if (healing)
        {
            group.alpha -= Time.deltaTime / healFadeTime;

            if (group.alpha <= 0f)
                FinishHealing();
        }
    }

    private void OnSinged()
    {
        // a burn during a fade-out brings the overlay straight back
        if (healing)
            FinishHealing();

        AddMark();
    }

    // one singe level healed: its oldest mark goes
    private void RemoveOldestMark()
    {
        if (marks.Count == 0)
            return;

        Destroy(marks[0].rect.gameObject);
        marks.RemoveAt(0);
    }

    private void OnHealed()
    {
        healing = true;
    }

    private void FinishHealing()
    {
        healing = false;
        group.alpha = 1f;

        foreach (Mark mark in marks)
            Destroy(mark.rect.gameObject);

        marks.Clear();
    }

    private void AddMark()
    {
        // old marks make room for new ones
        if (marks.Count >= maxMarks)
        {
            Destroy(marks[0].rect.gameObject);
            marks.RemoveAt(0);
        }

        GameObject markObject = new GameObject("Burn Mark", typeof(RectTransform), typeof(Image));
        markObject.transform.SetParent(marksRoot, false);

        Image image = markObject.GetComponent<Image>();
        image.sprite = markSprites[Random.Range(0, MarkVariants)];
        image.color = new Color(0f, 0f, 0f, Random.Range(0.8f, 0.97f));
        image.raycastTarget = false;

        float size = Mathf.Lerp(smallestMarkSize, largestMarkSize, burn.Burn) * Screen.height * Random.Range(0.6f, 1f);

        RectTransform rect = markObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = new Vector2(
            Random.Range(-0.5f, 0.5f) * Screen.width,
            Random.Range(-0.5f, 0.5f) * Screen.height);
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        marks.Add(new Mark { rect = rect, sizeAtBirth = size });
    }

    // ============================================================
    // BUILDING THE OVERLAY
    // ============================================================

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("Burn Overlay", typeof(Canvas), typeof(CanvasGroup));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // on top of everything else on screen
        canvas.sortingOrder = 100;

        group = canvasObject.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // a plain black layer over the whole screen, so every singe level darkens everything a bit more
        GameObject tintObject = new GameObject("Tint", typeof(RectTransform), typeof(Image));
        tintObject.transform.SetParent(canvasObject.transform, false);

        RectTransform tintRect = tintObject.GetComponent<RectTransform>();
        tintRect.anchorMin = Vector2.zero;
        tintRect.anchorMax = Vector2.one;
        tintRect.offsetMin = Vector2.zero;
        tintRect.offsetMax = Vector2.zero;

        tint = tintObject.GetComponent<Image>();
        tint.color = Color.clear;
        tint.raycastTarget = false;

        // edges next, so the marks are drawn over them
        GameObject edgesObject = new GameObject("Edges", typeof(RectTransform), typeof(Image));
        edgesObject.transform.SetParent(canvasObject.transform, false);

        RectTransform edgesRect = edgesObject.GetComponent<RectTransform>();
        edgesRect.anchorMin = Vector2.zero;
        edgesRect.anchorMax = Vector2.one;
        edgesRect.offsetMin = Vector2.zero;
        edgesRect.offsetMax = Vector2.zero;

        edges = edgesObject.GetComponent<Image>();
        edges.sprite = MakeSprite(MakeEdgeTexture());
        edges.color = Color.clear;
        edges.raycastTarget = false;

        GameObject marksObject = new GameObject("Marks", typeof(RectTransform));
        marksObject.transform.SetParent(canvasObject.transform, false);
        marksRoot = marksObject.GetComponent<RectTransform>();

        for (int i = 0; i < MarkVariants; i++)
            markSprites[i] = MakeSprite(MakeMarkTexture(i * 17.31f));
    }

    private static Sprite MakeSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    // black on the outside of the screen, clear in the middle
    private static Texture2D MakeEdgeTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);

                texture.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0.45f, 1.3f, distance)));
            }
        }

        texture.Apply();
        return texture;
    }

    // a scorched, ragged blob: round at the heart, with an edge wobbled by noise.
    // different seeds give different shapes
    private static Texture2D MakeMarkTexture(float seed)
    {
        Texture2D texture = new Texture2D(MarkTextureSize, MarkTextureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < MarkTextureSize; y++)
        {
            for (int x = 0; x < MarkTextureSize; x++)
            {
                float nx = (x + 0.5f) / MarkTextureSize * 2f - 1f;
                float ny = (y + 0.5f) / MarkTextureSize * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float angle = Mathf.Atan2(ny, nx);

                // how far out the edge is in this direction, pushed in and out by noise (one layer wide, one fine)
                float wobble =
                    Mathf.PerlinNoise(Mathf.Cos(angle) * 1.4f + seed, Mathf.Sin(angle) * 1.4f + seed) * 0.5f +
                    Mathf.PerlinNoise(Mathf.Cos(angle) * 4f + seed, Mathf.Sin(angle) * 4f + seed) * 0.2f;

                float edge = 0.35f + wobble;

                float alpha = 1f - Mathf.SmoothStep(edge - 0.18f, edge, distance);

                // streaky, patchy burn instead of a smooth blob
                alpha *= 0.65f + 0.35f * Mathf.PerlinNoise(x * 0.09f + seed, y * 0.09f + seed);

                texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }
}
