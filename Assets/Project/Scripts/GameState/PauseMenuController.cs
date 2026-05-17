using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pause Menu — activated by pressing Escape.
///
/// CANVAS SETUP:
///   1. Create a child Canvas (or use existing one) — name it "PauseCanvas".
///   2. Add a CanvasGroup to PauseCanvas.
///   3. Add a full-screen Image child → Color (0,0,0,0.75).
///   4. Add a "RevealMask" empty child RectTransform — this slides from left to right
///      to create the gradient reveal. Stretch it to full height, start width = 0.
///   5. Place your buttons OUTSIDE or ON TOP of the RevealMask, anchored to right side.
///      - Button "Continue" → wire OnClick to ResumGame()
///      - Button "Quit"     → wire OnClick to QuitGame()
///   6. Attach this script to PauseCanvas. Assign all fields in Inspector.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("CanvasGroup on the root pause canvas — controls overall visibility and interactability.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("RectTransform that sweeps left-to-right to reveal the panel. " +
             "Anchor: left-stretch. Start with Width = 0. Full width = screen width.")]
    [SerializeField] private RectTransform revealMask;

    [Tooltip("Buttons container — shown/hidden with the canvas.")]
    [SerializeField] private GameObject buttonsContainer;

    [Header("Animation")]
    [Tooltip("How long the left-to-right reveal sweep takes (seconds).")]
    [SerializeField] private float revealDuration = 0.4f;

    // ── State ───────────────────────────────────────────────────────────────
    private bool _isPaused = false;
    private Coroutine _animRoutine;
    private float _screenWidth;
    private PlayerInputActions _input;

    void Awake()
    {
        _screenWidth = Screen.width;
        _input = new PlayerInputActions();

        // Start fully hidden
        SetVisibility(false, instant: true);
    }

    void OnEnable()  => _input.Enable();
    void OnDisable() => _input.Disable();

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Toggle();
    }

    // ── Public Button Hooks ─────────────────────────────────────────────────

    public void ResumeGame()
    {
        Close();
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Debug.Log("[PauseMenu] Quit.");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ── Internal ────────────────────────────────────────────────────────────

    void Toggle()
    {
        if (_isPaused) Close();
        else           Open();
    }

    void Open()
    {
        _isPaused = true;
        Time.timeScale = 0f;

        UIInputMode.Enter();

        if (canvasGroup != null)
        {
            canvasGroup.interactable   = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (buttonsContainer != null) buttonsContainer.SetActive(true);

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(RevealRoutine(opening: true));
    }

    void Close()
    {
        _isPaused = false;

        if (canvasGroup != null)
        {
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(RevealRoutine(opening: false));

        Time.timeScale = 1f;
        UIInputMode.Exit();
    }

    /// <summary>
    /// Animates the CanvasGroup alpha AND sweeps the revealMask width
    /// from 0 → full (open) or full → 0 (close).
    /// </summary>
    IEnumerator RevealRoutine(bool opening)
    {
        float startAlpha = opening ? 0f : 1f;
        float endAlpha   = opening ? 1f : 0f;

        float startWidth = opening ? 0f : _screenWidth;
        float endWidth   = opening ? _screenWidth : 0f;

        float elapsed = 0f;

        // Use unscaled time so animation works even when timeScale = 0
        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / revealDuration));

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);

            if (revealMask != null)
            {
                Vector2 size = revealMask.sizeDelta;
                size.x = Mathf.Lerp(startWidth, endWidth, t);
                revealMask.sizeDelta = size;
            }

            yield return null;
        }

        // Snap to final values
        if (canvasGroup != null)  canvasGroup.alpha = endAlpha;

        if (revealMask != null)
        {
            Vector2 size = revealMask.sizeDelta;
            size.x = endWidth;
            revealMask.sizeDelta = size;
        }

        // After close animation finishes, hide buttons
        if (!opening && buttonsContainer != null)
            buttonsContainer.SetActive(false);
    }

    void SetVisibility(bool visible, bool instant = false)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha          = visible ? 1f : 0f;
            canvasGroup.interactable   = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (revealMask != null)
        {
            Vector2 size = revealMask.sizeDelta;
            size.x = visible ? _screenWidth : 0f;
            revealMask.sizeDelta = size;
        }

        if (buttonsContainer != null) buttonsContainer.SetActive(visible);
    }
}
