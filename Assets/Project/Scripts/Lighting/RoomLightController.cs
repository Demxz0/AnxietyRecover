using System.Collections;
using UnityEngine;

/// <summary>
/// Attach this to any ceiling light (or a parent that holds multiple lights).
/// It subscribes to AnxietyManager and smoothly shifts the light colour and
/// intensity from calm-blue → warm-white → amber-orange → deep-orange as
/// anxiety rises, matching the GDD colour language.
///
/// Supported light types: Point, Spot, Area (baked only).
/// Works for both real-time and mixed lights.
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
    [SerializeField] private Color calmColor      = new Color(1.00f, 1.00f, 1.00f); // pure white

    [Tooltip("Colour at 40 % anxiety — soft warm white")]
    [SerializeField] private Color neutralColor   = new Color(1.00f, 0.90f, 0.70f); // warm white

    [Tooltip("Colour at 70 % anxiety — amber tension")]
    [SerializeField] private Color highColor      = new Color(1.00f, 0.55f, 0.10f); // amber

    [Tooltip("Colour at 100 % anxiety — deep anxious orange (GDD)")]
    [SerializeField] private Color panicColor     = new Color(1.00f, 0.20f, 0.00f); // deep orange-red

    // ─── Intensity ────────────────────────────────────────────────────────────

    [Header("Intensity")]
    [Tooltip("Light intensity when calm.")]
    [SerializeField] private float calmIntensity  = 1.2f;

    [Tooltip("Light intensity at full panic.")]
    [SerializeField] private float panicIntensity = 0.5f;

    [Tooltip("How quickly colour and intensity interpolate (higher = snappier).")]
    [SerializeField] [Range(0.1f, 10f)] private float lerpSpeed = 1.5f;

    // ─── Flicker ─────────────────────────────────────────────────────────────

    [Header("Flicker (High Anxiety / Panic)")]
    [Tooltip("Anxiety normalised value (0-1) above which flickering starts.")]
    [SerializeField] [Range(0f, 1f)] private float flickerThreshold  = 0.60f;

    [Tooltip("Maximum random ± offset added to intensity each flicker tick.")]
    [SerializeField] private float flickerAmplitude = 0.25f;

    [Tooltip("Seconds between flicker samples (lower = faster, more erratic).")]
    [SerializeField] private float flickerInterval  = 0.08f;

    // ─── Room-Type Overrides ──────────────────────────────────────────────────

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
    private float  _targetIntensity;
    private Color  _targetColor;
    private float  _normalizedAnxiety;
    private bool   _flickerRunning;
    private bool   _isOn; // used when startsOff = true

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _light = GetComponent<Light>();

        if (preset != RoomPreset.None)
            ApplyPreset(preset);

        // Illusion Room: start fully dark, ignore anxiety until switched on
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

    void OnEnable()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnAnxietyChanged += HandleAnxietyChanged;

        // If the light starts off (Illusion Room), skip the initial update
        if (!startsOff)
        {
            float startAnxiety = AnxietyManager.Instance != null
                ? AnxietyManager.Instance.AnxietyValue
                : 0f;
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
        // Do nothing while the light is still off (Illusion Room before switch is found)
        if (!_isOn) return;

        // Smooth lerp toward target each frame
        _light.color     = Color.Lerp(_light.color,     _targetColor,     Time.deltaTime * lerpSpeed);
        _light.intensity = Mathf.Lerp(_light.intensity, _targetIntensity, Time.deltaTime * lerpSpeed);
    }

    // ─── Illusion Room — Public API ───────────────────────────────────────────

    /// <summary>
    /// Call this from your LightSwitchInteraction script when the player connects
    /// the missing piece and activates the switch.
    /// The light turns on at a reveal-brightness, then hands control back to anxiety.
    /// </summary>
    /// <param name="revealIntensity">Brightness of the dramatic reveal flash (default 1.6).</param>
    public void TurnOn(float revealIntensity = 1.6f)
    {
        if (_isOn) return;

        _isOn          = true;
        _light.enabled = true;

        // Snap to a bright reveal colour (neutral white) for the dramatic moment
        _light.intensity = revealIntensity;
        _light.color     = Color.white;

        // After a short beat, hand control back to the anxiety-driven gradient
        StartCoroutine(ResumeAnxietyControlAfterReveal());

        Debug.Log($"[RoomLightController] '{roomName}' light switched ON — reveal flash.");
    }

    private IEnumerator ResumeAnxietyControlAfterReveal()
    {
        // Hold the bright white for 1.5 s so the player can see "no one is there"
        yield return new WaitForSeconds(1.5f);

        // Now sync targets with current anxiety and let Update() lerp from here
        float currentAnxiety = AnxietyManager.Instance != null
            ? AnxietyManager.Instance.AnxietyValue
            : 0f;
        HandleAnxietyChanged(currentAnxiety);
    }

    // ─── Anxiety Response ─────────────────────────────────────────────────────

    private void HandleAnxietyChanged(float anxietyValue)
    {
        // Don't update targets while light is off (Illusion Room)
        if (!_isOn) return;

        _normalizedAnxiety = AnxietyManager.Instance != null
            ? AnxietyManager.Instance.NormalizedAnxiety
            : Mathf.Clamp01(anxietyValue / 100f);

        _targetColor     = EvaluateColor(_normalizedAnxiety);
        _targetIntensity = Mathf.Lerp(calmIntensity, panicIntensity, _normalizedAnxiety);

        // Start / stop flicker
        if (_normalizedAnxiety >= flickerThreshold && !_flickerRunning)
        {
            _flickerRunning = true;
            StartCoroutine(FlickerRoutine());
        }
        else if (_normalizedAnxiety < flickerThreshold && _flickerRunning)
        {
            _flickerRunning = false;
            StopCoroutine(FlickerRoutine()); // stops via flag check inside coroutine
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
            // Flicker strength scales with how far above the threshold we are
            float strength = Mathf.InverseLerp(flickerThreshold, 1f, _normalizedAnxiety);
            float offset   = Random.Range(-flickerAmplitude, flickerAmplitude) * strength;

            _light.intensity = Mathf.Max(0f, _targetIntensity + offset);

            yield return new WaitForSeconds(flickerInterval);
        }

        // Restore smooth target after flicker ends
        _light.intensity = _targetIntensity;
    }

    // ─── Preset System ────────────────────────────────────────────────────────

    [ContextMenu("Apply Selected Preset")]
    public void ApplySelectedPreset() => ApplyPreset(preset);

    /// <summary>
    /// Applies tuned defaults for specific rooms described in the GDD.
    /// You can also call this at runtime: GetComponent<RoomLightController>().ApplyPreset(RoomPreset.IllusionRoom);
    /// </summary>
    public void ApplyPreset(RoomPreset p)
    {
        switch (p)
        {
            // ── Open Room (Final Room — the ONLY room with calm blue) ────────
            case RoomPreset.OpenRoom:
                calmColor       = new Color(0.55f, 0.80f, 1.00f); // calm sky-blue
                neutralColor    = new Color(0.75f, 0.90f, 1.00f); // light blue-white
                highColor       = new Color(1.00f, 0.75f, 0.40f); // soft amber (rare)
                panicColor      = new Color(1.00f, 0.45f, 0.10f); // muted orange (still calmer)
                calmColor       = new Color(0.55f, 0.80f, 1.00f);
                calmIntensity   = 1.8f;  // brightest room — openness, relief
                panicIntensity  = 0.9f;  // even at panic, it stays relatively bright
                flickerThreshold = 0.85f; // almost never flickers — it's a safe space
                flickerAmplitude = 0.05f;
                lerpSpeed        = 0.8f;  // slow, peaceful transitions
                break;

            // ── Living Room / Default rooms ──────────────────────────────────
            case RoomPreset.LivingRoom:
                calmColor       = new Color(1.00f, 1.00f, 1.00f); // pure white
                neutralColor    = new Color(1.00f, 0.90f, 0.70f); // warm white
                highColor       = new Color(1.00f, 0.55f, 0.10f); // amber
                panicColor      = new Color(1.00f, 0.20f, 0.00f); // deep orange
                calmIntensity   = 1.4f;
                panicIntensity  = 0.5f;
                flickerThreshold = 0.65f;
                flickerAmplitude = 0.20f;
                break;

            // ── Bedroom — softer, more intimate ─────────────────────────────
            case RoomPreset.Bedroom:
                calmColor       = new Color(0.95f, 0.95f, 0.90f); // soft off-white
                neutralColor    = new Color(1.00f, 0.85f, 0.65f); // dim warm
                highColor       = new Color(1.00f, 0.52f, 0.08f); // amber-orange
                panicColor      = new Color(1.00f, 0.18f, 0.00f); // deep orange
                calmIntensity   = 0.8f;
                panicIntensity  = 0.3f;
                flickerThreshold = 0.55f;  // starts flickering earlier — more vulnerable
                flickerAmplitude = 0.30f;
                lerpSpeed        = 1.0f;
                break;

            // ── Illusion Room — STARTS FULLY OFF, controlled by light switch ───
            // The gradient is used AFTER TurnOn() is called.
            // Set startsOff = true in the Inspector on this light's controller.
            case RoomPreset.IllusionRoom:
                calmColor       = new Color(1.00f, 0.98f, 0.92f); // near-white (post-reveal)
                neutralColor    = new Color(1.00f, 0.88f, 0.65f); // warm
                highColor       = new Color(1.00f, 0.52f, 0.08f); // orange
                panicColor      = new Color(1.00f, 0.18f, 0.00f); // deep orange
                calmIntensity   = 1.2f;  // normal brightness after switch is on
                panicIntensity  = 0.4f;
                flickerThreshold = 0.45f; // flickering starts earlier — unsettling room
                flickerAmplitude = 0.45f; // heavy flicker
                flickerInterval  = 0.05f; // rapid
                lerpSpeed        = 1.0f;
                startsOff        = true;  // IMPORTANT: light begins completely OFF
                break;

            // ── Loop Room — hypnotic, flat colour shift ─────────────────────
            case RoomPreset.LoopRoom:
                calmColor       = new Color(1.00f, 1.00f, 0.95f); // near-white
                neutralColor    = new Color(1.00f, 0.88f, 0.62f); // warm
                highColor       = new Color(1.00f, 0.52f, 0.08f); // amber
                panicColor      = new Color(1.00f, 0.18f, 0.00f); // deep orange
                calmIntensity   = 1.0f;
                panicIntensity  = 0.6f;
                flickerThreshold = 0.70f;
                flickerAmplitude = 0.15f; // subtle — the trapped feeling is implied, not screamed
                lerpSpeed        = 0.6f;  // very slow shift — the trap closes slowly
                break;

            // ── Bathroom — clinical, bright white ────────────────────────────
            case RoomPreset.Bathroom:
                calmColor       = new Color(1.00f, 1.00f, 1.00f); // pure white (clinical)
                neutralColor    = new Color(1.00f, 0.92f, 0.75f); // warm shift
                highColor       = new Color(1.00f, 0.60f, 0.15f); // amber
                panicColor      = new Color(1.00f, 0.22f, 0.00f); // deep orange
                calmIntensity   = 1.6f;   // bright and clinical when calm
                panicIntensity  = 0.4f;
                flickerThreshold = 0.60f;
                flickerAmplitude = 0.35f; // harsh flicker — broken fluorescent feel
                flickerInterval  = 0.04f;
                break;

            // ── Hallway — narrow, transitional ──────────────────────────────
            case RoomPreset.Hallway:
                calmColor       = new Color(0.95f, 0.95f, 0.95f); // near-white
                neutralColor    = new Color(1.00f, 0.88f, 0.65f); // warm white
                highColor       = new Color(0.95f, 0.52f, 0.08f); // amber
                panicColor      = new Color(1.00f, 0.20f, 0.00f); // deep orange
                calmIntensity   = 0.9f;
                panicIntensity  = 0.35f;
                flickerThreshold = 0.50f; // hallways feel unsafe sooner
                flickerAmplitude = 0.40f;
                flickerInterval  = 0.06f;
                lerpSpeed        = 2.0f;  // snappy — danger comes fast in hallways
                break;

            // ── Kitchen ──────────────────────────────────────────────────────
            case RoomPreset.Kitchen:
                calmColor       = new Color(1.00f, 1.00f, 0.95f); // near-white
                neutralColor    = new Color(1.00f, 0.92f, 0.72f); // warm white
                highColor       = new Color(1.00f, 0.55f, 0.10f); // amber
                panicColor      = new Color(1.00f, 0.20f, 0.00f); // deep orange
                calmIntensity   = 1.5f;
                panicIntensity  = 0.55f;
                flickerThreshold = 0.65f;
                flickerAmplitude = 0.22f;
                break;
        }

        // Apply colours immediately if light is already live
        if (_light != null && !Application.isPlaying)
        {
            float t = 0f; // default to calm in edit mode
            _light.color     = EvaluateColor(t);
            _light.intensity = calmIntensity;
        }

        Debug.Log($"[RoomLightController] '{roomName}' preset applied: {p}");
    }
}

// ─── Presets Enum ──────────────────────────────────────────────────────────────

public enum RoomPreset
{
    None,
    OpenRoom,    // ONLY room with calm blue — the final safe room
    LivingRoom,
    Bedroom,
    IllusionRoom,
    LoopRoom,
    Bathroom,
    Hallway,
    Kitchen
}
