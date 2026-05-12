using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// WebGL-safe replacement for the realtime RoomLightController color shifts.
///
/// Instead of changing realtime light colors (expensive, breaks baked lighting),
/// this script drives the URP post-processing ColorAdjustments override on a
/// Global Volume to shift the whole scene's color temperature as anxiety rises:
///
///   Calm      → neutral (no shift)
///   Mild      → slight warm tint
///   High      → amber tint
///   Extreme   → strong orange tint
///   Panic     → deep orange-red tint  (pulsing)
///
/// ZERO realtime shadow cost — post-process is a single GPU fullscreen pass.
///
/// SETUP:
///   1. Create a Global Volume in the scene (or reuse the existing one).
///   2. Add a ColorAdjustments override to its profile (set Color Filter to white).
///   3. Attach this script to any persistent GameObject (e.g. the AudioManager GO).
///   4. Assign the Volume in the Inspector.
///   5. For the Open Room calm feeling, uncheck "affectOpenRoom" on this component
///      OR use a separate Volume with higher Priority that overrides it.
/// </summary>
public class AnxietyColorGrading : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Target Volume")]
    [Tooltip("The Global Volume containing a ColorAdjustments override.")]
    [SerializeField] private Volume postProcessVolume;

    [Header("Color Gradient (Calm → Panic)")]
    [Tooltip("Color at 0% anxiety — pure white (no tint).")]
    [SerializeField] private Color calmColor    = Color.white;

    [Tooltip("Color at 40% anxiety — soft warm white.")]
    [SerializeField] private Color warmColor    = new Color(1.00f, 0.92f, 0.78f);

    [Tooltip("Color at 70% anxiety — amber tension.")]
    [SerializeField] private Color amberColor   = new Color(1.00f, 0.68f, 0.30f);

    [Tooltip("Color at 100% anxiety — deep orange-red.")]
    [SerializeField] private Color panicColor   = new Color(1.00f, 0.35f, 0.10f);

    [Header("Transition")]
    [Tooltip("How quickly the color filter lerps toward the anxiety target.")]
    [SerializeField] [Range(0.1f, 5f)] private float lerpSpeed = 1.2f;

    [Header("Panic Pulse")]
    [Tooltip("When at Panic level, the color filter slightly pulses for added unease.")]
    [SerializeField] private bool  pulseAtPanic    = true;
    [SerializeField] private float pulseSpeed      = 1.8f;
    [SerializeField] private float pulseAmplitude  = 0.08f; // how much it oscillates

    [Header("Open Room Override")]
    [Tooltip("Disable anxiety color grading in the Open Room (uses its own calm blue palette).")]
    [SerializeField] private bool disableInOpenRoom = false;

    // ─── Private ──────────────────────────────────────────────────────────────

    private ColorAdjustments _colorAdj;
    private Color  _currentColor;
    private Color  _targetColor;
    private float  _normalizedAnxiety;
    private bool   _isPanic;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    void Start()
    {
        // Grab ColorAdjustments from the volume profile
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            if (!postProcessVolume.profile.TryGet(out _colorAdj))
                Debug.LogWarning("[AnxietyColorGrading] No ColorAdjustments override in the Volume profile. " +
                                 "Add one (set Color Filter to white).");
        }
        else
        {
            Debug.LogWarning("[AnxietyColorGrading] No Volume assigned.");
        }

        // Subscribe
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnAnxietyChanged += HandleAnxietyChanged;
            AnxietyManager.Instance.OnLevelChanged   += HandleLevelChanged;
            HandleAnxietyChanged(AnxietyManager.Instance.AnxietyValue);
        }

        _currentColor = calmColor;
        _targetColor  = calmColor;
        ApplyColor(_currentColor);
    }

    void OnDestroy()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnAnxietyChanged -= HandleAnxietyChanged;
            AnxietyManager.Instance.OnLevelChanged   -= HandleLevelChanged;
        }
    }

    void Update()
    {
        if (_colorAdj == null) return;

        // Compute base target
        Color baseTarget = EvaluateColor(_normalizedAnxiety);

        // Add panic pulse
        if (_isPanic && pulseAtPanic)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
            baseTarget = new Color(
                Mathf.Clamp01(baseTarget.r + pulse),
                Mathf.Clamp01(baseTarget.g - pulse * 0.5f),
                Mathf.Clamp01(baseTarget.b - pulse * 0.5f),
                1f);
        }

        _targetColor  = baseTarget;
        _currentColor = Color.Lerp(_currentColor, _targetColor, Time.deltaTime * lerpSpeed);
        ApplyColor(_currentColor);
    }

    // ─── Anxiety Callbacks ────────────────────────────────────────────────────

    void HandleAnxietyChanged(float value)
    {
        if (AnxietyManager.Instance != null)
            _normalizedAnxiety = AnxietyManager.Instance.NormalizedAnxiety;
        else
            _normalizedAnxiety = Mathf.Clamp01(value / 100f);
    }

    void HandleLevelChanged(AnxietyLevel level)
    {
        _isPanic = level == AnxietyLevel.Panic;
    }

    // ─── Color Mapping ────────────────────────────────────────────────────────

    /// <summary>
    /// Three-segment gradient matching the old RoomLightController:
    ///   0.00–0.40  : calm white    → warm white
    ///   0.40–0.70  : warm white   → amber
    ///   0.70–1.00  : amber        → deep orange-red (panic)
    /// </summary>
    Color EvaluateColor(float t)
    {
        if (t <= 0.40f)
            return Color.Lerp(calmColor,  warmColor,  t / 0.40f);
        if (t <= 0.70f)
            return Color.Lerp(warmColor,  amberColor, (t - 0.40f) / 0.30f);
        return     Color.Lerp(amberColor, panicColor, (t - 0.70f) / 0.30f);
    }

    void ApplyColor(Color c)
    {
        if (_colorAdj == null) return;
        _colorAdj.colorFilter.Override(c);
    }

#if UNITY_EDITOR
    [ContextMenu("Preview: Calm")]
    void PreviewCalm()    { ApplyColor(calmColor);  }

    [ContextMenu("Preview: Amber (70%)")]
    void PreviewAmber()   { ApplyColor(amberColor); }

    [ContextMenu("Preview: Panic (100%)")]
    void PreviewPanic()   { ApplyColor(panicColor); }

    [ContextMenu("Preview: Reset")]
    void PreviewReset()   { ApplyColor(Color.white); }
#endif
}
