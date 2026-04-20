using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the full-screen distortion overlay used during panic attacks.
///
/// SETUP:
///   1. Create a Canvas (Screen Space – Overlay, Sort Order = 999)
///   2. Add a RawImage child that stretches to fill the canvas
///   3. Assign a Material using the "RecoveryGame/PanicDistortion" shader
///   4. Drag the RawImage into the <see cref="distortionImage"/> field
///
/// <see cref="PanicAttackController"/> drives <see cref="Intensity"/> from 0 → 1.
/// </summary>
public class PanicDistortionEffect : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Full-screen RawImage with the PanicDistortion material.")]
    [SerializeField] private RawImage distortionImage;

    [Header("Effect Ranges")]
    [Tooltip("Max chromatic aberration offset at full intensity.")]
    [SerializeField] private float maxChromaOffset = 0.02f;

    [Tooltip("Max wave distortion amplitude at full intensity.")]
    [SerializeField] private float maxWaveAmplitude = 0.015f;

    [Tooltip("Wave frequency (higher = more ripples).")]
    [SerializeField] private float waveFrequency = 15f;

    [Tooltip("Wave animation speed.")]
    [SerializeField] private float waveSpeed = 3f;

    [Tooltip("Max red tint overlay at full intensity.")]
    [SerializeField] private float maxRedTint = 0.25f;

    [Tooltip("Red tint pulse speed.")]
    [SerializeField] private float pulseSpeed = 3f;

    // ─── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Set by PanicAttackController. 0 = invisible, 1 = full distortion.
    /// </summary>
    public float Intensity { get; set; }

    // ─── Private ───────────────────────────────────────────────────────────

    private Material _mat;

    // Shader property IDs (cached for performance)
    private static readonly int PropIntensity     = Shader.PropertyToID("_Intensity");
    private static readonly int PropChromaOffset  = Shader.PropertyToID("_ChromaOffset");
    private static readonly int PropWaveAmplitude = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int PropWaveFrequency = Shader.PropertyToID("_WaveFrequency");
    private static readonly int PropWaveSpeed     = Shader.PropertyToID("_WaveSpeed");
    private static readonly int PropRedTint       = Shader.PropertyToID("_RedTint");
    private static readonly int PropPulseSpeed    = Shader.PropertyToID("_PulseSpeed");

    void Start()
    {
        if (distortionImage == null)
        {
            Debug.LogError("[PanicDistortionEffect] No distortion RawImage assigned!");
            enabled = false;
            return;
        }

        // Create an instance so we don't modify the shared material asset
        _mat = Instantiate(distortionImage.material);
        distortionImage.material = _mat;

        // Start invisible
        SetMaterialProperties(0f);
        distortionImage.enabled = false;
    }

    void Update()
    {
        if (_mat == null) return;

        bool shouldBeVisible = Intensity > 0.001f;
        distortionImage.enabled = shouldBeVisible;

        if (shouldBeVisible)
            SetMaterialProperties(Intensity);
    }

    void SetMaterialProperties(float t)
    {
        _mat.SetFloat(PropIntensity,     t);
        _mat.SetFloat(PropChromaOffset,  maxChromaOffset * t);
        _mat.SetFloat(PropWaveAmplitude, maxWaveAmplitude * t);
        _mat.SetFloat(PropWaveFrequency, waveFrequency);
        _mat.SetFloat(PropWaveSpeed,     waveSpeed);
        _mat.SetFloat(PropRedTint,       maxRedTint * t);
        _mat.SetFloat(PropPulseSpeed,    pulseSpeed);
    }

    void OnDestroy()
    {
        if (_mat != null)
            Destroy(_mat);
    }
}
