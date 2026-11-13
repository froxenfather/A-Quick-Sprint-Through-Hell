using TMPro;
using UnityEngine;

// shows "Rooms: N", how many rooms the player has gone through.
// the text starts black and fades to bright red over fullRedRoom (100) rooms
// (hue stays red, saturation and value both go from 0 to 1)
public class RoomCounterHUD : MonoBehaviour
{
    [SerializeField] private RoomChain roomChain;
    [SerializeField] private TMP_Text text;

    // the room count at which the number is fully bright red
    // (also the room where the rooms are meant to switch to the outline look)
    [Min(1)]
    [SerializeField] private int fullRedRoom = 100;

    [Header("Outline")]
    [SerializeField] private Color outlineColor = Color.white;

    // 0 is no outline, 1 is the thickest the font allows.
    // the outline is drawn by the font's material, so it only shows in Play mode
    [Range(0f, 1f)]
    [SerializeField] private float outlineThickness = 0.2f;

    private void Awake()
    {
        if (roomChain == null)
            roomChain = FindFirstObjectByType<RoomChain>();

        if (text != null)
        {
            // fontMaterial is this text's own copy of the font material, so other texts that use the same font don't get the outline
            text.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);

            text.outlineColor = outlineColor;
            text.outlineWidth = outlineThickness;
        }
    }

    private void Update()
    {
        if (roomChain == null || text == null)
            return;

        int rooms = roomChain.RoomsPassed;
        float t = Mathf.Clamp01(rooms / (float)fullRedRoom);

        text.text = "Rooms: " + rooms;
        text.color = Color.HSVToRGB(0f, t, t);
    }
}
