using System.Collections;
using UnityEngine;

/// <summary>
/// Simulates early dawn / morning sunlight entering through the Open Room window.
///
/// Setup:
///   1. Create a Spot Light (or Directional Light) positioned just outside / at the window,
///      angled inward to project across the floor and wall.
///   2. Attach this script to that Light object.
///   3. Tweak Dawn Color, Peak Color, and intensity values in the Inspector.
///
/// Behaviour:
///   • The light gently "breathes" in intensity, simulating natural light variation.
///   • As anxiety rises it dims very slightly and warms up a touch — but never goes orange.
///     The sunlight remains calm no matter what — it is a visual safe harbour.
///   • Completely independent from RoomLightController (the ceiling light handles the blue fill).
/// </summary>
[RequireComponent(typeof(Light))]
public class WindowSunlightController : MonoBehaviour
{
    // ─── Colours ──────────────────────────────────────────────────────────────

    [Header("Sunlight Colours")]
    [Tooltip("The core dawn colour — soft warm golden. Visible at all anxiety levels.")]
    [SerializeField] private Color dawnColor     = new Color(1.00f, 0.88f, 0.62f); // soft gold

    [Tooltip("Very slight shift toward this at max anxiety. Still warm, never orange.")]
    [SerializeField] private Color anxiousColor  = new Color(1.00f, 0.78f, 0.48f); // deeper golden

    // ─── Intensity ────────────────────────────────────────────────────────────

    [Header("Intensity")]
    [Tooltip("Base brightness of the sunlight shaft.")]
    [SerializeField] private float baseIntensity     = 1.8f;

    [Tooltip("Minimum brightness the anxiety can pull the sun down to. Never goes dark.")]
    [SerializeField] private float minAnxietyIntensity = 1.2f;

    // ─── Breathing ────────────────────────────────────────────────────────────

    [Header("Natural Light Breathing")]
    [Tooltip("Enable gentle intensity breathing to simulate cloud-filtered morning light.")]
    [SerializeField] private bool  breathingEnabled  = true;

    [Tooltip("How much ± the intensity drifts during breathing.")]
    [SerializeField] private float breathingAmplitude = 0.12f;

    [Tooltip("Seconds for one full breath cycle (inhale + exhale).")]
    [SerializeField] private float breathingPeriod    = 6f;

    // ─── Light Shaft / Gobo ───────────────────────────────────────────────────

    [Header("Light Shaft (Optional)")]
    [Tooltip("Assign a Cookie texture (window frame silhouette) to the Light in the inspector " +
             "to project a window-frame shadow pattern across the floor. Leave it blank for a " +
             "clean shaft of light. See guide for recommended Cookie settings.")]
    [SerializeField] private bool  useCookie = false; // reminder field — actual cookie set on Light

    // ─── Dust Particles ───────────────────────────────────────────────────────

    [Header("Dust Particles (Optional)")]
    [Tooltip("Assign a Particle System for floating dust motes inside the light shaft. " +
             "The script will enable/disable it and link its emission colour to the sun colour.")]
    [SerializeField] private ParticleSystem dustParticles;

    // ─── Private ──────────────────────────────────────────────────────────────

    private Light _light;
    private float _normalizedAnxiety;
    private float _breathingTimer;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _light = GetComponent<Light>();
        _light.color     = dawnColor;
        _light.intensity = baseIntensity;
    }

    void OnEnable()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnAnxietyChanged += HandleAnxietyChanged;

        if (dustParticles != null)
            dustParticles.Play();
    }

    void OnDisable()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnAnxietyChanged -= HandleAnxietyChanged;

        if (dustParticles != null)
            dustParticles.Stop();
    }

    void Update()
    {
        if (!breathingEnabled) return;

        // Gentle sine-wave breathing — simulates light through moving clouds
        _breathingTimer += Time.deltaTime;
        float breathOffset = Mathf.Sin((2f * Mathf.PI * _breathingTimer) / breathingPeriod)
                             * breathingAmplitude;

        // Target intensity = anxiety-dimmed base + breathing offset
        float anxietyDim   = Mathf.Lerp(baseIntensity, minAnxietyIntensity, _normalizedAnxiety);
        _light.intensity   = Mathf.Max(0f, anxietyDim + breathOffset);
    }

    // ─── Anxiety Response ─────────────────────────────────────────────────────

    private void HandleAnxietyChanged(float anxietyValue)
    {
        _normalizedAnxiety = AnxietyManager.Instance != null
            ? AnxietyManager.Instance.NormalizedAnxiety
            : Mathf.Clamp01(anxietyValue / 100f);

        // Very subtle colour shift — the sun stays golden, never orange
        Color targetColor = Color.Lerp(dawnColor, anxiousColor, _normalizedAnxiety * 0.5f);
        _light.color = Color.Lerp(_light.color, targetColor, Time.deltaTime * 0.8f);

        // Update dust particle colour to match
        if (dustParticles != null)
        {
            var main = dustParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(dawnColor, anxiousColor, _normalizedAnxiety * 0.4f));
        }
    }

    // ─── Editor Helpers ───────────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Preview: Dawn (Calm)")]
    void PreviewCalm()
    {
        _light = GetComponent<Light>();
        _light.color     = dawnColor;
        _light.intensity = baseIntensity;
    }

    [ContextMenu("Preview: High Anxiety")]
    void PreviewAnxious()
    {
        _light = GetComponent<Light>();
        _light.color     = Color.Lerp(dawnColor, anxiousColor, 0.5f);
        _light.intensity = minAnxietyIntensity;
    }
#endif
}
