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

    [Header("Spawn Position")]
    [Tooltip("How many metres in front of the player the text appears.")]
    [SerializeField] private float spawnDistance   = 2f;

    [Tooltip("Height offset above player pivot (positive = higher).")]
    [SerializeField] private float spawnHeightOffset = 0.2f;

    [Header("Float Settings")]
    [Tooltip("Units per second the text drifts upward while visible (only in relative-to-player mode).")]
    [SerializeField] private float floatSpeed = 0.08f;

    [Tooltip("Horizontal sway amplitude (only in relative-to-player mode).")]
    [SerializeField] private float swayAmplitude = 0.015f;

    [Header("Typewriter")]
    [Tooltip("Characters revealed per second. 20 = natural inner-voice pace. 50 = fast.")]
    [SerializeField] private float typewriterSpeed = 20f;

    [Header("Style — Narrator")]
    [Tooltip("Warm off-white — used for the external narrator voice. Pinned, large, no drift.")]
    [SerializeField] private Color narratorColor   = new Color(1f, 0.96f, 0.85f, 1f); // warm cream
    [SerializeField] private float narratorFontSize = 56f;

    [Header("Style — Discovery")]
    [SerializeField] private Color discoveryColor   = Color.white;
    [SerializeField] private float discoveryFontSize = 48f;  // TMP font size in points (canvas scale ~0.005)

    [Header("Style — Calming")]
    [SerializeField] private Color calmingColor   = new Color(0.75f, 0.88f, 1f, 1f);
    [SerializeField] private float calmingFontSize = 36f;

    // ─── Runtime State ────────────────────────────────────────────────────────
    private bool      _inUse;
    private Coroutine _showRoutine;
    private float     _swayOffset;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        // Auto-fetch missing references so the prefab works without manual Inspector wiring
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>();

        if (worldCanvas == null)
            worldCanvas = GetComponentInChildren<Canvas>();

        if (textMesh == null)
            textMesh = GetComponentInChildren<TextMeshProUGUI>();

        // Assign the event camera immediately — World Space canvas won't sort/render correctly without it
        if (worldCanvas != null && worldCanvas.worldCamera == null)
        {
            Camera main = Camera.main;
            if (main != null) worldCanvas.worldCamera = main;
        }
    }

    // ─── Pool Interface ───────────────────────────────────────────────────────

    /// <summary>True while this instance is showing text.</summary>
    public bool InUse => _inUse;

    /// <summary>
    /// Called by NarrativeManager to display this instance.
    /// Pass worldPosition to pin text to an exact spot; leave null for relative-to-camera spawn.
    /// </summary>
    public void Show(NarrativeEntry entry, Transform player, Camera cam, Vector3? worldPosition = null)
    {
        if (_showRoutine != null) StopCoroutine(_showRoutine);
        gameObject.SetActive(true); // Must activate BEFORE StartCoroutine — inactive objects can't run coroutines
        _showRoutine = StartCoroutine(ShowRoutine(entry, player, cam, worldPosition));
    }

    /// <summary>Force-hides this instance immediately and returns it to the pool.</summary>
    public void ForceHide()
    {
        if (_showRoutine != null) { StopCoroutine(_showRoutine); _showRoutine = null; }
        ReturnToPool();
    }

    // ─── Show Coroutine ───────────────────────────────────────────────────────

    IEnumerator ShowRoutine(NarrativeEntry entry, Transform player, Camera cam, Vector3? worldPosition = null)
    {
        _inUse = true;
        _swayOffset = Random.Range(0f, Mathf.PI * 2f);

        // ── Position ──────────────────────────────────────────────────────────
        if (worldPosition.HasValue)
        {
            // Exact anchor position set by the caller — no math needed
            transform.position = worldPosition.Value;
        }
        else
        {
            // Relative-to-camera: spawn in front of where the player is looking
            Vector3 lookDir = cam != null ? cam.transform.forward : player.forward;
            Vector3 forward = Vector3.ProjectOnPlane(lookDir, Vector3.up);

            // Safety: if camera is looking straight up/down, fall back to player forward
            if (forward.magnitude < 0.01f)
                forward = Vector3.ProjectOnPlane(player.forward, Vector3.up);

            forward = forward.normalized;
            Vector3 origin = cam != null ? cam.transform.position : player.position;
            transform.position = origin + forward * spawnDistance + Vector3.up * spawnHeightOffset;
        }

        // Apply style
        ApplyStyle(entry.style);

        // Narrator: text appears instantly so the voice delivers it — no typewriter.
        // Inner-voice styles: typewriter if the entry requests it.
        bool useTypewriter = entry.typewriterReveal && entry.style != NarrativeStyle.Narrator;

        // Set text
        if (useTypewriter)
            textMesh.text = "";
        else
            textMesh.text = entry.text;

        // Configure canvas (object is already active — activated in Show() before coroutine start)
        if (worldCanvas != null && cam != null)
            worldCanvas.worldCamera = cam;

        canvasGroup.alpha = 0f;

        // ── Fade in ──────────────────────────────────────────────────────────
        yield return StartCoroutine(FadeAlpha(0f, 1f, entry.fadeInTime));

        // ── Typewriter (inner-voice only) ─────────────────────────────────────
        if (useTypewriter)
            yield return StartCoroutine(TypewriterReveal(entry.text));

        // ── Hold ─────────────────────────────────────────────────────────────
        float holdTime = entry.displayDuration > 0f
            ? entry.displayDuration
            : (entry.narratorClip != null ? entry.narratorClip.length : 3f);

        // Narrator style and anchored entries are always pinned — no drift or sway.
        // Inner-voice entries with no anchor float freely upward.
        bool pinned = worldPosition.HasValue || entry.style == NarrativeStyle.Narrator;

        float elapsed = 0f;
        while (elapsed < holdTime)
        {
            elapsed += Time.deltaTime;

            if (!pinned)
            {
                // Billboard — always face camera
                if (cam != null)
                    transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                                     cam.transform.rotation * Vector3.up);

                // Float upward + gentle sway
                float sway = Mathf.Sin(Time.time * 0.6f + _swayOffset) * swayAmplitude;
                transform.position += new Vector3(sway, floatSpeed * Time.deltaTime, 0f);
            }

            yield return null;
        }

        // ── Fade out ─────────────────────────────────────────────────────────
        yield return StartCoroutine(FadeAlpha(1f, 0f, entry.fadeOutTime));

        ReturnToPool();
    }

    IEnumerator TypewriterReveal(string fullText)
    {
        if (typewriterSpeed <= 0f) { textMesh.text = fullText; yield break; }

        float delay = 1f / typewriterSpeed; // seconds per character

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
            case NarrativeStyle.Narrator:
                textMesh.color    = narratorColor;
                textMesh.fontSize = narratorFontSize;
                break;
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
