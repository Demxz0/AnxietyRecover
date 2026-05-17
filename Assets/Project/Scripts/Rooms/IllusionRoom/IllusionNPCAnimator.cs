using UnityEngine;

/// <summary>
/// Attach to each NPC character inside the Silhouettes parent in the Illusion Room.
/// When the parent GameObject is enabled by IllusionRoomManager, this component
/// immediately plays the configured animation state on the Animator.
///
/// SETUP IN EDITOR:
///   1. Select each NPC under the Silhouettes parent.
///   2. Add this component.
///   3. Open Window → Animation → Animator to find the exact state names.
///   4. Type the desired state name into "Animation State Name" field.
///      Example values (REPLACE WITH YOUR ACTUAL STATE NAMES):
///        "Idle"  — for static standing characters
///        "Talk"  — for characters that should appear to be talking
///
/// NOTE: Uses OnEnable() not Start(), because the parent starts disabled.
/// </summary>
public class IllusionNPCAnimator : MonoBehaviour
{
    [Tooltip("The exact name of the Animator state to play.\n" +
             "Check Window → Animation → Animator for your state names.\n" +
             "PLACEHOLDER — replace with your actual state name.")]
    [SerializeField] private string animationStateName = "Idle"; // <-- REPLACE WITH ACTUAL STATE NAME

    [Tooltip("Layer index in the Animator controller (usually 0 = Base Layer).")]
    [SerializeField] private int animatorLayerIndex = 0;

    private Animator _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        if (_animator == null)
            Debug.LogWarning($"[IllusionNPCAnimator] No Animator found on {gameObject.name}. " +
                             "Add an Animator component with a controller assigned.");
    }

    // OnEnable fires every time the silhouettes parent is enabled by IllusionRoomManager.
    void OnEnable()
    {
        PlayAnimation();
    }

    void PlayAnimation()
    {
        if (_animator == null) return;

        if (string.IsNullOrEmpty(animationStateName))
        {
            Debug.LogWarning($"[IllusionNPCAnimator] ({gameObject.name}) animationStateName is empty. " +
                             "Set it in the Inspector to the desired Animator state name.");
            return;
        }

        _animator.Play(animationStateName, animatorLayerIndex);
        Debug.Log($"[IllusionNPCAnimator] ({gameObject.name}) Playing state: '{animationStateName}'");
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Play Animation Now")]
    void TestPlay() => PlayAnimation();
#endif
}
