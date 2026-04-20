using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Central orchestrator for the Panic Attack experience.
///
/// Listens to <see cref="AnxietyManager.OnPanicAttackStarted"/> and coordinates:
///   • Camera shake         → <see cref="PanicCameraShake"/>
///   • Tunnel vision        → URP Vignette (via Volume)
///   • Heavy breathing      → AudioSource
///   • Rapid heartbeat      → AudioSource
///   • Visual distortion    → <see cref="PanicDistortionEffect"/>
///   • Blackout fade        → UI Image overlay
///
/// SETUP:
///   1. Create a GameObject "PanicAttackController" and attach this script.
///   2. Assign all references in the Inspector (see tooltips on each field).
///   3. Ensure the scene has an AnxietyManager singleton.
///   4. Ensure the camera has the <see cref="PanicCameraShake"/> component.
///   5. Create a Global Volume with a Vignette override (intensity = 0 by default).
///   6. Create a Canvas with a full-screen black Image for the blackout overlay.
///   7. Create a Canvas with a full-screen RawImage for the distortion overlay.
///   8. Assign two AudioSources — one for breathing, one for heartbeat.
///   9. Enable "Post Processing" on the Main Camera (URP Camera Data).
/// </summary>
public class PanicAttackController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Timing
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Timing")]
    [Tooltip("Seconds to ramp effects from 0 → full during onset.")]
    [SerializeField] private float onsetDuration = 2f;

    [Tooltip("Seconds the player has to calm down before blackout.")]
    [SerializeField] private float panicDuration = 30f;

    [Tooltip("Seconds to ramp effects down on successful calm-down.")]
    [SerializeField] private float recoveryDuration = 3f;

    [Tooltip("Seconds for the screen to fade to/from black.")]
    [SerializeField] private float blackoutFadeDuration = 2f;

    [Tooltip("Seconds the screen stays fully black (unconscious).")]
    [SerializeField] private float blackoutHoldDuration = 3f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Effect References
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Camera Shake")]
    [Tooltip("The PanicCameraShake component on the camera.")]
    [SerializeField] private PanicCameraShake cameraShake;

    [Header("Tunnel Vision (URP Vignette)")]
    [Tooltip("The scene's Global Volume containing a Vignette override.")]
    [SerializeField] private Volume postProcessVolume;

    [Tooltip("Max vignette intensity during full panic (0–1).")]
    [SerializeField] private float maxVignetteIntensity = 0.55f;

    [Header("Audio — Breathing")]
    [Tooltip("AudioSource playing the heavy breathing loop. Assign the clip in the AudioSource itself.")]
    [SerializeField] private AudioSource breathingSource;

    [Tooltip("Max volume for breathing during panic.")]
    [SerializeField] [Range(0f, 1f)] private float breathingMaxVolume = 0.8f;

    [Tooltip("Breathing pitch at full panic (slightly faster = more panicked).")]
    [SerializeField] private float breathingMaxPitch = 1.3f;

    [Header("Audio — Heartbeat")]
    [Tooltip("AudioSource playing the rapid heartbeat loop. Assign the clip in the AudioSource itself.")]
    [SerializeField] private AudioSource heartbeatSource;

    [Tooltip("Max volume for heartbeat during panic.")]
    [SerializeField] [Range(0f, 1f)] private float heartbeatMaxVolume = 0.9f;

    [Tooltip("Heartbeat pitch at full panic (faster = more frantic).")]
    [SerializeField] private float heartbeatMaxPitch = 1.5f;

    [Header("Visual Distortion")]
    [Tooltip("The PanicDistortionEffect component controlling the overlay shader.")]
    [SerializeField] private PanicDistortionEffect distortionEffect;

    [Header("Blackout Overlay")]
    [Tooltip("Full-screen UI Image used for the fade-to-black effect. " +
             "Should cover the entire screen. Start with alpha = 0.")]
    [SerializeField] private Image blackoutImage;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Recovery
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Post-Blackout")]
    [Tooltip("Anxiety value to set after the player 'wakes up' from a blackout. " +
             "Mild level (~30) avoids immediate re-trigger.")]
    [SerializeField] private float postBlackoutAnxiety = 30f;

    // ═══════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ═══════════════════════════════════════════════════════════════════════

    private Vignette _vignette;
    private Coroutine _panicRoutine;
    private bool _isActive;

    /// <summary>
    /// Set to true by external calming systems (breathing mini-game, medication)
    /// to signal that the panic attack has been resolved.
    /// </summary>
    private bool _calmedDown;

    // ═══════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    void Awake()
    {
        // Cache the Vignette override from the Volume profile
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            if (!postProcessVolume.profile.TryGet(out _vignette))
            {
                Debug.LogWarning("[PanicAttackController] Volume profile has no Vignette override! " +
                                 "Add one for the tunnel-vision effect.");
            }
        }
        else
        {
            Debug.LogWarning("[PanicAttackController] No post-process Volume assigned. " +
                             "Tunnel vision (vignette) will be skipped.");
        }

        // Ensure blackout starts invisible
        if (blackoutImage != null)
            SetBlackoutAlpha(0f);

        // Ensure audio starts silent
        InitAudioSource(breathingSource);
        InitAudioSource(heartbeatSource);
    }

    void OnEnable()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnPanicAttackStarted += HandlePanicStarted;
        }
        else
        {
            // AnxietyManager might initialize later; retry in Start
            StartCoroutine(SubscribeWhenReady());
        }
    }

    void OnDisable()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnPanicAttackStarted -= HandlePanicStarted;
    }

    IEnumerator SubscribeWhenReady()
    {
        yield return null; // Wait one frame
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnPanicAttackStarted += HandlePanicStarted;
        else
            Debug.LogError("[PanicAttackController] AnxietyManager not found in scene!");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call this from the breathing mini-game or medication system
    /// to signal that the player has calmed down during an active panic attack.
    /// </summary>
    public void CalmDown()
    {
        if (!_isActive)
        {
            Debug.Log("[PanicAttackController] CalmDown called but no panic is active.");
            return;
        }

        _calmedDown = true;
        Debug.Log("[PanicAttackController] ✓ Player calmed down! Starting recovery...");
    }

    /// <summary>True while a panic attack sequence is running.</summary>
    public bool IsActive => _isActive;

    // ═══════════════════════════════════════════════════════════════════════
    //  PANIC SEQUENCE
    // ═══════════════════════════════════════════════════════════════════════

    void HandlePanicStarted()
    {
        if (_isActive) return; // guard against double-fires
        _panicRoutine = StartCoroutine(PanicSequence());
    }

    IEnumerator PanicSequence()
    {
        _isActive   = true;
        _calmedDown = false;

        Debug.Log("[PanicAttackController] ── Panic Attack Onset ──");

        // ── Phase 1: ONSET — ramp effects up ─────────────────────────────
        StartAudioLoops();

        float elapsed = 0f;
        while (elapsed < onsetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / onsetDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t); // ease in-out
            ApplyEffects(smoothT);
            FadeAudio(smoothT);
            yield return null;
        }
        ApplyEffects(1f); // ensure we hit exactly 1.0
        FadeAudio(1f);

        Debug.Log("[PanicAttackController] ── Panic Attack Active (waiting for calm or timeout) ──");

        // ── Phase 2: HOLD — wait for calm-down or timeout ────────────────
        float holdTimer = 0f;
        while (holdTimer < panicDuration && !_calmedDown)
        {
            holdTimer += Time.deltaTime;

            // Subtle intensity fluctuation during hold (adds unease)
            float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.5f);
            ApplyEffects(flicker);

            yield return null;
        }

        if (_calmedDown)
        {
            // ── Phase 3A: RECOVERY — player calmed down successfully ─────
            Debug.Log("[PanicAttackController] ── Recovery Phase ──");
            yield return StartCoroutine(RampDown(recoveryDuration));
            StopAudioLoops();

            _isActive = false;
            AnxietyManager.Instance.NotifyPanicAttackEnded();
            Debug.Log("[PanicAttackController] ── Panic Attack Resolved ✓ ──");
        }
        else
        {
            // ── Phase 3B: BLACKOUT — player failed to calm down ──────────
            Debug.Log("[PanicAttackController] ── Blackout Sequence ──");
            yield return StartCoroutine(BlackoutSequence());

            _isActive = false;
            AnxietyManager.Instance.NotifyPanicAttackEnded();
            Debug.Log("[PanicAttackController] ── Player Woke Up ──");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  BLACKOUT SEQUENCE
    // ═══════════════════════════════════════════════════════════════════════

    IEnumerator BlackoutSequence()
    {
        // Fade all effects + screen to black simultaneously
        float elapsed = 0f;
        while (elapsed < blackoutFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / blackoutFadeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Effects ramp down while screen goes black
            ApplyEffects(1f - smoothT);
            SetBlackoutAlpha(smoothT);

            // Fade audio out too
            FadeAudio(1f - smoothT);

            yield return null;
        }

        ApplyEffects(0f);
        SetBlackoutAlpha(1f);
        StopAudioLoops();

        // ── Hold black (unconscious) ─────────────────────────────────────
        Debug.Log("[PanicAttackController] Character unconscious...");

        // Reset anxiety to mild level while the screen is black
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.SetAnxiety(postBlackoutAnxiety);

        yield return new WaitForSeconds(blackoutHoldDuration);

        // ── Fade back in ─────────────────────────────────────────────────
        elapsed = 0f;
        while (elapsed < blackoutFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / blackoutFadeDuration);
            SetBlackoutAlpha(1f - Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        SetBlackoutAlpha(0f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  RAMP DOWN (successful recovery)
    // ═══════════════════════════════════════════════════════════════════════

    IEnumerator RampDown(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            ApplyEffects(t);
            FadeAudio(t);
            yield return null;
        }

        ApplyEffects(0f);
        StopAudioLoops();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  EFFECT APPLICATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply all panic effects at the given intensity (0 = none, 1 = full).
    /// </summary>
    void ApplyEffects(float t)
    {
        // Camera shake
        if (cameraShake != null)
            cameraShake.Intensity = t;

        // Tunnel vision (vignette)
        if (_vignette != null)
        {
            _vignette.intensity.Override(Mathf.Lerp(0f, maxVignetteIntensity, t));
        }

        // Visual distortion overlay
        if (distortionEffect != null)
            distortionEffect.Intensity = t;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  AUDIO HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    void InitAudioSource(AudioSource src)
    {
        if (src == null) return;
        src.volume = 0f;
        src.loop   = true;
        src.playOnAwake = false;
    }

    void StartAudioLoops()
    {
        if (breathingSource != null && breathingSource.clip != null && !breathingSource.isPlaying)
            breathingSource.Play();

        if (heartbeatSource != null && heartbeatSource.clip != null && !heartbeatSource.isPlaying)
            heartbeatSource.Play();
    }

    void StopAudioLoops()
    {
        if (breathingSource != null) { breathingSource.Stop(); breathingSource.volume = 0f; }
        if (heartbeatSource != null) { heartbeatSource.Stop(); heartbeatSource.volume = 0f; }
    }

    /// <summary>
    /// Blend audio volume and pitch based on intensity t (0→1).
    /// </summary>
    void FadeAudio(float t)
    {
        if (breathingSource != null)
        {
            breathingSource.volume = Mathf.Lerp(0f, breathingMaxVolume, t);
            breathingSource.pitch  = Mathf.Lerp(1f, breathingMaxPitch, t);
        }

        if (heartbeatSource != null)
        {
            heartbeatSource.volume = Mathf.Lerp(0f, heartbeatMaxVolume, t);
            heartbeatSource.pitch  = Mathf.Lerp(1f, heartbeatMaxPitch, t);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  BLACKOUT OVERLAY
    // ═══════════════════════════════════════════════════════════════════════

    void SetBlackoutAlpha(float alpha)
    {
        if (blackoutImage == null) return;
        Color c = blackoutImage.color;
        blackoutImage.color = new Color(c.r, c.g, c.b, alpha);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  EDITOR TEST HELPERS
    // ═══════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Test: Trigger Panic Attack")]
    void TestTrigger()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.TriggerPanicAttack();
        else
            Debug.LogError("No AnxietyManager in scene!");
    }

    [ContextMenu("Test: Calm Down")]
    void TestCalmDown() => CalmDown();
#endif
}
