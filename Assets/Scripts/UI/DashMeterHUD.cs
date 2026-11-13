using UnityEngine;
using UnityEngine.UI;

// a row of rounded rectangles, one per dash charge
//   blue  = no charge yet
//   green = the charge that is filling up (the shape fills left to right)
//   teal  = full, a dash can be done
// the row is built from the player's max dash charges, so changing that number changes the bar
// put it on an empty object under the Canvas.
// the slots are made as its children
public class DashMeterHUD : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;

    // a white rounded rectangle picture, tinted by the colors below
    [SerializeField] private Sprite slotSprite;

    [Header("Look")]
    [SerializeField] private Vector2 slotSize = new Vector2(90f, 40f);
    [SerializeField] private float spacing = 14f;

    [SerializeField] private Color emptyColor = new Color(0.15f, 0.4f, 1f, 1f);
    [SerializeField] private Color chargingColor = new Color(0.25f, 0.9f, 0.3f, 1f);
    [SerializeField] private Color readyColor = new Color(0f, 0.85f, 0.8f, 1f);

    private Image[] backgrounds = new Image[0];
    private Image[] fills = new Image[0];

    private void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<CustomPlayerController>();
    }

    private void Update()
    {
        if (player == null || slotSprite == null)
            return;

        if (backgrounds.Length != player.MaxDashCharges)
            BuildSlots(player.MaxDashCharges);

        float charges = player.DashCharges;

        for (int i = 0; i < fills.Length; i++)
        {
            // how full this slot is, 0 to 1
            float amount = Mathf.Clamp01(charges - i);

            fills[i].fillAmount = amount;
            fills[i].color = amount >= 1f ? readyColor : chargingColor;
        }
    }

    private void BuildSlots(int count)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        backgrounds = new Image[count];
        fills = new Image[count];

        for (int i = 0; i < count; i++)
        {
            // the row starts at this object's left edge and grows to the right
            Image background = MakeImage("Slot " + (i + 1), transform, emptyColor);
            Place(background.rectTransform, new Vector2(slotSize.x * 0.5f + i * (slotSize.x + spacing), 0f));

            Image fill = MakeImage("Fill", background.transform, chargingColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            backgrounds[i] = background;
            fills[i] = fill;
        }
    }

    private Image MakeImage(string objectName, Transform parent, Color color)
    {
        GameObject slot = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        slot.transform.SetParent(parent, false);

        Image image = slot.GetComponent<Image>();
        image.sprite = slotSprite;
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private void Place(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = slotSize;
        rect.anchoredPosition = position;
    }
}
