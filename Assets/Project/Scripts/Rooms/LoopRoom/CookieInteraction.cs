using UnityEngine;

/// <summary>
/// Attach to any food item in the kitchen (good OR bad food).
///
/// GOOD FOOD — The cookie keeps its original special mechanic:
///   reduces anxiety by a configurable number of levels AND disappears.
///   The first time ANY good food is eaten, the shared good-food floating text appears.
///   Any subsequent good food click still reduces anxiety but shows no text.
///
/// BAD FOOD — Increases anxiety by a fixed amount and disappears.
///   The first time ANY bad food is eaten, the shared bad-food floating text appears.
///   Any subsequent bad food click still adds anxiety but shows no text.
///
/// The floating text fires ONCE per food category per session (shared across all instances).
///
/// SETUP (do this for EACH food object):
///   1. Attach this script to the food mesh (must have a Collider on the Interactable layer).
///   2. Set 'isGoodFood' — checked for healthy, unchecked for junk.
///   3. Assign 'goodFoodEntry' and 'badFoodEntry' (same assets across all food objects).
///   4. Assign 'goodFoodTextAnchor' — an empty child GO positioned where the good-food text appears.
///   5. Assign 'badFoodTextAnchor'  — an empty child GO positioned where the bad-food text appears.
///      (Good and bad anchors are intentionally different positions.)
/// </summary>
public class CookieInteraction : MonoBehaviour, IInteractable
{
    // ─── Static shared-state — one flag for ALL food instances ───────────────
    // "Has the good-food floating text been shown yet this session?"
    private static bool s_goodFoodTextShown = false;
    // "Has the bad-food floating text been shown yet this session?"
    private static bool s_badFoodTextShown  = false;

    /// <summary>Call this on scene load or game restart to reset the session flags.</summary>
    public static void ResetSessionFlags()
    {
        s_goodFoodTextShown = false;
        s_badFoodTextShown  = false;
    }

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Food Type")]
    [Tooltip("True = healthy food (reduces anxiety). False = junk/bad food (increases anxiety).")]
    [SerializeField] private bool isGoodFood = false;

    [Header("Anxiety Settings")]
    [Tooltip("Good food: number of anxiety LEVELS to reduce (original cookie mechanic — ReduceOneLevel × N).")]
    [SerializeField] private int anxietyLevelsToReduce = 2;

    [Tooltip("Bad food: flat anxiety points ADDED.")]
    [SerializeField] private float anxietyToAdd = 10f;

    [Header("Floating Text — shared across all food instances")]
    [Tooltip("NarrativeEntry shown the FIRST TIME any good food is eaten. " +
             "Use the same asset on every good food object.")]
    [SerializeField] private NarrativeEntry goodFoodEntry;

    [Tooltip("NarrativeEntry shown the FIRST TIME any bad food is eaten. " +
             "Use the same asset on every bad food object.")]
    [SerializeField] private NarrativeEntry badFoodEntry;

    [Header("Text Anchors")]
    [Tooltip("World position where the GOOD-food reaction text appears. " +
             "Create an empty child GO, place it at the desired spot, and assign it here.")]
    [SerializeField] private Transform goodFoodTextAnchor;

    [Tooltip("World position where the BAD-food reaction text appears. " +
             "Intentionally a different position from the good-food anchor.")]
    [SerializeField] private Transform badFoodTextAnchor;

    // ─── Per-instance state ───────────────────────────────────────────────────
    private bool _eaten;

    // ─── IInteractable ────────────────────────────────────────────────────────

    public void Interact()
    {
        if (_eaten) return;

        // Only active while the expanding-room effect is running
        if (LoopRoomManager.Instance == null || !LoopRoomManager.Instance.IsActive)
        {
            Debug.Log("[FoodInteraction] Food clicked but kitchen effect is not active — ignoring.");
            return;
        }

        _eaten = true;

        if (isGoodFood)
        {
            // ── Original cookie mechanic: reduce anxiety by N levels ──────────
            if (AnxietyManager.Instance != null)
            {
                for (int i = 0; i < anxietyLevelsToReduce; i++)
                    AnxietyManager.Instance.ReduceOneLevel();
            }

            // ── Floating text — only on the FIRST good-food click in the session ──
            if (!s_goodFoodTextShown)
            {
                s_goodFoodTextShown = true;
                if (goodFoodEntry != null && NarrativeManager.Instance != null)
                    NarrativeManager.Instance.Show(goodFoodEntry, goodFoodTextAnchor);
                else if (goodFoodEntry == null)
                    Debug.LogWarning($"[FoodInteraction] goodFoodEntry not assigned on {gameObject.name}.");
            }

            Debug.Log($"[FoodInteraction] Good food '{gameObject.name}' eaten. " +
                      $"Anxiety reduced by {anxietyLevelsToReduce} levels. " +
                      $"Text shown: {s_goodFoodTextShown}.");
        }
        else
        {
            // ── Add anxiety ───────────────────────────────────────────────────
            if (AnxietyManager.Instance != null)
                AnxietyManager.Instance.AddAnxiety(anxietyToAdd);

            // ── Floating text — only on the FIRST bad-food click in the session ──
            if (!s_badFoodTextShown)
            {
                s_badFoodTextShown = true;
                if (badFoodEntry != null && NarrativeManager.Instance != null)
                    NarrativeManager.Instance.Show(badFoodEntry, badFoodTextAnchor);
                else if (badFoodEntry == null)
                    Debug.LogWarning($"[FoodInteraction] badFoodEntry not assigned on {gameObject.name}.");
            }

            Debug.Log($"[FoodInteraction] Bad food '{gameObject.name}' eaten. " +
                      $"Anxiety +{anxietyToAdd}. " +
                      $"Text shown: {s_badFoodTextShown}.");
        }

        // Food disappears after being eaten (original behaviour)
        gameObject.SetActive(false);
    }

    public string GetPromptText()
    {
        if (_eaten) return "";
        return "Eat Cookie";
    }
}
