using TMPro;
using UnityEngine;

// types a text out letter by letter when it appears.
// put it on a TextMesh Pro text inside a room's Tutorial UI prefab.
// every text can have its own speed and delay, so a message can build up piece by piece
[RequireComponent(typeof(TMP_Text))]
public class TypewriterText : MonoBehaviour
{
    // how many letters appear each second
    [Min(1f)]
    [SerializeField] private float charactersPerSecond = 30f;

    // how long to wait before the first letter (seconds)
    [Min(0f)]
    [SerializeField] private float startDelaySeconds = 0f;

    private TMP_Text label;
    private int totalCharacters;

    // how far the typing has got (a fraction of a letter counts, so the speed is smooth).
    // it starts below zero during the delay
    private float typed;

    private void OnEnable()
    {
        label = GetComponent<TMP_Text>();

        // the whole text is laid out first, so the lines don't jump around while it types
        label.maxVisibleCharacters = 0;
        label.ForceMeshUpdate();

        totalCharacters = label.textInfo.characterCount;
        typed = -startDelaySeconds * charactersPerSecond;
    }

    private void Update()
    {
        if (typed >= totalCharacters)
            return;

        typed += charactersPerSecond * Time.unscaledDeltaTime;
        label.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(typed), 0, totalCharacters);
    }
}
