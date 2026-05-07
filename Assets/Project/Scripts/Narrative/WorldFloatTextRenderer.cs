using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// A single pooled world-space text instance.
///
/// Spawns ~2m in front of the player at eye level.
/// Always faces the camera (billboard).
/// Floats slowly upward while visible, then fades out.
/// Returned to the pool by NarrativeManager when done.
///
/// SETUP (prefab):
///   1. Create an empty GameObject "WorldFloatText".
///   2. Add a World Space Canvas (renderMode = WorldSpace, pixelPerfect = false).
///      Scale the Canvas to something like 0.005 x 0.005 x 0.005 so it's not enormous.
///   3. Add a TextMeshProUGUI child on the canvas — configure font, alignment = center.
///   4. Attach this script to the root GameObject.
///   5. Assign 'textMesh' in the Inspector.
///   6. Set the prefab's Canvas camera to the Main Camera (or leave null — set at runtime).
/// </summary>
public class WorldFloatTextRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI textMesh;
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Float Settings")]
    [Tooltip("Units per second the text drifts upward while visible.")]
    [SerializeField] private float floatSpeed = 0.08f;

    [Tooltip("Horizontal sway amplitude (adds gentle life to the float).")]
    [SerializeField] private float swayAmplitude = 0.015f;

    [Header("Style — Discovery")]
    [SerializeField] private Color discoveryColor   = Color.white;
    [SerializeField] private float discoveryFontSize = 2.8f;

    [Header("Style — Calming")]
    [SerializeField] private Color calmingColor   = new Color(0.75f, 0.88f, 1f, 1f);
    [SerializeField] private float calmingFontSize = 2.2f;

    // ─── Runtime State ────────────────────────────────────────────────────────
    private bool      _inUse;
    private Coroutine _showRoutine;
    private float     _swayOffset;

    // ─── Pool Interface ───────────────────────────────────────────────────────

    /// <summary>True while this instance is showing text.</summary>
    public bool InUse => _inUse;

    /// <summary>
    /// Called by NarrativeManager to display this instance.
    /// </summary>
    public void Show(NarrativeEntry entry, Transform player, Camera cam)
    {
        if (_showRoutine != null) StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(ShowRoutine(entry, player, cam));
    }

    /// <summary>Force-hides this instance immediately and returns it to the pool.</summary>
    public void ForceHide()
    {
        if (_showRoutine != null) { StopCoroutine(_showRoutine); _showRoutine = null; }
        ReturnToPool();
    }

    // ─── Show Coroutine ───────────────────────────────────────────────────────

    IEnumerator ShowRoutine(NarrativeEntry entry, Transform player, Camera cam)
    {
        _inUse = true;
        _swayOffset = Random.Range(0f, Mathf.PI * 2f); // randomize sway phase

        // Position: 2m in front of player at eye level
        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        transform.position = player.position + forward * 2f + Vector3.up * 0.2f;

        // Apply style
        ApplyStyle(entry.style);

        // Set text
        if (entry.typewriterReveal)
            textMesh.text = "";
        else
            textMesh.text = entry.text;

        // Make canvas visible
        gameObject.SetActive(true);
        if (worldCanvas != null && cam != null)
            worldCanvas.worldCamera = cam;

        canvasGroup.alpha = 0f;

        // ── Fade in ──────────────────────────────────────────────────────────
        yield return StartCoroutine(FadeAlpha(0f, 1f, entry.fadeInTime));

        // ── Typewriter ───────────────────────────────────────────────────────
        if (entry.typewriterReveal)
            yield return StartCoroutine(TypewriterReveal(entry.text, entry.displayDuration));

        // ── Hold (float upward) ──────────────────────────────────────────────
        float holdTime = entry.displayDuration > 0f
            ? entry.displayDuration
            : (entry.narratorClip != null ? entry.narratorClip.length : 3f);

        float elapsed = 0f;
        while (elapsed < holdTime)
        {
            elapsed += Time.deltaTime;

            // Billboard — always face camera
            if (cam != null)
                transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                                 cam.transform.rotation * Vector3.up);

            // Float upward + gentle sway
            float sway = Mathf.Sin(Time.time * 0.6f + _swayOffset) * swayAmplitude;
            transform.position += new Vector3(sway, floatSpeed * Time.deltaTime, 0f);

            yield return null;
        }

        // ── Fade out ─────────────────────────────────────────────────────────
        yield return StartCoroutine(FadeAlpha(1f, 0f, entry.fadeOutTime));

        ReturnToPool();
    }

    IEnumerator TypewriterReveal(string fullText, float holdDuration)
    {
        // Calculate per-character delay so the whole text is revealed in 40% of display time
        float revealTime = holdDuration * 0.4f;
        float delay = fullText.Length > 0 ? revealTime / fullText.Length : 0.05f;

        for (int i = 0; i <= fullText.Length; i++)
        {
            textMesh.text = fullText.Substring(0, i);
            yield return new WaitForSeconds(delay);
        }
    }

    IEnumerator FadeAlpha(float from, float to, float duration)
    {
        if (duration <= 0f) { canvasGroup.alpha = to; yield break; }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    void ApplyStyle(NarrativeStyle style)
    {
        switch (style)
        {
            case NarrativeStyle.Discovery:
                textMesh.color    = discoveryColor;
                textMesh.fontSize = discoveryFontSize;
                break;
            case NarrativeStyle.Calming:
                textMesh.color    = calmingColor;
                textMesh.fontSize = calmingFontSize;
                break;
        }
    }

    void ReturnToPool()
    {
        _inUse = false;
        textMesh.text = "";
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        _showRoutine = null;
    }
}
