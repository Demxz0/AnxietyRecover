using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Central orchestrator for the Panic Attack experience.
///
/// Listens to AnxietyManager.OnPanicAttackStarted and coordinates:
///   • Camera shake         → PanicCameraShake
///   • Tunnel vision        → URP Vignette (via Volume)
///   • Heavy breathing      → AudioManager.PlayPanicAudio()
///   • Rapid heartbeat      → AudioManager.PlayPanicAudio()  (both together)
///   • Visual distortion    → PanicDistortionEffect
///   • Blackout fade        → UI Image overlay + ALL audio fade
///
/// SETUP:
///   1. Create a GameObject "PanicAttackController" and attach this script.
///   2. Assign all references in the Inspector (see tooltips on each field).
///   3. Ensure the scene has an AnxietyManager singleton and an AudioManager singleton.
///   4. Ensure the camera has the PanicCameraShake component.
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

    [Header("Visual Distortion")]
    [Tooltip("The PanicDistortionEffect component controlling the overlay shader.")]
    [SerializeField] private PanicDistortionEffect distortionEffect;

    [Header("Blackout Overlay")]
    [Tooltip("Full-screen UI Image used for the fade-to-black effect. Start with alpha = 0.")]
    [SerializeField] private Image blackoutImage;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Recovery
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Wake-Up Sequence")]
    [Tooltip("The camera Transform used for the wake-up tilt. Auto-finds Camera.main if not set.")]
    [SerializeField] private Transform wakeUpCamera;

    [Tooltip("How far the camera tilts sideways when lying on the ground (degrees on Z axis, e.g. 80).")]
    [SerializeField] private float wakeUpDownPitch = 80f;

    [Tooltip("Local Y position of the camera when lying on the ground (relative to player body). " +
             "0 = floor level. Should be lower than normal eye height.")]
    [SerializeField] private float wakeUpGroundY = 0f;

    [Tooltip("How far sideways (left or right, local X) the camera shifts when falling. " +
             "Simulates the head sliding to the side. A small value like 0.25 works well.")]
    [SerializeField] private float wakeUpSideOffset = 0.25f;

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
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            if (!postProcessVolume.profile.TryGet(out _vignette))
                Debug.LogWarning("[PanicAttackController] Volume profile has no Vignette override!");
        }
        else
        {
            Debug.LogWarning("[PanicAttackController] No post-process Volume assigned. Vignette skipped.");
        }

        if (blackoutImage != null)
            SetBlackoutAlpha(0f);
    }

    void OnEnable()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnPanicAttackStarted += HandlePanicStarted;
        else
            StartCoroutine(SubscribeWhenReady());
    }

    void OnDisable()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnPanicAttackStarted -= HandlePanicStarted;
    }

    IEnumerator SubscribeWhenReady()
    {
        yield return null;
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnPanicAttackStarted += HandlePanicStarted;
        else
            Debug.LogError("[PanicAttackController] AnxietyManager not found in scene!");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════════════

    public void CalmDown()
    {
        if (!_isActive) { Debug.Log("[PanicAttackController] CalmDown called but no panic is active."); return; }
        _calmedDown = true;
        Debug.Log("[PanicAttackController] ✓ Player calmed down! Starting recovery...");
    }

    public bool IsActive => _isActive;

    public void ForceBlackout()
    {
        if (_panicRoutine != null) { StopCoroutine(_panicRoutine); _panicRoutine = null; }
        _panicRoutine = StartCoroutine(ForceBlackoutRoutine());
    }

    IEnumerator ForceBlackoutRoutine()
    {
        _isActive   = true;
        _calmedDown = false;
        AudioManager.Instance?.PlayPanicAudio();
        ApplyEffects(1f);
        Debug.Log("[PanicAttackController] ── FORCED Blackout Sequence ──");
        yield return StartCoroutine(BlackoutSequence());
        _isActive = false;
        AnxietyManager.Instance?.NotifyPanicAttackEnded();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PANIC SEQUENCE
    // ═══════════════════════════════════════════════════════════════════════

    void HandlePanicStarted()
    {
        if (_isActive) return;
        _panicRoutine = StartCoroutine(PanicSequence());
    }

    IEnumerator PanicSequence()
    {
        _isActive   = true;
        _calmedDown = false;
        Debug.Log("[PanicAttackController] ── Panic Attack Onset ──");

        // ── Phase 1: ONSET — start BOTH breathing AND heartbeat, ramp effects up ──
        AudioManager.Instance?.PlayPanicAudio();

        float elapsed = 0f;
        while (elapsed < onsetDuration)
        {
            elapsed += Time.deltaTime;
            float t       = Mathf.Clamp01(elapsed / onsetDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            ApplyEffects(smoothT);
            yield return null;
        }
        ApplyEffects(1f);

        Debug.Log("[PanicAttackController] ── Panic Attack Active ──");

        // ── Phase 2: HOLD — wait for calm-down or timeout ──
        float holdTimer = 0f;
        while (holdTimer < panicDuration && !_calmedDown)
        {
            holdTimer += Time.deltaTime;

            if (AnxietyManager.Instance != null &&
                AnxietyManager.Instance.CurrentLevel < AnxietyLevel.Panic)
            {
                _calmedDown = true;
                Debug.Log("[PanicAttackController] Anxiety dropped below Panic — ending early.");
                break;
            }

            float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.5f);
            ApplyEffects(flicker);
            yield return null;
        }

        if (_calmedDown)
        {
            // ── Phase 3A: RECOVERY ──
            Debug.Log("[PanicAttackController] ── Recovery Phase ──");
            yield return StartCoroutine(RampDown(recoveryDuration));
            AudioManager.Instance?.StopPanicAudio();
            _isActive = false;
            AnxietyManager.Instance.NotifyPanicAttackEnded();
            Debug.Log("[PanicAttackController] ── Panic Attack Resolved ✓ ──");
        }
        else
        {
            // ── Phase 3B: BLACKOUT ──
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
        // Find camera early so we can prep the wake-up while black
        Transform cam = wakeUpCamera;
        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;

        // ── Fade to black: effects ramp down + ALL audio fades out ──
        AudioManager.Instance?.FadeAllAudio(0f, blackoutFadeDuration);

        float elapsed = 0f;
        while (elapsed < blackoutFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t       = Mathf.Clamp01(elapsed / blackoutFadeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            ApplyEffects(1f - smoothT);
            SetBlackoutAlpha(smoothT);
            yield return null;
        }

        ApplyEffects(0f);
        SetBlackoutAlpha(1f);
        AudioManager.Instance?.StopPanicAudio();
        AudioManager.Instance?.StopAllEnvironmentalLoops();

        Debug.Log("[PanicAttackController] Character unconscious...");

        // ── While screen is BLACK: reset anxiety + snap camera to ground position ──
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.SetAnxiety(AnxietyManager.Instance.MildThreshold);

        // Snap camera while screen is fully black (player won't see the snap)
        if (cam != null)
            SnapCameraToGround(cam);

        yield return new WaitForSeconds(blackoutHoldDuration);

        // ── Fade back in ──
        elapsed = 0f;
        while (elapsed < blackoutFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / blackoutFadeDuration);
            SetBlackoutAlpha(1f - Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        SetBlackoutAlpha(0f);

        // Restore ALL audio after fade-in completes
        AudioManager.Instance?.FadeAllAudio(1f, blackoutFadeDuration * 0.5f);

        // ── Wake-up rise animation (player is already on floor, now gets up) ──
        yield return StartCoroutine(WakeUpRiseSequence(cam));
    }

    // ─── Snap camera to ground while screen is black ──────────────────────

    void SnapCameraToGround(Transform cam)
    {
        // Lock controls
        PlayerMovement.CanMove = false;
        MouseLook.CanLook      = false;

        // Determine side offset direction: prefer the side without a wall
        float sideDir = ChooseSideOffset(cam);

        Vector3 uprightLocalPos = cam.localPosition;

        // Snap to ground: low Y, sideways X offset, Z-roll tilt
        Vector3    groundLocalPos = new Vector3(uprightLocalPos.x + sideDir * wakeUpSideOffset,
                                                wakeUpGroundY,
                                                uprightLocalPos.z);
        Quaternion downRotation   = Quaternion.Euler(0f, cam.eulerAngles.y, wakeUpDownPitch * Mathf.Sign(sideDir));

        cam.localPosition = groundLocalPos;
        cam.rotation      = downRotation;

        // Store upright state for the rise phase (store in persistent fields)
        _wakeUpUprightPos    = uprightLocalPos;
        _wakeUpUprightRot    = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        _wakeUpGroundPos     = groundLocalPos;
        _wakeUpGroundRot     = downRotation;

        Debug.Log("[PanicAttackController] Wake-up: snapped to ground (screen still black).");
    }

    // Persisted between SnapCameraToGround and WakeUpRiseSequence
    private Vector3    _wakeUpUprightPos;
    private Quaternion _wakeUpUprightRot;
    private Vector3    _wakeUpGroundPos;
    private Quaternion _wakeUpGroundRot;

    // ─── Choose side offset without clipping into walls ───────────────────

    float ChooseSideOffset(Transform cam)
    {
        // Cast a small sphere to each side; pick the side with more clearance
        float checkDist  = wakeUpSideOffset + 0.15f;
        Vector3 rightDir = cam.right;

        bool rightBlocked = Physics.Raycast(cam.position, rightDir,  checkDist);
        bool leftBlocked  = Physics.Raycast(cam.position, -rightDir, checkDist);

        if (rightBlocked && !leftBlocked)  return -1f; // go left
        if (leftBlocked  && !rightBlocked) return  1f; // go right

        // Both clear or both blocked → random
        return Random.value > 0.5f ? 1f : -1f;
    }

    // ─── Rise animation after screen fades back in ────────────────────────

    IEnumerator WakeUpRiseSequence(Transform cam)
    {
        if (cam == null)
        {
            yield return new WaitForSeconds(wakeUpLieDownDuration + wakeUpRiseDuration);
            PlayerMovement.CanMove = true;
            MouseLook.CanLook      = true;
            yield break;
        }

        // Lie on floor briefly (screen already faded in — player sees the floor view)
        Debug.Log("[PanicAttackController] Wake-up: lying on floor, getting up...");
        yield return new WaitForSeconds(wakeUpLieDownDuration);

        // Rise: lerp back to upright position and rotation
        float elapsed = 0f;
        while (elapsed < wakeUpRiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / wakeUpRiseDuration));

            cam.rotation      = Quaternion.Slerp(_wakeUpGroundRot, _wakeUpUprightRot, t);
            cam.localPosition = Vector3.Lerp(_wakeUpGroundPos, _wakeUpUprightPos, t);
            yield return null;
        }

        cam.rotation      = _wakeUpUprightRot;
        cam.localPosition = _wakeUpUprightPos;

        // Reset mouse look internal state so no snap on first mouse move
        var mouseLook = cam.GetComponent<MouseLook>();
        if (mouseLook != null) mouseLook.ResetPitchToForward();

        PlayerMovement.CanMove = true;
        MouseLook.CanLook      = true;
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
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  EFFECT APPLICATION
    // ═══════════════════════════════════════════════════════════════════════

    void ApplyEffects(float t)
    {
        if (cameraShake != null)
            cameraShake.Intensity = t;

        if (_vignette != null)
            _vignette.intensity.Override(Mathf.Lerp(0f, maxVignetteIntensity, t));

        if (distortionEffect != null)
            distortionEffect.Intensity = t;
    }

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
        if (AnxietyManager.Instance != null) AnxietyManager.Instance.TriggerPanicAttack();
        else Debug.LogError("No AnxietyManager in scene!");
    }

    [ContextMenu("Test: Force Blackout (Skip Hold)")]
    void TestForceBlackout() => ForceBlackout();

    [ContextMenu("Test: Calm Down")]
    void TestCalmDown() => CalmDown();
#endif
}
