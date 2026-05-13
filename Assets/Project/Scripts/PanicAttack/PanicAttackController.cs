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
///   • Heavy breathing      → AudioManager.PlayLoop(SoundID.BreathingHeavy)
///   • Rapid heartbeat      → AudioManager.PlayLoop(SoundID.Heartbeat)
///   • Visual distortion    → <see cref="PanicDistortionEffect"/>
///   • Blackout fade        → UI Image overlay
///
/// SETUP:
///   1. Create a GameObject "PanicAttackController" and attach this script.
///   2. Assign all references in the Inspector (see tooltips on each field).
///   3. Ensure the scene has an AnxietyManager singleton and an AudioManager singleton.
///   4. Ensure the camera has the <see cref="PanicCameraShake"/> component.
///   5. Create a Global Volume with a Vignette override (intensity = 0 by default).
///   6. Create a Canvas with a full-screen black Image for the blackout overlay.
///   7. Create a Canvas with a full-screen RawImage for the distortion overlay.
///   8. Enable "Post Processing" on the Main Camera (URP Camera Data).
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

    // Audio is handled entirely by AudioManager.
    // Clips: SoundID.BreathingHeavy and SoundID.Heartbeat.

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

    [Header("Wake-Up Sequence")]
    [Tooltip("The camera Transform used for the wake-up tilt. Auto-finds Camera.main if not set.")]
    [SerializeField] private Transform wakeUpCamera;

    [Tooltip("How far the camera tilts sideways when lying on the ground (degrees on Z axis, e.g. 80).")]
    [SerializeField] private float wakeUpDownPitch = 80f;

    [Tooltip("Local Y position of the camera when lying on the ground " +
             "(relative to player body). 0 = floor level. Should be lower than normal eye height.")]
    [SerializeField] private float wakeUpGroundY = 0f;

    [Tooltip("How long the camera stays tilted down before starting to rise (seconds).")]
    [SerializeField] private float wakeUpLieDownDuration = 1.5f;

    [Tooltip("How long the camera takes to rise from lying down to upright (seconds).")]
    [SerializeField] private float wakeUpRiseDuration = 2.5f;

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

    /// <summary>
    /// Skip directly to the blackout phase — bypasses the onset and hold.
    /// Useful for Editor testing or scripted story events.
    /// Safe to call even if no panic is currently running.
    /// </summary>
    public void ForceBlackout()
    {
        // Stop any in-progress panic coroutine so we don't double-run
        if (_panicRoutine != null)
        {
            StopCoroutine(_panicRoutine);
            _panicRoutine = null;
        }

        _panicRoutine = StartCoroutine(ForceBlackoutRoutine());
    }

    IEnumerator ForceBlackoutRoutine()
    {
        _isActive   = true;
        _calmedDown = false;

        // Snap all effects to full immediately
        AudioManager.Instance?.PlayLoop(SoundID.BreathingHeavy);
        ApplyEffects(1f);

        Debug.Log("[PanicAttackController] ── FORCED Blackout Sequence ──");
        yield return StartCoroutine(BlackoutSequence());

        _isActive = false;
        AnxietyManager.Instance?.NotifyPanicAttackEnded();
        Debug.Log("[PanicAttackController] ── Forced Blackout Complete ──");
    }

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
        // Start both panic audio loops via AudioManager
        AudioManager.Instance?.PlayLoop(SoundID.BreathingHeavy);
        // Heartbeat is separate — use sfxSource loop directly via a second source,
        // or rely on AudioManager's dedicated heartbeat handling
        AudioManager.Instance?.PlayOneShotOnSfx(SoundID.Heartbeat);

        float elapsed = 0f;
        while (elapsed < onsetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / onsetDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t); // ease in-out
            ApplyEffects(smoothT);
            // Volume rises with intensity — AudioManager handles gradual fade in via its own loop
            yield return null;
        }
        ApplyEffects(1f); // ensure we hit exactly 1.0

        Debug.Log("[PanicAttackController] ── Panic Attack Active (waiting for calm or timeout) ──");

        // ── Phase 2: HOLD — wait for calm-down, timeout, or anxiety dropping below Panic ──
        float holdTimer = 0f;
        while (holdTimer < panicDuration && !_calmedDown)
        {
            holdTimer += Time.deltaTime;

            // If anxiety drops below the Panic threshold mid-panic, stop the attack.
            if (AnxietyManager.Instance != null &&
                AnxietyManager.Instance.CurrentLevel < AnxietyLevel.Panic)
            {
                _calmedDown = true;
                Debug.Log("[PanicAttackController] Anxiety dropped below Panic level — panic ending early.");
                break;
            }

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
            AudioManager.Instance?.StopLoop();

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
            AudioManager.Instance?.SetLoopVolume(1f - smoothT);

            yield return null;
        }

        ApplyEffects(0f);
        SetBlackoutAlpha(1f);
        AudioManager.Instance?.StopLoop();

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

        // ── Wake-up sequence (controls OFF until fully upright) ────────────────
        yield return StartCoroutine(WakeUpSequence());
    }

    IEnumerator WakeUpSequence()
    {
        // Ensure controls stay off
        PlayerMovement.CanMove = false;
        MouseLook.CanLook = false;

        // Find camera if not assigned
        Transform cam = wakeUpCamera;
        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;

        if (cam == null)
        {
            // No camera found — just re-enable controls after a short wait
            yield return new WaitForSeconds(wakeUpLieDownDuration + wakeUpRiseDuration);
            PlayerMovement.CanMove = true;
            MouseLook.CanLook = true;
            yield break;
        }

        // Save upright state — both rotation and local eye-height position
        // We force pitch (X) and roll (Z) to 0 so the player wakes up looking straight forward
        Quaternion uprightRotation   = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        Vector3   uprightLocalPos    = cam.localPosition;
        float     eyeHeight          = uprightLocalPos.y;

        // --- Phase A: Instantly snap to ground (lying on side, at floor level) ---
        // Z roll = sideways lean, Y local position = camera height snapped to ground, X pitch = 0 (looking ahead)
        Quaternion downRotation   = Quaternion.Euler(0f, cam.eulerAngles.y, wakeUpDownPitch);
        Vector3    groundLocalPos = new Vector3(uprightLocalPos.x, wakeUpGroundY, uprightLocalPos.z);

        cam.rotation      = downRotation;
        cam.localPosition = groundLocalPos;

        Debug.Log("[PanicAttackController] Wake-up: lying on the ground.");
        yield return new WaitForSeconds(wakeUpLieDownDuration);

        // --- Phase B: Simultaneously rise in height AND roll back to upright ---
        Debug.Log("[PanicAttackController] Wake-up: getting up...");
        float elapsed = 0f;
        while (elapsed < wakeUpRiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / wakeUpRiseDuration));

            // Roll from side-tilt back to upright
            cam.rotation = Quaternion.Slerp(downRotation, uprightRotation, t);

            // Rise from ground level up to eye height
            float currentY    = Mathf.Lerp(wakeUpGroundY, eyeHeight, t);
            cam.localPosition = new Vector3(uprightLocalPos.x, currentY, uprightLocalPos.z);

            yield return null;
        }

        // Snap exactly to original state
        cam.rotation      = uprightRotation;
        cam.localPosition = uprightLocalPos;

        // Reset the mouse look internal pitch so it doesn't snap back to pre-blackout rotation
        var mouseLook = cam.GetComponent<MouseLook>();
        if (mouseLook != null)
        {
            mouseLook.ResetPitchToForward();
        }

        // Controls re-enabled — player is back in the game
        PlayerMovement.CanMove = true;
        MouseLook.CanLook = true;
        Debug.Log("[PanicAttackController] Wake-up: fully upright — controls restored.");
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
            AudioManager.Instance?.SetLoopVolume(t);
            yield return null;
        }

        ApplyEffects(0f);
        AudioManager.Instance?.StopLoop();
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

    [ContextMenu("Test: Force Blackout (Skip Hold)")]
    void TestForceBlackout() => ForceBlackout();

    [ContextMenu("Test: Calm Down")]
    void TestCalmDown() => CalmDown();
#endif
}
