using UnityEngine;

/// <summary>
/// Attach to the kitchen cookie GameObject.
/// When the player clicks on the cookie while the expanding-room effect is active,
/// the cookie disappears and reduces anxiety by two levels.
///
/// SETUP:
///   1. Attach to the cookie mesh in the kitchen.
///   2. Ensure the cookie has a Collider and is on the Interactable layer.
///   3. This script works independently — no other references needed.
/// </summary>
public class CookieInteraction : MonoBehaviour, IInteractable
{
    [Header("Anxiety Settings")]
    [Tooltip("Number of anxiety levels to reduce when the cookie is eaten. Default is 2.")]
    [SerializeField] private int anxietyLevelsToReduce = 2;

    private bool _eaten;

    public void Interact()
    {
        if (_eaten) return;

        // Only works while the expanding-room effect is active (player is in the kitchen loop)
        if (LoopRoomManager.Instance == null || !LoopRoomManager.Instance.IsActive)
        {
            Debug.Log("[CookieInteraction] Cookie clicked but kitchen effect is not active — ignoring.");
            return;
        }

        _eaten = true;

        // Reduce anxiety by two levels
        if (AnxietyManager.Instance != null)
        {
            for (int i = 0; i < anxietyLevelsToReduce; i++)
                AnxietyManager.Instance.ReduceOneLevel();
        }

        Debug.Log("[CookieInteraction] Cookie eaten! Anxiety reduced by 2 levels.");

        // Disappear
        gameObject.SetActive(false);
    }

    public string GetPromptText() => "Eat Cookie";
}
