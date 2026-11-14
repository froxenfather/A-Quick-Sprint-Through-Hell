using TMPro;
using UnityEngine;

// shows the player's flat speed (m/s) as a number.
// from growStartSpeed up, the number gets bigger the faster we go, up to maxScale times its normal size
// put it on any object under the Canvas and give it the number text.
// the "SPEED:" picture is just an Image next to it
public class SpeedHUD : MonoBehaviour
{
    [SerializeField] private CustomPlayerController player;
    [SerializeField] private TMP_Text numberText;

    [Header("Growing")]
    // below this speed (m/s) the number stays its normal size
    [SerializeField] private float growStartSpeed = 30f;

    // at this speed the number is maxScale times bigger, and it never grows past that
    [SerializeField] private float maxScaleSpeed = 100f;

    // the biggest the number gets, as a multiple of its normal size
    [SerializeField] private float maxScale = 2f;

    // how quickly the size catches up with the speed.
    // higher = snappier, 0 = the size never changes
    [SerializeField] private float scaleSmoothing = 12f;

    private float currentScale = 1f;

    private void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<CustomPlayerController>();
    }

    private void Update()
    {
        if (player == null || numberText == null)
            return;

        float speed = player.HorizontalSpeed;
        numberText.text = Mathf.RoundToInt(speed).ToString();

        // 1x at growStartSpeed, maxScale at maxScaleSpeed, straight line in between
        float t = Mathf.InverseLerp(growStartSpeed, maxScaleSpeed, speed);
        float targetScale = Mathf.Lerp(1f, maxScale, t);

        currentScale = Mathf.Lerp(currentScale, targetScale, scaleSmoothing * Time.unscaledDeltaTime);

        numberText.rectTransform.localScale = Vector3.one * currentScale;
    }
}
