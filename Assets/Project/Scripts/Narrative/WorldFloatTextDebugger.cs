using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TEMPORARY DEBUG — attach this to the WorldFloatText prefab root.
/// Remove once the text is visible and working correctly.
///
/// What it checks every second while active:
///   - CanvasGroup alpha
///   - Canvas render mode + camera
///   - Canvas scale
///   - TextMeshPro text, font size, color, rect size
///   - Whether the object is actually in front of the camera
/// </summary>
[DisallowMultipleComponent]
public class WorldFloatTextDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("How often (seconds) to print the status report.")]
    [SerializeField] private float reportInterval = 1f;

    [Tooltip("Draw a visible gizmo sphere at this object's position in Scene view.")]
    [SerializeField] private bool drawGizmo = true;

    // ── cached refs ──────────────────────────────────────────────────────────
    private Canvas       _canvas;
    private CanvasGroup  _canvasGroup;
    private TextMeshProUGUI _textMesh;
    private RectTransform   _canvasRect;

    void Awake()
    {
        _canvas      = GetComponentInChildren<Canvas>();
        _canvasGroup = GetComponentInChildren<CanvasGroup>();
        _textMesh    = GetComponentInChildren<TextMeshProUGUI>();
        if (_canvas != null) _canvasRect = _canvas.GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        // Print immediately when the object wakes up
        StartCoroutine(ReportLoop());
        Log("━━━ WorldFloatTextDebugger ENABLED ━━━");
        PrintFullReport();
    }

    IEnumerator ReportLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(reportInterval);
            PrintFullReport();
        }
    }

    // ── main report ──────────────────────────────────────────────────────────

    void PrintFullReport()
    {
        Log("──────────────────── WorldFloatText Debug Report ────────────────────");

        // 1. GameObject state
        Log($"[GameObject]  Active in hierarchy : {gameObject.activeInHierarchy}");
        Log($"[GameObject]  World position      : {transform.position}");
        Log($"[GameObject]  World rotation      : {transform.eulerAngles}");

        // 2. Canvas
        if (_canvas == null)
        {
            LogError("[Canvas]  ✖ NO CANVAS FOUND — add a World Space Canvas as a child!");
        }
        else
        {
            Log($"[Canvas]  Render mode : {_canvas.renderMode}   " +
                (_canvas.renderMode != RenderMode.WorldSpace
                    ? "✖ MUST BE WorldSpace!"
                    : "✔"));

            Log($"[Canvas]  Event camera : {(_canvas.worldCamera != null ? _canvas.worldCamera.name : "✖ NULL — text will not sort correctly!")}");

            Vector3 cs = _canvas.transform.localScale;
            bool scaleOk = cs.x > 0.001f && cs.x < 0.1f;
            Log($"[Canvas]  Local scale  : {cs}   " +
                (scaleOk ? "✔ (looks reasonable)" : "⚠ Unusual — default (1,1,1) makes canvas enormous; try 0.005"));

            if (_canvasRect != null)
                Log($"[Canvas]  Rect size    : {_canvasRect.rect.size}");

            // sorting
            Log($"[Canvas]  Sort order   : {_canvas.sortingOrder}  |  Layer: \"{_canvas.sortingLayerName}\"");
        }

        // 3. CanvasGroup
        if (_canvasGroup == null)
        {
            LogError("[CanvasGroup]  ✖ NOT FOUND — the fade system won't work. Add a CanvasGroup component.");
        }
        else
        {
            bool alphaOk = _canvasGroup.alpha > 0.01f;
            Log($"[CanvasGroup]  Alpha           : {_canvasGroup.alpha:F2}   " +
                (alphaOk ? "✔" : "✖ INVISIBLE — alpha is 0. Fade-in may not have started yet."));
            Log($"[CanvasGroup]  Blocks Raycasts : {_canvasGroup.blocksRaycasts}");
            Log($"[CanvasGroup]  Interactable    : {_canvasGroup.interactable}");
        }

        // 4. TextMeshPro
        if (_textMesh == null)
        {
            LogError("[TextMesh]  ✖ NO TextMeshProUGUI FOUND — add one as a child of the Canvas.");
        }
        else
        {
            bool hasText     = !string.IsNullOrEmpty(_textMesh.text);
            bool fontSizeOk  = _textMesh.fontSize >= 10f;
            bool colorAlpha  = _textMesh.color.a > 0.01f;

            Log($"[TextMesh]  Text            : \"{_textMesh.text}\"   " +
                (hasText ? "✔" : "✖ EMPTY — no text has been set yet."));

            Log($"[TextMesh]  Font size       : {_textMesh.fontSize}   " +
                (fontSizeOk ? "✔" : "✖ TOO SMALL — increase to 36–72 for a canvas scaled at 0.005."));

            Log($"[TextMesh]  Color           : {_textMesh.color}   " +
                (colorAlpha ? "✔" : "✖ COLOR ALPHA IS 0 — text is transparent."));

            Log($"[TextMesh]  Enable          : {_textMesh.enabled}   " +
                (_textMesh.enabled ? "✔" : "✖ COMPONENT IS DISABLED."));

            Log($"[TextMesh]  Overflow mode   : {_textMesh.overflowMode}");

            RectTransform rt = _textMesh.GetComponent<RectTransform>();
            if (rt != null)
                Log($"[TextMesh]  Rect size       : {rt.rect.size}   " +
                    (rt.rect.width > 10f && rt.rect.height > 10f ? "✔" : "⚠ Very small rect — text may be clipped."));
        }

        // 5. Camera direction + spawn diagnosis
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 camForward   = cam.transform.forward;
            Vector3 camPos       = cam.transform.position;
            Vector3 projForward  = Vector3.ProjectOnPlane(camForward, Vector3.up);
            bool    degenerate   = projForward.magnitude < 0.01f;

            Log($"[Camera]  cam.transform.forward   : {camForward:F3}");
            Log($"[Camera]  Projected horizontal fwd: {projForward:F3}   " +
                (degenerate ? "✖ ZERO VECTOR — camera is looking straight up/down! Text will not spawn in front." : "✔"));
            Log($"[Camera]  cam.transform.position  : {camPos:F2}");
            Log($"[Camera]  Text world position     : {transform.position:F2}");

            Vector3 toObj   = transform.position - camPos;
            float   dist    = toObj.magnitude;
            float   dot     = Vector3.Dot(camForward, toObj.normalized);
            bool    inFront = dot > 0f;

            Log($"[Camera]  Distance to text : {dist:F2} m   " +
                (dist < 0.5f ? "⚠ Very close — you might be inside it." :
                 dist > 20f  ? "⚠ Very far — check spawn distance." : "✔"));

            Log($"[Camera]  Text in front of camera : {inFront}  (dot={dot:F3})  " +
                (inFront ? "✔" : "✖ Text is BEHIND the camera!"));

            if (!inFront)
            {
                // Extra hint: what direction IS the text from the camera?
                Vector3 dir = toObj.normalized;
                Log($"[Camera]  ► Direction from camera to text: {dir:F3}  " +
                    $"(cam right={cam.transform.right:F2}, up={cam.transform.up:F2}, fwd={camForward:F2})");
            }

            if (dist < cam.nearClipPlane)
                LogError($"[Camera]  ✖ Text is inside near clip plane ({cam.nearClipPlane}m)!");
            else if (dist > cam.farClipPlane)
                LogError($"[Camera]  ✖ Text beyond far clip plane ({cam.farClipPlane}m) — increase Far Clip on camera.");
            else
                Log($"[Camera]  Clip planes OK (near:{cam.nearClipPlane} far:{cam.farClipPlane})   ✔");
        }
        else
        {
            LogError("[Camera]  ✖ Camera.main is NULL — tag your camera as 'MainCamera'.");
        }

        Log("─────────────────────────────────────────────────────────────────────");
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (!drawGizmo) return;

        // Big yellow sphere at the text pivot
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.08f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);

        // White line from camera to text
        if (Camera.main != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(Camera.main.transform.position, transform.position);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static void Log(string msg)      => Debug.Log($"<color=#88DDFF>[FloatTextDebug]</color> {msg}");
    static void LogError(string msg) => Debug.LogError($"[FloatTextDebug] {msg}");
}
