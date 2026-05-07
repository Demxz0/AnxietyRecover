using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a hand-drawn icon floating in world-space above/near an interactable object
/// when the player gets close. Edith Finch style proximity indicator.
///
/// This script is SELF-CONTAINED — it checks its own distance to the player every frame.
/// No changes are required to PlayerInteraction.
///
/// SETUP:
///   1. Attach to any interactable GameObject (or a child of it).
///   2. Create a child GameObject "HandIconCanvas":
///        - Add a Canvas component, set renderMode = WorldSpace.
///        - Scale it to ~0.002 x 0.002 x 0.002.
///        - Add an Image child — assign your hand-drawn sprite.
///        - Add a CanvasGroup to the canvas root.
///   3. Assign 'handIconCanvas', 'canvasGroup', and optionally 'handImage'.
///   4. Assign 'handSprite' (your hand-drawn texture imported as Sprite).
///   5. Set 'showRadius' (default 4.5m).
///
/// The icon disappears permanently after the player interacts with the object.
/// Call Hide() from the interaction script's Interact() method.
/// </summary>
public class InteractableHandIcon : MonoBehaviour
{
    [Header("Icon References")]
    [Tooltip("The world-space Canvas containing the hand icon.")]
    [SerializeField] private Canvas handIconCanvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Optional Image to assign the hand sprite to at runtime.")]
    [SerializeField] private Image handImage;
    [Tooltip("The hand-drawn sprite to display.")]
    [SerializeField] private Sprite handSprite;

    [Header("Proximity")]
    [Tooltip("Distance (meters) at which the hand icon becomes visible.")]
    [SerializeField] private float showRadius = 4.5f;

    [Header("Bob Animation")]
    [Tooltip("Vertical bob amplitude in world units.")]
    [SerializeField] private float bobAmplitude = 0.06f;
    [Tooltip("Cycles per second for the bob.")]
    [SerializeField] private float bobFrequency = 1.2f;

    [Header("Fade Timing")]
    [SerializeField] private float fadeInDuration  = 0.4f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    // ─── State ────────────────────────────────────────────────────────────────
    private Transform _player;
    private Camera    _cam;
    private bool      _visible;
    private bool      _permanentlyHidden;
    private float     _currentAlpha;
    private Vector3   _baseLocalPosition;
    private Coroutine _fadeRoutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (handImage != null && handSprite != null)
            handImage.sprite = handSprite;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            _currentAlpha    = 0f;
        }

        if (handIconCanvas != null)
            _baseLocalPosition = handIconCanvas.transform.localPosition;
    }

    void Start()
    {
        // Find player — try Player tag first, then Camera.main parent
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        _player = playerGO != null ? playerGO.transform : null;
        _cam    = Camera.main;
    }

    void Update()
    {
        if (_permanentlyHidden || _player == null || canvasGroup == null) return;

        float dist = Vector3.Distance(transform.position, _player.position);
        bool shouldShow = dist <= showRadius;

        if (shouldShow && !_visible)
        {
            _visible = true;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeTo(1f, fadeInDuration));
        }
        else if (!shouldShow && _visible)
        {
            _visible = false;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeTo(0f, fadeOutDuration));
        }

        // Billboard — always face camera
        if (_cam != null && handIconCanvas != null)
        {
            handIconCanvas.transform.rotation = _cam.transform.rotation;
        }

        // Bob animation
        if (_currentAlpha > 0f && handIconCanvas != null)
        {
            float bobOffset = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            handIconCanvas.transform.localPosition = _baseLocalPosition + Vector3.up * bobOffset;
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from the object's Interact() method to permanently hide the icon.
    /// </summary>
    public void Hide()
    {
        _permanentlyHidden = true;
        _visible           = false;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        StartCoroutine(FadeTo(0f, fadeOutDuration));
    }

    // ─── Fade ─────────────────────────────────────────────────────────────────

    IEnumerator FadeTo(float target, float duration)
    {
        float start   = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed         += Time.deltaTime;
            _currentAlpha    = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            canvasGroup.alpha = _currentAlpha;
            yield return null;
        }
        _currentAlpha    = target;
        canvasGroup.alpha = target;
    }

    // ─── Gizmo ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, showRadius);
    }

    [ContextMenu("Test: Force Show")]
    void TestShow()
    {
        _permanentlyHidden = false;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    [ContextMenu("Test: Force Hide")]
    void TestHide()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }
#endif
}
