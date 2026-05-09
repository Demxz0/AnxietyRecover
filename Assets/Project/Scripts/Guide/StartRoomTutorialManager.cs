using System.Collections;
using UnityEngine;

/// <summary>
/// Shows a short floating inner-monologue text about breathing in the start room.
/// Fires automatically a few seconds after the game starts — no voice, no interaction needed.
///
/// HOW TO SET UP IN THE EDITOR:
///   1. Create an empty GameObject in the start room, name it "StartRoomTutorialManager".
///   2. Attach this script.
///   3. NarrativeManager must be set up in the scene (existing system).
///   4. Adjust 'autoStartDelay' and the text in the Inspector if needed.
///
/// The text appears as a floating world-space monologue using the existing
/// NarrativeManager pool — same visual style as all in-game narrative text.
/// </summary>
public class StartRoomTutorialManager : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds to wait after the game starts before the breathing text appears.")]
    [SerializeField] private float autoStartDelay = 5f;

    [Header("Breathing Monologue Text")]
    [TextArea(3, 6)]
    [Tooltip("The floating inner-voice text explaining the breathing mechanic.")]
    [SerializeField] private string breathingText =
        "التنفس... أحتاج أن أتذكر كيف أتنفس.\n" +
        "اضغط مع الاستمرار على F للشهيق، ثم G للزفير.\n" +
        "أكمل الدورة لتهدئة نفسي.";

    [Tooltip("How long (seconds) the text stays visible on screen.")]
    [SerializeField] private float displayDuration = 8f;

    [Tooltip("Seconds for the text to fade in.")]
    [SerializeField] private float fadeInTime = 1f;

    [Tooltip("Seconds for the text to fade out.")]
    [SerializeField] private float fadeOutTime = 1f;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _hasShown;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        StartCoroutine(AutoShowRoutine());
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    IEnumerator AutoShowRoutine()
    {
        yield return new WaitForSeconds(autoStartDelay);
        ShowBreathingMonologue();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Show the breathing monologue immediately.
    /// Safe to call manually — fires only once.
    /// </summary>
    public void ShowBreathingMonologue()
    {
        if (_hasShown) return;
        _hasShown = true;

        if (NarrativeManager.Instance == null)
        {
            Debug.LogWarning("[StartRoomTutorial] NarrativeManager not found — breathing text skipped.");
            return;
        }

        // Build a temporary NarrativeEntry — no ScriptableObject needed
        NarrativeEntry entry = ScriptableObject.CreateInstance<NarrativeEntry>();
        entry.text            = breathingText;
        entry.displayDuration = displayDuration;
        entry.fadeInTime      = fadeInTime;
        entry.fadeOutTime     = fadeOutTime;
        entry.narratorClip    = null;
        entry.waitForNarrator = false;
        entry.style           = NarrativeStyle.Calming; // soft blue/white — fits a quiet thought

        NarrativeManager.Instance.Show(entry);

        // Clean up the temporary asset after it would have finished
        StartCoroutine(DestroyEntryAfterDelay(entry, fadeInTime + displayDuration + fadeOutTime + 1f));

        Debug.Log("[StartRoomTutorial] Breathing monologue shown.");
    }

    IEnumerator DestroyEntryAfterDelay(NarrativeEntry entry, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (entry != null) Destroy(entry);
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Show Breathing Monologue Now")]
    void TestShow()
    {
        _hasShown = false;
        ShowBreathingMonologue();
    }
#endif
}
