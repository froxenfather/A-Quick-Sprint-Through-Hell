using UnityEngine;

// lets the first-person camera sit inside the stick figure's head without seeing the inside of it
// put this on the Player.
// the head (and everything on it) is shrunk to nothing each frame, so the arms, torso and legs stay visible
// when you look down, and the animations still play on the rest of the body
public class FirstPersonBody : MonoBehaviour
{
    // found by name under the Player if left empty
    [SerializeField] private Transform headBone;

    [SerializeField] private string headBoneName = "Head";

    // turn this off to see the head again (for a third-person test)
    [SerializeField] private bool hideHead = true;

    private void Awake()
    {
        if (headBone != null)
            return;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == headBoneName)
            {
                headBone = child;
                break;
            }
        }

        if (headBone == null)
            Debug.LogWarning($"FirstPersonBody could not find a bone called '{headBoneName}' under the Player.", this);
    }

    // after the Animator has posed the body, so our scale wins this frame
    private void LateUpdate()
    {
        if (headBone == null)
            return;

        headBone.localScale = hideHead ? Vector3.zero : Vector3.one;
    }
}
