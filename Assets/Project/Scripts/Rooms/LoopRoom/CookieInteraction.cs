using UnityEngine;

/// <summary>
/// Attach to any cookie / food item in the kitchen.
/// When eaten, reduces anxiety by exactly one level and disappears.
/// </summary>
public class CookieInteraction : MonoBehaviour, IInteractable
{
    private static bool s_textShown = false;

    /// <summary>Call this on scene load or game restart to reset the session flags.</summary>
    public static void ResetSessionFlags()
    {
        s_textShown = false;
    }

    [Header("Anxiety Settings")]
    [Tooltip("Number of anxiety levels to reduce when eaten.")]
    [SerializeField] private int anxietyLevelsToReduce = 1;

    [Header("Floating Text")]
    [Tooltip("NarrativeEntry shown the FIRST TIME a cookie is eaten.")]
    [SerializeField] private NarrativeEntry eatCookieEntry;

    [Tooltip("World position where the reaction text appears.")]
    [SerializeField] private Transform textAnchor;

    private bool _eaten;

    public void Interact()
    {
        if (_eaten) return;

        // Only active while the expanding-room effect is running
        if (LoopRoomManager.Instance == null || !LoopRoomManager.Instance.IsActive)
        {
            Debug.Log("[CookieInteraction] Cookie clicked but kitchen effect is not active — ignoring.");
            return;
        }

        _eaten = true;

        // Reduce anxiety by one level (or specified levels)
        if (AnxietyManager.Instance != null)
        {
            for (int i = 0; i < anxietyLevelsToReduce; i++)
            {
                AnxietyManager.Instance.ReduceOneLevel();
            }
        }

        // Floating text — only on the FIRST cookie click in the session
        if (!s_textShown)
        {
            s_textShown = true;
            if (eatCookieEntry != null && NarrativeManager.Instance != null)
            {
                NarrativeManager.Instance.Show(eatCookieEntry, textAnchor);
            }
        }

        Debug.Log($"[CookieInteraction] Cookie '{gameObject.name}' eaten. Anxiety reduced by {anxietyLevelsToReduce} level(s).");

        // Food disappears after being eaten
        gameObject.SetActive(false);
    }

    public string GetPromptText()
    {
        if (_eaten) return "";
        return "Eat Cookie";
    }
}
