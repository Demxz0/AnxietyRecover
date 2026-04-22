using System.Collections;
using UnityEngine;

/// <summary>
/// Attach this to any ceiling light (or a parent that holds multiple lights).
/// It subscribes to AnxietyManager and smoothly shifts the light colour from
/// calm → warm-white → amber-orange → deep-orange as anxiety rises,
/// matching the GDD colour language.
///
/// Intensity is NOT controlled by this script — set it manually on the
/// Light component in the Inspector and it will never be touched.
///
/// Supported light types: Point, Spot.
/// Light Mode must be set to Realtime.
/// </summary>
[RequireComponent(typeof(Light))]
public class RoomLightController : MonoBehaviour
{
    // ─── Per-Room Identity ────────────────────────────────────────────────────

    [Header("Room Identity")]
    [Tooltip("Human-readable label shown in Debug logs.")]
    [SerializeField] private string roomName = "Room";

    // ─── Colour Gradient ──────────────────────────────────────────────────────

    [Header("Colour Gradient (Low → High Anxiety)")]
    [Tooltip("Colour at 0 % anxiety — clean white (blue is reserved for the Open Room only)")]
    [SerializeField] private Color calmColor    = new Color(1.00f, 1.00f, 1.00f); // pure white

    [Tooltip("Colour at 40 % anxiety — soft warm white")]
    [SerializeField] private Color neutralColor = new Color(1.00f, 0.90f, 0.70f); // warm white

    [Tooltip("Colour at 70 % anxiety — amber tension")]
    [SerializeField] private Color highColor    = new Color(1.00f, 0.55f, 0.10f); // amber

    [Tooltip("Colour at 100 % anxiety — deep anxious orange (GDD)")]
    [SerializeField] private Color panicColor   = new Color(1.00f, 0.20f, 0.00f); // deep orange-red

    // ─── Colour Transition ────────────────────────────────────────────────────

    [Header("Colour Transition")]
    [Tooltip("How quickly colour interpolates toward the anxiety target (higher = snappier).")]
    [SerializeField] [Range(0.1f, 10f)] private float lerpSpeed = 1.5f;

    // ─── Flicker ─────────────────────────────────────────────────────────────

    [Header("Flicker (High Anxiety / Panic)")]
    [Tooltip("Anxiety normalised value (0-1) above which flickering starts.")]
    [SerializeField] [Range(0f, 1f)] private float flickerThreshold  = 0.60f;

    [Tooltip("Maximum random ± offset added to intensity each flicker tick.")]
    [SerializeField] private float flickerAmplitude = 0.25f;

    [Tooltip("Seconds between flicker samples (lower = faster, more erratic).")]
    [SerializeField] private float flickerInterval  = 0.08f;

    // ─── Room-Type Preset ─────────────────────────────────────────────────────

    [Header("Room-Type Preset (optional)")]
    [Tooltip("Pick a preset that sets sensible defaults for a specific room. " +
             "Call ApplyPreset() from the inspector context menu or at runtime.")]
    [SerializeField] private RoomPreset preset = RoomPreset.None;

    // ─── Illusion Room — Light Switch ─────────────────────────────────────────

    [Header("Illusion Room — Light Switch")]
    [Tooltip("If true, the light starts completely OFF and ignores anxiety until TurnOn() is called. " +
             "Use this for the Illusion Room where the player must find the switch piece.")]
    [SerializeField] private bool startsOff = false;

    // ─── Private State ────────────────────────────────────────────────────────

    private Light  _light;
    private Color  _targetColor;
    private float  _normalizedAnxiety;
    private bool   _flickerRunning;
    private float  _flickerBaseIntensity;
    private bool   _isOn;
    private bool   _started; // true after Start() has run — guards OnEnable re-subscription

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _light = GetComponent<Light>();

        if (preset != RoomPreset.None)
            ApplyPreset(preset);

        if (startsOff)
        {
            _isOn            = false;
            _light.intensity = 0f;
            _light.enabled   = false;
        }
        else
        {
            _isOn = true;
        }
    }

    // ─── OnEnable / Start / OnDisable ─────────────────────────────────────────

    void OnEnable()
    {
        // At scene load OnEnable fires before AnxietyManager.Awake() sets its Instance,
        // so subscription here would silently fail. Start() handles the initial subscription.
        // This block handles only mid-game re-enables (e.g. room GameObject toggled on).
        if (_started && AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnAnxietyChanged += HandleAnxietyChanged;
    }

    void Start()
    {
        // All Awake() calls in the scene are guaranteed to have finished before Start() runs,
        // so AnxietyManager.Instance is safely set here.
        _started = true;

        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnAnxietyChanged += HandleAnxietyChanged;

        if (!startsOff)
        {
            float startAnxiety = AnxietyManager.Instance != null
                ? AnxietyManager.Instance.AnxietyValue : 0f;
            HandleAnxietyChanged(startAnxiety);
        }
    }

    void OnDisable()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnAnxietyChanged -= HandleAnxietyChanged;

        StopAllCoroutines();
        _flickerRunning = false;
    }

    void Update()
    {
        if (!_isOn) return;

        // Smooth colour lerp toward anxiety target — intensity is never touched here
        _light.color = Color.Lerp(_light.color, _targetColor, Time.deltaTime * lerpSpeed);
    }

    // ─── Illusion Room — Public API ───────────────────────────────────────────

    /// <summary>
    /// Call this from your LightSwitchInteraction script when the player connects
    /// the missing piece and activates the switch.
    /// The light snaps to bright white for 1.5 s (dramatic reveal), then colour
    /// control hands back to the anxiety gradient.
    /// </summary>
    /// <param name="revealIntensity">Brightness of the dramatic reveal flash (default 1.6).</param>
    public void TurnOn(float revealIntensity = 1.6f)
    {
        if (_isOn) return;

        _isOn          = true;
        _light.enabled = true;

        _light.intensity = revealIntensity;
        _light.color     = Color.white;

        StartCoroutine(ResumeAnxietyControlAfterReveal());

        Debug.Log($"[RoomLightController] '{roomName}' light switched ON — reveal flash.");
    }

    private IEnumerator ResumeAnxietyControlAfterReveal()
    {
        yield return new WaitForSeconds(1.5f);

        float currentAnxiety = AnxietyManager.Instance != null
            ? AnxietyManager.Instance.AnxietyValue : 0f;
        HandleAnxietyChanged(currentAnxiety);
    }

    // ─── Anxiety Response ─────────────────────────────────────────────────────

    private void HandleAnxietyChanged(float anxietyValue)
    {
        if (!_isOn) return;

        _normalizedAnxiety = AnxietyManager.Instance != null
            ? AnxietyManager.Instance.NormalizedAnxiety
            : Mathf.Clamp01(anxietyValue / 100f);

        _targetColor = EvaluateColor(_normalizedAnxiety);

        if (_normalizedAnxiety >= flickerThreshold && !_flickerRunning)
        {
            _flickerBaseIntensity = _light.intensity;
            _flickerRunning       = true;
            StartCoroutine(FlickerRoutine());
        }
        else if (_normalizedAnxiety < flickerThreshold && _flickerRunning)
        {
            _flickerRunning = false;
        }
    }

    // ─── Colour Mapping ───────────────────────────────────────────────────────

    /// <summary>
    /// Three-segment gradient:
    ///   0.00 → 0.40  : white       → warm white   (normal, slightly uneasy)
    ///   0.40 → 0.70  : warm white  → amber        (tension building)
    ///   0.70 → 1.00  : amber       → deep orange  (overwhelm / panic)
    ///
    /// NOTE: Blue is NOT used in this gradient.
    /// Blue is reserved exclusively for the final Open Room.
    /// </summary>
    private Color EvaluateColor(float t)
    {
        if (t <= 0.40f)
            return Color.Lerp(calmColor,    neutralColor, t / 0.40f);

        if (t <= 0.70f)
            return Color.Lerp(neutralColor, highColor,    (t - 0.40f) / 0.30f);

        return Color.Lerp(highColor,    panicColor,   (t - 0.70f) / 0.30f);
    }

    // ─── Flicker ──────────────────────────────────────────────────────────────

    private IEnumerator FlickerRoutine()
    {
        while (_flickerRunning)
        {
            float strength = Mathf.InverseLerp(flickerThreshold, 1f, _normalizedAnxiety);
            float offset   = Random.Range(-flickerAmplitude, flickerAmplitude) * strength;

            _light.intensity = Mathf.Max(0f, _flickerBaseIntensity + offset);

            yield return new WaitForSeconds(flickerInterval);
        }

        _light.intensity = _flickerBaseIntensity;
    }

    // ─── Preset System ────────────────────────────────────────────────────────

    [ContextMenu("Apply Selected Preset")]
    public void ApplySelectedPreset() => ApplyPreset(preset);

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Force Calm Color (0%)")]
    void DebugCalm()
    {
        _light = GetComponent<Light>();
        _targetColor = EvaluateColor(0f);
        _light.color = _targetColor;
        _normalizedAnxiety = 0f;
    }

    [ContextMenu("DEBUG: Force Mid Anxiety Color (50%)")]
    void DebugMid()
    {
        _light = GetComponent<Light>();
        _targetColor = EvaluateColor(0.5f);
        _light.color = _targetColor;
        _normalizedAnxiety = 0.5f;
    }

    [ContextMenu("DEBUG: Force Panic Color (100%)")]
    void DebugPanic()
    {
        _light = GetComponent<Light>();
        _targetColor = EvaluateColor(1f);
        _light.color = _targetColor;
        _normalizedAnxiety = 1f;
    }
#endif

    /// <summary>
    /// Applies tuned colour and flicker defaults for specific rooms described in the GDD.
    /// Intensity is NOT set by presets — configure it directly on the Light component.
    /// </summary>
    public void ApplyPreset(RoomPreset p)
    {
        switch (p)
        {
            case RoomPreset.OpenRoom:
                calmColor        = new Color(0.55f, 0.80f, 1.00f); // calm sky-blue
                neutralColor     = new Color(0.75f, 0.90f, 1.00f); // light blue-white
                highColor        = new Color(1.00f, 0.75f, 0.40f); // soft amber (rare)
                panicColor       = new Color(1.00f, 0.45f, 0.10f); // muted orange (still calmer)
                flickerThreshold = 0.85f;
                flickerAmplitude = 0.05f;
                lerpSpeed        = 0.8f;
                break;

            case RoomPreset.LivingRoom:
                calmColor        = new Color(1.00f, 1.00f, 1.00f); // pure white
                neutralColor     = new Color(1.00f, 0.90f, 0.70f); // warm white
                highColor        = new Color(1.00f, 0.55f, 0.10f); // amber
                panicColor       = new Color(1.00f, 0.20f, 0.00f); // deep orange
                flickerThreshold = 0.65f;
                flickerAmplitude = 0.20f;
                break;

            case RoomPreset.Bedroom:
                calmColor        = new Color(0.95f, 0.95f, 0.90f); // soft off-white
                neutralColor     = new Color(1.00f, 0.85f, 0.65f); // dim warm
                highColor        = new Color(1.00f, 0.52f, 0.08f); // amber-orange
                panicColor       = new Color(1.00f, 0.18f, 0.00f); // deep orange
                flickerThreshold = 0.55f;
                flickerAmplitude = 0.30f;
                lerpSpeed        = 1.0f;
                break;

            case RoomPreset.IllusionRoom:
                calmColor        = new Color(1.00f, 0.98f, 0.92f); // near-white (post-reveal)
                neutralColor     = new Color(1.00f, 0.88f, 0.65f); // warm
                highColor        = new Color(1.00f, 0.52f, 0.08f); // orange
                panicColor       = new Color(1.00f, 0.18f, 0.00f); // deep orange
                flickerThreshold = 0.45f;
                flickerAmplitude = 0.45f;
                flickerInterval  = 0.05f;
                lerpSpeed        = 1.0f;
                startsOff        = true;
                break;

            case RoomPreset.LoopRoom:
                calmColor        = new Color(1.00f, 1.00f, 0.95f); // near-white
                neutralColor     = new Color(1.00f, 0.88f, 0.62f); // warm
                highColor        = new Color(1.00f, 0.52f, 0.08f); // amber
                panicColor       = new Color(1.00f, 0.18f, 0.00f); // deep orange
                flickerThreshold = 0.70f;
                flickerAmplitude = 0.15f;
                lerpSpeed        = 0.6f;
                break;

            case RoomPreset.Bathroom:
                calmColor        = new Color(1.00f, 1.00f, 1.00f); // pure white (clinical)
                neutralColor     = new Color(1.00f, 0.92f, 0.75f); // warm shift
                highColor        = new Color(1.00f, 0.60f, 0.15f); // amber
                panicColor       = new Color(1.00f, 0.22f, 0.00f); // deep orange
                flickerThreshold = 0.60f;
                flickerAmplitude = 0.35f;
                flickerInterval  = 0.04f;
                break;

            case RoomPreset.Hallway:
                calmColor        = new Color(0.95f, 0.95f, 0.95f); // near-white
                neutralColor     = new Color(1.00f, 0.88f, 0.65f); // warm white
                highColor        = new Color(0.95f, 0.52f, 0.08f); // amber
                panicColor       = new Color(1.00f, 0.20f, 0.00f); // deep orange
                flickerThreshold = 0.50f;
                flickerAmplitude = 0.40f;
                flickerInterval  = 0.06f;
                lerpSpeed        = 2.0f;
                break;

            case RoomPreset.Kitchen:
                calmColor        = new Color(1.00f, 1.00f, 0.95f); // near-white
                neutralColor     = new Color(1.00f, 0.92f, 0.72f); // warm white
                highColor        = new Color(1.00f, 0.55f, 0.10f); // amber
                panicColor       = new Color(1.00f, 0.20f, 0.00f); // deep orange
                flickerThreshold = 0.65f;
                flickerAmplitude = 0.22f;
                break;
        }

        if (_light != null && !Application.isPlaying)
            _light.color = EvaluateColor(0f);

        Debug.Log($"[RoomLightController] '{roomName}' preset applied: {p}");
    }
}

// ─── Presets Enum ──────────────────────────────────────────────────────────────

public enum RoomPreset
{
    None,
    OpenRoom,
    LivingRoom,
    Bedroom,
    IllusionRoom,
    LoopRoom,
    Bathroom,
    Hallway,
    Kitchen
}
