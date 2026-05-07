using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that manages contextual objective hints displayed in the screen corner.
///
/// Hints slide in smoothly from off-screen, stay visible, then slide out when
/// replaced or cleared. Only one hint is visible at a time.
///
/// USAGE:
///   ObjectiveHintManager.Instance.ShowHint("The phone is ringing — pick it up");
///   ObjectiveHintManager.Instance.ShowHint("Find another way", duration: 8f);
///   ObjectiveHintManager.Instance.ClearHint();
///
/// SETUP:
///   1. Create a Canvas (Screen Space — Overlay, sort order above gameplay).
///   2. Create a child Panel anchored to the top-right corner with:
///        - Image (semi-transparent dark background)
///        - TextMeshProUGUI for hint text
///        - Optional: a small icon Image
///   3. Attach this script to the Canvas or a manager object.
///   4. Assign 'hintPanel', 'hintText', and optionally 'hintIcon'.
///   5. Set 'slideDistance' to match the panel width so it starts off-screen.
/// </summary>
public class ObjectiveHintManager : MonoBehaviour
{
    public static ObjectiveHintManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("The RectTransform of the hint panel (the sliding container).")]
    [SerializeField] private RectTransform hintPanel;
    [SerializeField] private TextMeshProUGUI hintText;
    [Tooltip("Optional icon image shown next to the hint text.")]
    [SerializeField] private Image hintIcon;
    [Tooltip("Optional sprite to show on the icon.")]
    [SerializeField] private Sprite defaultHintIcon;

    [Header("Animation")]
    [Tooltip("How far (in UI pixels) the panel slides from off-screen. Should match panel width.")]
    [SerializeField] private float slideDistance = 350f;
    [Tooltip("Seconds for slide-in animation.")]
    [SerializeField] private float slideInDuration = 0.4f;
    [Tooltip("Seconds for slide-out animation.")]
    [SerializeField] private float slideOutDuration = 0.3f;

    [Header("Anchor Side")]
    [Tooltip("If true, hint slides in from the right. If false, from the left.")]
    [SerializeField] private bool anchorRight = true;

    // ─── State ────────────────────────────────────────────────────────────────
    private bool      _isVisible;
    private Coroutine _hintRoutine;
    private Vector2   _shownPosition;
    private Vector2   _hiddenPosition;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ComputePositions();
        if (hintPanel != null)
            hintPanel.anchoredPosition = _hiddenPosition;

        if (hintIcon != null && defaultHintIcon != null)
            hintIcon.sprite = defaultHintIcon;
    }

    void ComputePositions()
    {
        _shownPosition  = Vector2.zero;
        _hiddenPosition = anchorRight
            ? new Vector2(slideDistance, 0f)   // slides in from right
            : new Vector2(-slideDistance, 0f); // slides in from left
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Show a hint. Replaces any existing hint gracefully.
    /// duration = 0 means the hint stays until replaced or manually cleared.
    /// </summary>
    public void ShowHint(string text, float duration = 0f)
    {
        if (_hintRoutine != null) StopCoroutine(_hintRoutine);
        _hintRoutine = StartCoroutine(HintRoutine(text, duration));
    }

    /// <summary>Slide the current hint out and hide it.</summary>
    public void ClearHint()
    {
        if (_hintRoutine != null) StopCoroutine(_hintRoutine);
        if (_isVisible)
            _hintRoutine = StartCoroutine(SlideOut());
    }

    // ─── Coroutines ───────────────────────────────────────────────────────────

    IEnumerator HintRoutine(string text, float duration)
    {
        // If already visible, slide out first (quick)
        if (_isVisible)
            yield return StartCoroutine(SlideOut());

        // Update text
        if (hintText != null) hintText.text = text;

        // Slide in
        yield return StartCoroutine(SlideIn());

        // Hold
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
            yield return StartCoroutine(SlideOut());
        }
        // If duration = 0, stay visible until ClearHint() or next ShowHint()
    }

    IEnumerator SlideIn()
    {
        _isVisible = true;
        yield return StartCoroutine(AnimatePanel(_hiddenPosition, _shownPosition, slideInDuration));
    }

    IEnumerator SlideOut()
    {
        _isVisible = false;
        yield return StartCoroutine(AnimatePanel(_shownPosition, _hiddenPosition, slideOutDuration));
    }

    IEnumerator AnimatePanel(Vector2 from, Vector2 to, float duration)
    {
        if (hintPanel == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            hintPanel.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        hintPanel.anchoredPosition = to;
    }

    // ─── Editor Helpers ───────────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Test: Show Hint")]
    void TestShow() => ShowHint("The phone is ringing — pick it up");

    [ContextMenu("Test: Show Timed Hint (5s)")]
    void TestTimedShow() => ShowHint("Look around the room", 5f);

    [ContextMenu("Test: Clear Hint")]
    void TestClear() => ClearHint();
#endif
}
